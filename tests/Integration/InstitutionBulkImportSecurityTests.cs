using ElectronicRegisterAPI.Domain.DTOs.Import;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;
namespace ElectronicRegisterAPI.Tests.Integration;

public sealed partial class InstitutionClassCrudTests
{
    [Fact]
    public async Task BulkImport_PrivatePreviewIsNotSharedWithAnotherAdministrator()
    {
        await using var host=await ImportHost();var token=await Access();var b=await PreviewImport(host,token,"Nome;Cognome\nMario;Rossi");
        var login=await Request("/api/Auth/identity/login",body:new LoginDto{Email=_admin2.ToString("N")+"@mtclass.example.test",Password=Password},method:"POST");await Expect(login,HttpStatusCode.OK);
        var ticket=(await login.Content.ReadFromJsonAsync<IdentityTicketDto>())!;
        var selection=await Request("/api/Auth/select-institution",ticket.Ticket,new SelectInstitutionDto{InstitutionId=_a,MembershipId=Id("admin2-membership")},"POST");await Expect(selection,HttpStatusCode.OK);
        var other=(await selection.Content.ReadFromJsonAsync<InstitutionAccessDto>())!.Token;
        await Expect(await Request(Imports+"/"+b.Id,other,host:host),HttpStatusCode.NotFound);await Expect(await ConfirmImport(host,other,b),HttpStatusCode.NotFound);
        var list=(await(await Request(Imports,other,host:host)).Content.ReadFromJsonAsync<ImportBatchPage>())!;Assert.Empty(list.Items);
    }
    [Theory]
    [InlineData("Role")][InlineData("UserId")][InlineData("InstitutionId")][InlineData("Email")][InlineData("PasswordHash")]
    public async Task BulkImport_ConfirmationRejectsUnknownSecurityFields(string field)
    {
        await using var host=await ImportHost();var token=await StaffToken();var b=await PreviewImport(host,token,"Nome;Cognome\nMario;Rossi");
        var body=new JsonObject{["eventId"]=Guid.NewGuid(),["expectedRevision"]=b.Revision,["previewDigest"]=b.PreviewDigest,[field]="forged"};
        await Expect(await Request(Imports+"/"+b.Id+"/confirm",token,body,"POST",host),HttpStatusCode.BadRequest);
        await Db(async db=>Assert.False(await db.Set<InstitutionImportItem>().AnyAsync(i=>i.BatchId==b.Id)));
    }
    [Fact]
    public async Task BulkImport_DisabledRolloutAndSelectionTokensCannotMutate()
    {
        await using var disabled=await ImportHost(false);var token=await Access();await Expect(await UploadImport(disabled,token,Encoding.UTF8.GetBytes("Nome;Cognome\nA;B"),new(ImportCategory.Student)),HttpStatusCode.Forbidden);
        await using var active=await ImportHost();await Expect(await UploadImport(active,await Ticket(),Encoding.UTF8.GetBytes("Nome;Cognome\nA;B"),new(ImportCategory.Student)),HttpStatusCode.Unauthorized);
    }
    [Theory]
    [InlineData("suspend")][InlineData("generation")][InlineData("revoke")]
    public async Task BulkImport_StaleMembershipCannotConfirmStoredPreview(string kind)
    {
        await using var host=await ImportHost();var token=await StaffToken();var b=await PreviewImport(host,token,"Nome;Cognome\nMario;Rossi");
        if(kind=="suspend")await Execute("UPDATE institution_membership SET status='suspended' WHERE id=@id",("@id",Id("teacher-membership")));
        else if(kind=="generation")await Execute("UPDATE institution_membership_security_state SET security_generation=security_generation+1 WHERE membership_id=@id",("@id",Id("teacher-membership")));
        else await Execute("DELETE FROM institution_membership_role WHERE membership_id=@id AND role='Staff'",("@id",Id("teacher-membership")));
        await Expect(await ConfirmImport(host,token,b),HttpStatusCode.Forbidden);await Db(async db=>Assert.False(await db.Set<InstitutionImportItem>().AnyAsync(i=>i.BatchId==b.Id)));
    }
    [Fact]
    public async Task BulkImport_ChangingMappingInvalidatesOldDigestAndCanResolveAmbiguousClass()
    {
        var path=Guid.NewGuid();var bsp=Guid.NewGuid();var secondClass=Guid.NewGuid();
        await Execute("INSERT INTO study_path(id,name,description,institution_id) VALUES(@id,'Import alternative path','Test',@t)",("@id",path),("@t",_a));
        await Execute("INSERT INTO biennium_study_path(id,biennium_study_area_id,study_path_id,institution_id) SELECT @id,biennium_study_area_id,@path,institution_id FROM biennium_study_path WHERE id=@source",("@id",bsp),("@path",path),("@source",Id("bsp")));
        await Execute("INSERT INTO class(id,biennium_study_path_id,name,institution_id) VALUES(@id,@bsp,'Class',@t)",("@id",secondClass),("@bsp",bsp),("@t",_a));
        await using var host=await ImportHost();var token=await StaffToken();var b=await PreviewImport(host,token,"Nome;Cognome;Classe\nMario;Rossi;Class");Assert.Equal(1,b.InvalidRows);
        var rows=(await(await Request(Imports+"/"+b.Id+"/rows",token,host:host)).Content.ReadFromJsonAsync<ImportRowsPage>())!;Assert.Equal(ImportRowStatus.AmbiguousReference,rows.Items[0].Data.Status);Assert.Contains(secondClass,rows.Items[0].ClassCandidates!);Assert.DoesNotContain(Id("class",true),rows.Items[0].ClassCandidates!);
        var changed=await Request(Imports+"/"+b.Id+"/mapping",token,new ImportRemap(Guid.NewGuid(),b.Revision,new(ImportCategory.Student,Classes:new[]{new ImportClassChoice(2,secondClass)})),"PUT",host);await Expect(changed,HttpStatusCode.OK);var next=(await changed.Content.ReadFromJsonAsync<ImportBatch>())!;Assert.Equal(2,next.Revision);Assert.Equal(0,next.InvalidRows);
        await Expect(await ConfirmImport(host,token,b),HttpStatusCode.Conflict);await Expect(await ConfirmImport(host,token,next),HttpStatusCode.OK);
    }
    [Fact]
    public async Task BulkImport_ClassChangeBeforeConfirmationRequiresFreshPreview()
    {
        await using var host=await ImportHost();var token=await Access();var b=await PreviewImport(host,token,"Nome;Cognome;Classe\nMario;Rossi;"+Id("class"));
        await Execute("UPDATE class SET name='Changed' WHERE id=@id",("@id",Id("class")));
        await Expect(await ConfirmImport(host,token,b),HttpStatusCode.Conflict);await Db(async db=>Assert.False(await db.Set<InstitutionImportItem>().AnyAsync(i=>i.BatchId==b.Id)));
    }
    [Fact]
    public async Task BulkImport_IdenticalFileRequiresExplicitIntentButHomonymsAreAllowed()
    {
        await using var host=await ImportHost();var token=await Access();const string csv="Nome;Cognome\nMario;Rossi";var first=await PreviewImport(host,token,csv);await Expect(await ConfirmImport(host,token,first),HttpStatusCode.OK);
        var again=await PreviewImport(host,token,csv);Assert.Contains(again.Warnings,w=>w.Code=="FilePreviouslyImported");await Expect(await ConfirmImport(host,token,again),HttpStatusCode.Conflict);
        await Expect(await ConfirmImport(host,token,again,repeat:true,reason:"Distinct homonym; intentional repeat"),HttpStatusCode.OK);
        await Db(async db=>Assert.Equal(2,await db.Set<InstitutionImportItem>().CountAsync(i=>i.InstitutionId==_a)));
    }
    [Fact]
    public async Task BulkImport_ExistingExternalReferenceBlocksCreateOnlyWithoutMerge()
    {
        await using var host=await ImportHost();var token=await Access();const string csv="Nome;Cognome;RiferimentoEsterno\nMario;Rossi;stable-1";var first=await PreviewImport(host,token,csv);await Expect(await ConfirmImport(host,token,first),HttpStatusCode.OK);
        var again=await PreviewImport(host,token,csv);Assert.Equal(1,again.InvalidRows);await Expect(await ConfirmImport(host,token,again,repeat:true,reason:"Cannot overwrite stable identity"),HttpStatusCode.UnprocessableEntity);
    }
    [Fact]
    public async Task BulkImport_ExpiredStagingIsUnreadableAndRetentionPurgesOnlyPayload()
    {
        await using var host=await ImportHost();var token=await Access();var original=await PreviewImport(host,token,"Nome;Cognome\nMario;Rossi");var id=Guid.NewGuid();
        await Execute("INSERT INTO institution_import_batch(id,institution_id,actor_user_id,actor_membership_id,category,state,revision,file_sha256,preview_digest,total_rows,valid_rows,invalid_rows,payload,warnings_json,policy_revision,created_at_utc,expires_at_utc) SELECT @id,institution_id,actor_user_id,actor_membership_id,category,state,revision,file_sha256,preview_digest,total_rows,valid_rows,invalid_rows,payload,warnings_json,policy_revision,UTC_TIMESTAMP(6)-INTERVAL 2 HOUR,UTC_TIMESTAMP(6)-INTERVAL 1 HOUR FROM institution_import_batch WHERE id=@source",("@id",id),("@source",original.Id));
        await Execute("INSERT INTO institution_import_event(institution_id,id,batch_id,revision,action,actor_kind,actor_user_id,actor_membership_id,request_hash,result_json,reason,occurred_at_utc) VALUES(@t,@id,@batch,1,'Previewed','User',@u,@m,@hash,JSON_OBJECT('preview',true),'Expired fixture',UTC_TIMESTAMP(6))",("@t",_a),("@id",Guid.NewGuid()),("@batch",id),("@u",_adminUser),("@m",Id("admin-membership")),("@hash",new string('A',64)));
        await Expect(await Request(Imports+"/"+id+"/rows",token,host:host),HttpStatusCode.Conflict);
        // Retention is deliberately bounded. Earlier rehearsal runs can leave more
        // than one batch of expired staging; one cycle need not select this tenant.
        var pending=0;
        await Db(async db=>pending=await db.Set<InstitutionImportBatch>().CountAsync(b=>b.State=="Preview"&&b.ExpiresAtUtc<=DateTime.UtcNow));
        using(var scope=host.Host.Services.CreateScope())
        {
            var retention=scope.ServiceProvider.GetRequiredService<IInstitutionImportRetention>();
            for(var cycle=0;cycle<=pending;cycle++)
            {
                var expired=false;
                await Db(async db=>expired=await db.Set<InstitutionImportBatch>().AsNoTracking().AnyAsync(b=>b.Id==id&&b.State=="Expired"&&b.Payload==null));
                if(expired)break;
                Assert.InRange(await retention.PurgeExpiredAsync(default),1,20);
            }
        }
        await Db(async db=>{var b=await db.Set<InstitutionImportBatch>().SingleAsync(i=>i.Id==id);Assert.Null(b.Payload);Assert.Equal("Expired",b.State);var e=await db.Set<InstitutionImportEvent>().SingleAsync(e=>e.BatchId==id&&e.Action=="Expired");Assert.Equal("Retention",e.ActorKind);Assert.Null(e.ActorUserId);Assert.Null(e.ActorMembershipId);});
        await Db(async db=>Assert.NotNull((await db.Set<InstitutionImportBatch>().AsNoTracking().SingleAsync(b=>b.Id==original.Id)).Payload));
        Assert.Equal(1644,(await Assert.ThrowsAsync<MySqlException>(()=>Execute("DELETE FROM institution_import_event WHERE batch_id=@id",("@id",id)))).Number);
    }
}
