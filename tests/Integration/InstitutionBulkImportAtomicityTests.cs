using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace ElectronicRegisterAPI.Tests.Integration;

public sealed partial class InstitutionClassCrudTests
{
    [Theory]
    [InlineData("second-person")][InlineData("audit")]
    public async Task BulkImport_FaultRollsBackAllPeopleReferencesAndBatchCompletion(string fault)
    {
        await using var host=await ImportHost();var token=await Access();var b=await PreviewImport(host,token,"Nome;Cognome;RiferimentoEsterno\nMario;Rossi;P1\nFaultImport;Verdi;P2");var before=await Digest(false);
        var trigger="import_test_fault_"+Guid.NewGuid().ToString("N");
        var ddl=fault=="second-person"?$"CREATE TRIGGER {trigger} BEFORE INSERT ON student FOR EACH ROW BEGIN IF NEW.institution_id='{_a:D}' AND NEW.first_name='FaultImport' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Injected row fault'; END IF; END":
            $"CREATE TRIGGER {trigger} AFTER INSERT ON institution_import_event FOR EACH ROW BEGIN IF NEW.institution_id='{_a:D}' AND NEW.action='Confirmed' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Injected import audit fault'; END IF; END";
        await Execute(ddl);
        try{await Expect(await ConfirmImport(host,token,b),HttpStatusCode.Conflict);}
        finally{await Execute($"DROP TRIGGER {trigger}");}
        Assert.Equal(before,await Digest(false));
        await Db(async db=>{Assert.False(await db.Set<InstitutionImportItem>().AnyAsync(i=>i.BatchId==b.Id));Assert.False(await db.Set<InstitutionImportExternalReference>().AnyAsync(i=>i.InstitutionId==_a));Assert.Equal("Preview",(await db.Set<InstitutionImportBatch>().SingleAsync(i=>i.Id==b.Id)).State);});
    }
    [Theory]
    [InlineData(EmailCollisionStrategy.Reject)][InlineData(EmailCollisionStrategy.NumericSuffix)]
    public async Task BulkImport_GlobalCollisionCannotSilentlyChangeConfirmedEmailsAndRollsBackEarlierRows(EmailCollisionStrategy collision)
    {
        await using var host=await ImportHost();var token=await Access();var config=EmailConfig("{nome}.{cognome}",collision:collision);await PublishEmail(host,token,config);
        await Execute("UPDATE user SET email=@email WHERE id=@id",("@email","marco.rossi@"+config.Domain),("@id",_teacherUser));
        var b=await PreviewImport(host,token,"Nome;Cognome\nMario;Rossi\nMarco;Rossi",new(ImportCategory.Student,ImportEmailMode.ReserveInstitutionalAddress));var before=await Digest(false);
        var response=await ConfirmImport(host,token,b);await Expect(response,HttpStatusCode.Conflict);Assert.DoesNotContain(config.Domain,await response.Content.ReadAsStringAsync());Assert.Equal(before,await Digest(false));
        await Db(async db=>{Assert.False(await db.Set<InstitutionEmailAssignment>().AnyAsync(a=>a.InstitutionId==_a));Assert.False(await db.Set<InstitutionImportItem>().AnyAsync(i=>i.BatchId==b.Id));Assert.False(await db.Set<InstitutionEmailEvent>().AnyAsync(e=>e.InstitutionId==_a&&e.AssignmentId!=null));});
        if(collision==EmailCollisionStrategy.NumericSuffix)
        {
            var changed=await Request(Imports+"/"+b.Id+"/mapping",token,new ImportRemap(Guid.NewGuid(),b.Revision,new(ImportCategory.Student,ImportEmailMode.ReserveInstitutionalAddress,EmailCandidates:new[]{new ImportEmailCandidateChoice(3,1)})),"PUT",host);
            await Expect(changed,HttpStatusCode.OK);var next=(await changed.Content.ReadFromJsonAsync<ImportBatch>())!;
            await Expect(await ConfirmImport(host,token,next),HttpStatusCode.OK);
            await Db(async db=>Assert.True(await db.Set<InstitutionEmailAssignment>().AnyAsync(a=>a.InstitutionId==_a&&a.Email=="marco.rossi_2@"+config.Domain)));
        }
    }
    [Theory]
    [InlineData(true)][InlineData(false)]
    public async Task BulkImport_ConcurrentConfirmationAndLostResponseCannotDuplicatePeople(bool sameEvent)
    {
        await using var host=await ImportHost();var token=await Access();var b=await PreviewImport(host,token,"Nome;Cognome\nMario;Rossi\nAnna;Verdi");var eventId=Guid.NewGuid();
        var secondEvent=sameEvent?eventId:Guid.NewGuid();var a=ConfirmImport(host,token,b,eventId);var c=ConfirmImport(host,token,b,secondEvent);var results=await Task.WhenAll(a,c);
        Assert.Equal(sameEvent?2:1,results.Count(r=>r.StatusCode==HttpStatusCode.OK));Assert.Equal(sameEvent?0:1,results.Count(r=>r.StatusCode==HttpStatusCode.Conflict));
        // Discard a committed HTTP response and retry with the same durable request identity.
        var winner=results[0].StatusCode==HttpStatusCode.OK?0:1;var winningEvent=winner==0?eventId:secondEvent;
        results[winner].Dispose();var retry=await ConfirmImport(host,token,b,winningEvent);await Expect(retry,HttpStatusCode.OK);Assert.Equal(2,(await retry.Content.ReadFromJsonAsync<ImportResult>())!.CreatedCount);
        await Db(async db=>{Assert.Equal(2,await db.Set<InstitutionImportItem>().CountAsync(i=>i.BatchId==b.Id));Assert.Single(await db.Set<InstitutionImportEvent>().Where(e=>e.BatchId==b.Id&&e.Action=="Confirmed").ToArrayAsync());});
    }
    [Fact]
    public async Task BulkImport_StaffRevocationWaitsForCommitAndThenBlocksOldToken()
    {
        await using var setup=await ImportHost();var admin=await Access();var token=await StaffToken();var b=await PreviewImport(setup,token,"Nome;Cognome\nMario;Rossi");
        var acquired=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var host=await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString,_a,Array.Empty<Guid>(),enabled:false,school:true,exam:true,membershipAdmin:true,membershipWrite:true,
            extraArguments:new[]{"--Multitenancy:InstitutionImport:Enabled=true"},configureServices:services=>{var descriptor=services.Last(s=>s.ServiceType==typeof(IInstitutionWriteAuthorization));services.AddScoped<IInstitutionWriteAuthorization>(sp=>new StaffWriteBarrier((IInstitutionWriteAuthorization)ActivatorUtilities.CreateInstance(sp,descriptor.ImplementationType!),acquired,release));});
        long revision=0;await Db(async db=>revision=(await db.InstitutionMembershipSecurityStates.SingleAsync(s=>s.MembershipId==Id("teacher-membership"))).SecurityGeneration);
        var write=ConfirmImport(host,token,b);await acquired.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var revoke=Request("/api/institution/admin/members/"+Id("teacher-membership")+"/mutations",admin,new{EventId=Guid.NewGuid(),ExpectedRevision=revision,Action="RemoveRole",Role="Staff",Reason="Concurrent import revocation"},"POST",host);
        try{await Task.WhenAny(revoke,Task.Delay(250));Assert.False(revoke.IsCompleted);}finally{release.TrySetResult(true);}
        await Expect(await write,HttpStatusCode.OK);await Expect(await revoke,HttpStatusCode.OK);await Expect(await ConfirmImport(setup,token,b),HttpStatusCode.Forbidden);
    }
}
