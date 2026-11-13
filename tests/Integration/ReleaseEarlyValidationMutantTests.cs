using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.Authorization;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

public sealed partial class InstitutionClassCrudTests
{
    [Fact]
    public async Task Release_ValidationBeforeLockMutantCannotProveSerialization()
    {
        var prepared=await PrepareQualityWrite("teacher","update");var reached=Phase3.Signal();var release=Phase3.Signal();
        await using var host=await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString,_a,[],enabled:false,school:true,exam:true,configureServices:s=>
            s.AddScoped<IInstitutionWriteAuthorization>(sp=>new EarlyValidationMutant(sp.GetRequiredService<ElectronicRegisterContext>(),sp.GetRequiredService<IMembershipAuthorizationRepository>(),reached,release)));
        var write=SqlLockEvidence.Track(()=>Request(prepared.Route,prepared.Token,prepared.Body,prepared.Method,host));
        await reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var revoke=SqlLockEvidence.Track(()=>Execute("UPDATE institution_membership SET status='suspended' WHERE id=@m",("@m",Id("teacher-membership"))));
        try{await revoke;await Assert.ThrowsAnyAsync<Xunit.Sdk.XunitException>(()=>SqlLockEvidence.WaitAsync(fixture.ConnectionString,revoke,write));}
        finally{release.TrySetResult(true);}
        await Expect(await write,HttpStatusCode.NoContent); // The intentionally unsafe test-only mutation.
    }
    private sealed class EarlyValidationMutant(ElectronicRegisterContext db,IMembershipAuthorizationRepository memberships,TaskCompletionSource<bool> reached,TaskCompletionSource<bool> release):IInstitutionWriteAuthorization
    {
        private bool locked;
        public Task LockAsync(TenantContext actor,CancellationToken ct=default)=>LockExamAsync(actor,ct);
        public async Task LockExamAsync(TenantContext actor,CancellationToken ct=default)
        {
            if(!MembershipAuthorizationRules.Matches(actor,await memberships.GetAsync(actor.UserId,actor.InstitutionId,actor.MembershipId,ct)))throw new UnauthorizedAccessException();
            reached.TrySetResult(true);await release.Task.WaitAsync(TimeSpan.FromSeconds(15),ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM institution WHERE id={actor.InstitutionId} FOR UPDATE",ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM institution_membership WHERE institution_id={actor.InstitutionId} AND id={actor.MembershipId} LOCK IN SHARE MODE",ct);
            locked=true; // Deliberately omit the post-lock current authorization read.
        }
        public void RequireLocked(TenantContext actor){if(!locked||db.Database.CurrentTransaction is null)throw new UnauthorizedAccessException();}
    }
}
