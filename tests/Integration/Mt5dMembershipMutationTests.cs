using ElectronicRegisterAPI.Domain.DTOs.Membership;
using ElectronicRegisterAPI.Domain.Results;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Application.DependencyInjection;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.DependencyInjection;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;
namespace ElectronicRegisterAPI.Tests.Integration;

[Collection("Mt5aIdentityOnly")]
public sealed class Mt5dMembershipMutationTests : IAsyncLifetime
{
    private readonly Guid _a=Guid.NewGuid(),_b=Guid.NewGuid();
    private readonly Guid[] _users=Enumerable.Range(0,4).Select(_=>Guid.NewGuid()).ToArray();
    private readonly Guid[] _members=Enumerable.Range(0,5).Select(_=>Guid.NewGuid()).ToArray();
    private string _connection=null!;
    private Mt3cHosting.ProgramHost _host=null!;
    private async Task<MySqlConnection> Open()
    {
        var c=new MySqlConnection(_connection);await c.OpenAsync();
        await using var cmd=new MySqlCommand("SELECT VERSION(),@@hostname,@@port,REPLACE(@@datadir,CHAR(92),'/'),DATABASE()",c);
        await using var r=await cmd.ExecuteReaderAsync();Assert.True(await r.ReadAsync());
        Assert.Equal("5.7.24",r.GetString(0));Assert.Equal(Environment.MachineName,r.GetString(1),ignoreCase:true);Assert.Equal((int)TestMySqlTarget.Port,r.GetInt32(2));
        Assert.EndsWith(TestMySqlTarget.DataDirectorySuffix,r.GetString(3),StringComparison.OrdinalIgnoreCase);
        Assert.Equal("electronic_register_test",r.GetString(4));return c;
    }
    private async Task Exec(string sql,params (string,object)[] args)
    {
        await using var c=await Open();await using var cmd=new MySqlCommand(sql,c);
        foreach(var (k,v) in args)cmd.Parameters.AddWithValue(k,v);await cmd.ExecuteNonQueryAsync();
    }
    private async Task<long> Scalar(string sql,params (string,object)[] args)
    {
        await using var c=await Open();await using var cmd=new MySqlCommand(sql,c);
        foreach(var(k,v)in args)cmd.Parameters.AddWithValue(k,v);return Convert.ToInt64(await cmd.ExecuteScalarAsync());
    }
    private Task<long> Generation(int index=2)=>Scalar("SELECT security_generation FROM institution_membership_security_state WHERE membership_id=@m",("@m",_members[index]));
    private Task<long> Events()=>Scalar("SELECT COUNT(*) FROM institution_membership_event WHERE institution_id IN (@a,@b)",("@a",_a),("@b",_b));
    public async Task InitializeAsync()
    {
        _connection=IntegrationFixture.ValidateLocalConnectionString(Environment.GetEnvironmentVariable("TEST_MYSQL_CONNECTION")??throw new InvalidOperationException("Explicit isolated target required"));
        var target=new MySqlConnectionStringBuilder(_connection);Assert.Equal("127.0.0.1",target.Server);Assert.Equal(TestMySqlTarget.Port,target.Port);
        target.AllowUserVariables=true;_connection=target.ConnectionString;
        await Exec("INSERT INTO institution(id,code,name,status) VALUES(@a,@ca,'Synthetic IAM A','active'),(@b,@cb,'Synthetic IAM B','active')",("@a",_a),("@b",_b),("@ca","MT5BP_"+_a.ToString("N")),("@cb","MT5BP_"+_b.ToString("N")));
        foreach(var u in _users)await Exec("INSERT INTO user(id,email,password_hash,role) VALUES(@u,@e,'not-a-login-fixture','admin')",("@u",u),("@e",u.ToString("N")+"@mt5d.example.test"));
        for(var i=0;i<5;i++)await Exec("INSERT INTO institution_membership(id,institution_id,user_id,status) VALUES(@m,@o,@u,'active'); INSERT INTO institution_membership_role(membership_id,role) VALUES(@m,@r)",
            ("@m",_members[i]),("@o",i==4?_b:_a),("@u",_users[i==4?2:i]),("@r",i<2?"InstitutionAdmin":"Teacher"));
        _host=await Mt3cHosting.ProgramHost.StartAsync(_connection,_a,Array.Empty<Guid>(),enabled:false,school:false,course:false,exam:false,membershipAdmin:false);
    }
    public async Task DisposeAsync()
    {
        if(_host is not null)await _host.DisposeAsync();if(_connection is null)return;
        await Exec("SET @er_mt5b_fixture_owners=@owners; DELETE FROM institution_membership_event WHERE institution_id IN (@a,@b); DELETE r FROM institution_membership_role r JOIN institution_membership m ON m.id=r.membership_id WHERE m.institution_id IN (@a,@b); DELETE FROM institution_membership WHERE institution_id IN (@a,@b); DELETE FROM institution WHERE id IN (@a,@b); DELETE FROM user WHERE id IN (@u0,@u1,@u2,@u3); SET @er_mt5b_fixture_owners=NULL",
            ("@owners",_a+","+_b),("@a",_a),("@b",_b),("@u0",_users[0]),("@u1",_users[1]),("@u2",_users[2]),("@u3",_users[3]));
    }
    private async Task<TenantContext> Context(int index=0)
    {
        using var scope=_host.Host.Services.CreateScope();var m=await scope.ServiceProvider.GetRequiredService<IMembershipAuthorizationRepository>()
            .GetAsync(_users[index==4?2:index],index==4?_b:_a,_members[index]);Assert.NotNull(m);
        return new(m!.UserId,m.InstitutionId,m.MembershipId,m.Roles,m.SecurityGeneration);
    }
    private async Task<MembershipMutationResult> Apply(MembershipMutation request,TenantContext? actor=null)
    {
        actor??=await Context();using var scope=_host.Host.Services.CreateScope();
        var current=scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        current.GetType().GetMethod("Set",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(current,new object[]{actor});
        return await scope.ServiceProvider.GetRequiredService<IInstitutionMembershipAdminManager>().MutateAsync(request);
    }
    private async Task<MembershipMutationResult> Direct(MembershipMutation request,TenantContext actor)
    {
        using var scope=_host.Host.Services.CreateScope();return await scope.ServiceProvider.GetRequiredService<IInstitutionMembershipMutationRepository>().MutateAsync(actor,request);
    }
    private async Task<MembershipMutation> Request(MembershipMutationAction action=MembershipMutationAction.AddRole,InstitutionRole? role=InstitutionRole.Student,int target=2)=>
        new(_members[target],Guid.NewGuid(),await Generation(target),action,role,"Synthetic IAM security test");

    [Fact]
    public async Task RolesAndStatusAreAuditedWithSqlOwnedRevisionAndTenantIsolation()
    {
        var b=await Generation(4);var start=await Generation();
        var added=await Apply(await Request());Assert.Equal(start+1,added.Revision);Assert.Contains(InstitutionRole.Student,added.Roles);
        var removed=await Apply(await Request(MembershipMutationAction.RemoveRole));Assert.Equal(start+2,removed.Revision);Assert.DoesNotContain(InstitutionRole.Student,removed.Roles);
        var suspended=await Apply(await Request(MembershipMutationAction.Suspend,null));Assert.Equal(MembershipStatus.Suspended,suspended.Status);
        var active=await Apply(await Request(MembershipMutationAction.Reactivate,null));Assert.Equal(MembershipStatus.Active,active.Status);Assert.Equal(start+4,active.Revision);
        Assert.Equal(b,await Generation(4));Assert.Equal(4,await Events());
        Assert.Equal(4,await Scalar("SELECT COUNT(*) FROM institution_membership_event WHERE institution_id=@a AND actor_user_id=@u AND actor_membership_id=@m AND target_membership_id=@t AND revision_after=revision_before+1 AND occurred_at_utc BETWEEN UTC_TIMESTAMP()-INTERVAL 2 MINUTE AND UTC_TIMESTAMP()+INTERVAL 2 MINUTE",("@a",_a),("@u",_users[0]),("@m",_members[0]),("@t",_members[2])));
    }
    [Fact]
    public async Task ReplayReturnsOriginalResultAndConflictingEventDoesNotMutate()
    {
        var r=await Request();var first=await Apply(r);
        await Apply(await Request(MembershipMutationAction.Suspend,null));
        var replay=await Apply(r);Assert.Equal(first.EventId,replay.EventId);Assert.Equal(first.Revision,replay.Revision);Assert.Equal(first.Status,replay.Status);Assert.Equal(first.Roles,replay.Roles);
        await Assert.ThrowsAsync<MembershipConflictException>(()=>Apply(r with{Reason="Different intent"}));
        await Assert.ThrowsAsync<MembershipConflictException>(()=>Apply(r with{ExpectedRevision=r.ExpectedRevision+1}));
        await Assert.ThrowsAsync<MembershipConflictException>(()=>Apply(r with{MembershipId=_members[3]}));Assert.Equal(2,await Events());
        await Assert.ThrowsAsync<MembershipConflictException>(()=>Apply(r with{MembershipId=Guid.NewGuid()}));
    }
    [Theory]
    [InlineData("stale")][InlineData("noop-role")][InlineData("noop-status")]
    public async Task ConflictDoesNotAdvanceRevisionOrAppendAudit(string kind)
    {
        var r=await Request();r=kind switch{"stale"=>r with{ExpectedRevision=0},"noop-role"=>r with{Role=InstitutionRole.Teacher},_=>r with{Action=MembershipMutationAction.Reactivate,Role=null}};
        await Assert.ThrowsAsync<MembershipConflictException>(()=>Apply(r));Assert.Equal(1,await Generation());Assert.Equal(0,await Events());
    }
    [Theory]
    [InlineData("non-admin")][InlineData("cross-tenant")][InlineData("self")][InlineData("suspended")][InlineData("stale-context")]
    public async Task AuthorizationIsCurrentScopedAndNeverUsesGlobalUserAdmin(string kind)
    {
        var actor=await Context(kind=="non-admin"?3:0);var r=await Request();
        if(kind=="cross-tenant")r=r with{MembershipId=_members[4]};
        if(kind=="self")r=r with{MembershipId=actor.MembershipId};
        if(kind=="suspended")await Exec("UPDATE institution_membership SET status='suspended' WHERE id=@m",("@m",actor.MembershipId));
        if(kind=="stale-context")await Exec("INSERT INTO institution_membership_role(membership_id,role) VALUES(@m,'Teacher')",("@m",actor.MembershipId));
        if(kind=="cross-tenant")await Assert.ThrowsAsync<KeyNotFoundException>(()=>Direct(r,actor));
        else await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Direct(r,actor));
        Assert.Equal(0,await Events());Assert.Equal(1,await Generation());
    }
    [Fact]
    public async Task ReplayReauthorizesRevokedActorUnderMutex()
    {
        await Exec("INSERT INTO institution_membership_role(membership_id,role) VALUES(@m,'Teacher')",("@m",_members[0]));
        var r=await Request();var actor=await Context();await Apply(r,actor);
        await Apply(await Request(MembershipMutationAction.RemoveRole,InstitutionRole.InstitutionAdmin,0),await Context(1));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Direct(r,actor));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async()=>await Direct(r,await Context()));Assert.Equal(2,await Events());
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task MutualAdminRevocationsSerializeAndLeaveAnActiveAdmin(bool suspend)
    {
        var ca=await Context();var cb=await Context(1);
        var ra=await Request(suspend?MembershipMutationAction.Suspend:MembershipMutationAction.RemoveRole,suspend?null:InstitutionRole.InstitutionAdmin,1);
        var rb=await Request(suspend?MembershipMutationAction.Suspend:MembershipMutationAction.RemoveRole,suspend?null:InstitutionRole.InstitutionAdmin,0);
        async Task<bool> Try(MembershipMutation r,TenantContext c){try{await Direct(r,c);return true;}catch(UnauthorizedAccessException){return false;}}
        var results=await Task.WhenAll(Try(ra,ca),Try(rb,cb));Assert.Equal(1,results.Count(x=>x));Assert.Equal(1,await Events());
        Assert.Equal(1,await Scalar("SELECT COUNT(*) FROM institution_membership m JOIN institution_membership_role r ON r.membership_id=m.id JOIN user u ON u.id=m.user_id WHERE m.institution_id=@a AND m.status='active' AND r.role='InstitutionAdmin'",("@a",_a)));
    }
    [Fact]
    public async Task ConcurrentSameEventAppliesOnceAndDistinctStaleRevisionLoses()
    {
        var a=await Context();var b=await Context(1);var r=await Request();
        var same=await Task.WhenAll(Direct(r,a),Direct(r,a));Assert.Equal(same[0].Revision,same[1].Revision);Assert.Equal(1,await Events());
        var r1=await Request(MembershipMutationAction.RemoveRole);var r2=await Request(MembershipMutationAction.Suspend,null);
        async Task<bool> Try(MembershipMutation x,TenantContext c){try{await Direct(x,c);return true;}catch(MembershipConflictException){return false;}}
        var results=await Task.WhenAll(Try(r1,a),Try(r2,b));Assert.Equal(1,results.Count(x=>x));Assert.Equal(2,await Events());Assert.Equal(3,await Generation());
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task AuditOrCommitFailureRollsBackMutationGenerationAndEvent(bool commitFault)
    {
        var actor=await Context();var request=await Request();
        var cfg=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"ConnectionStrings:DefaultConnection",_connection}}).Build();
        var services=new ServiceCollection().AddLogging().AddInfrastructure(cfg);
        var commit=new CommitFault();var audit=new AuditFault();
        services.AddDbContext<ElectronicRegisterContext>(o=>o.AddInterceptors(commitFault?commit:audit));
        await using var provider=services.BuildServiceProvider();using var scope=provider.CreateScope();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>scope.ServiceProvider.GetRequiredService<IInstitutionMembershipMutationRepository>().MutateAsync(actor,request));
        Assert.True(commitFault?commit.Reached:audit.Reached);
        Assert.Equal(1,await Generation());Assert.Equal(0,await Events());
        Assert.Equal(0,await Scalar("SELECT COUNT(*) FROM institution_membership_role WHERE membership_id=@m AND role='Student'",("@m",_members[2])));
    }
    [Fact]
    public async Task AuditCannotBeChangedOrPurgedWithoutExplicitSyntheticOwnership()
    {
        await Apply(await Request());
        foreach(var sql in new[]{"UPDATE institution_membership_event SET reason='changed' WHERE institution_id=@a","DELETE FROM institution_membership_event WHERE institution_id=@a"})
            Assert.Equal(1644,(await Assert.ThrowsAsync<MySqlException>(()=>Exec(sql,("@a",_a)))).Number);
        Assert.Equal(1644,(await Assert.ThrowsAsync<MySqlException>(()=>Exec("SET @er_mt5b_fixture_owners=@b; DELETE FROM institution_membership_event WHERE institution_id=@a; SET @er_mt5b_fixture_owners=NULL",("@a",_a),("@b",_b.ToString())))).Number);
        Assert.Equal(1,await Events());
    }
    [Fact]
    public async Task RealJwtIsPermanentlyRevokedByIamAndOtherTenantRemainsValid()
    {
        async Task<string> Token(int index){using var s=_host.Host.Services.CreateScope();return s.ServiceProvider.GetRequiredService<IInstitutionTokens>().Access(await Context(index));}
        async Task<HttpStatusCode> Status(string token){using var r=new HttpRequestMessage(HttpMethod.Get,"/api/Auth/context");r.Headers.Authorization=new("Bearer",token);using var response=await _host.Client.SendAsync(r);return response.StatusCode;}
        var old=await Token(2);var other=await Token(4);Assert.Equal(HttpStatusCode.OK,await Status(old));
        await Apply(await Request());Assert.Equal(HttpStatusCode.Forbidden,await Status(old));
        await Apply(await Request(MembershipMutationAction.RemoveRole));Assert.Equal(HttpStatusCode.Forbidden,await Status(old));
        var current=await Token(2);Assert.Equal(HttpStatusCode.OK,await Status(current));
        await Apply(await Request(MembershipMutationAction.Suspend,null));Assert.Equal(HttpStatusCode.Forbidden,await Status(current));
        await Apply(await Request(MembershipMutationAction.Reactivate,null));Assert.Equal(HttpStatusCode.Forbidden,await Status(current));
        Assert.Equal(HttpStatusCode.OK,await Status(await Token(2)));Assert.Equal(HttpStatusCode.OK,await Status(other));
    }
    [Theory]
    [InlineData(MembershipMutationAction.RemoveRole)][InlineData(MembershipMutationAction.Suspend)]
    public async Task SoleAdminCannotRemoveTheirOwnAuthority(MembershipMutationAction action)
    {
        await Exec("DELETE FROM institution_membership_role WHERE membership_id=@m AND role='InstitutionAdmin'",("@m",_members[1]));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Apply(new(_members[0],Guid.NewGuid(),1,action,
            action==MembershipMutationAction.RemoveRole?InstitutionRole.InstitutionAdmin:null,"Synthetic last-admin test")));
        Assert.Equal(1,await Generation(0));Assert.Equal(0,await Events());
        Assert.Equal(1,await Scalar("SELECT COUNT(*) FROM institution_membership m JOIN institution_membership_role r ON r.membership_id=m.id WHERE m.institution_id=@a AND m.status='active' AND r.role='InstitutionAdmin'",("@a",_a)));
    }
    [Fact]
    public async Task MissingTargetStateFailsClosedEvenForAnExistingReplay()
    {
        var r=await Request();await Apply(r);var gen=await Generation();
        await Exec("SET @er_mt5b_fixture_owners=@a; DELETE FROM institution_membership_security_state WHERE institution_id=@a AND membership_id=@m; SET @er_mt5b_fixture_owners=NULL",("@a",_a),("@m",_members[2]));
        try{await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Apply(r));}
        finally{await Exec("INSERT INTO institution_membership_security_state(institution_id,membership_id,security_generation) VALUES(@a,@m,0); UPDATE institution_membership_security_state SET security_generation=@g WHERE institution_id=@a AND membership_id=@m",("@a",_a),("@m",_members[2]),("@g",gen));}
        Assert.Equal(1,await Events());Assert.Equal(gen,await Generation());
    }
    [Fact]
    public async Task ExhaustedRevisionIsRejectedWithoutAuditOrWrapping()
    {
        // Privileged setup on this synthetic member only. Remove roles before max so
        // normal guarded fixture cleanup never needs to overflow a role-delete increment.
        await Exec("DELETE FROM institution_membership_role WHERE membership_id=@m; UPDATE institution_membership_security_state SET security_generation=9223372036854775807 WHERE membership_id=@m",("@m",_members[2]));
        await Assert.ThrowsAsync<MembershipConflictException>(()=>Apply(new(_members[2],Guid.NewGuid(),long.MaxValue,MembershipMutationAction.AddRole,InstitutionRole.Student,"Synthetic overflow test")));
        Assert.Equal(long.MaxValue,await Generation());Assert.Equal(0,await Events());
    }
    [Fact]
    public async Task ConcurrentConflictingReuseOfEventIdHasOnlyOneWinner()
    {
        var actor=await Context();var r=await Request();
        async Task<bool> Try(MembershipMutation intent){try{await Direct(intent,actor);return true;}catch(MembershipConflictException){return false;}}
        var results=await Task.WhenAll(Try(r),Try(r with{Action=MembershipMutationAction.Suspend,Role=null}));
        Assert.Equal(1,results.Count(x=>x));Assert.Equal(1,await Events());Assert.Equal(2,await Generation());
    }
    [Theory]
    [InlineData("provisioning")][InlineData("suspended")][InlineData("archived")]
    public async Task OperationalIamCannotWriteIntoNonActiveInstitution(string status)
    {
        var actor=await Context();var request=await Request();
        await Exec("UPDATE institution SET status=@s WHERE id=@a",("@a",_a),("@s",status));
        var afterLifecycle=await Generation();Assert.Equal(2,afterLifecycle);
        // Even a candidate with the current generation cannot authorize IAM in
        // a non-active institution; denial must not advance it again.
        actor=new TenantContext(actor.UserId,actor.InstitutionId,actor.MembershipId,actor.Roles,afterLifecycle);
        request=request with{ExpectedRevision=afterLifecycle};
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Direct(request,actor));Assert.Equal(0,await Events());Assert.Equal(afterLifecycle,await Generation());
    }
    [Theory]
    [InlineData("generation")][InlineData("user")][InlineData("roles")]
    public async Task ForgedOrIncompleteInternalContextCannotAuthorizeWrite(string fault)
    {
        var actor=await Context();var request=await Request();
        var forged=new TenantContext(fault=="user"?_users[3]:actor.UserId,actor.InstitutionId,actor.MembershipId,
            fault=="roles"?new[]{InstitutionRole.InstitutionAdmin,InstitutionRole.Teacher}:actor.Roles,
            fault=="generation"?null:actor.SecurityGeneration);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Direct(request,forged));Assert.Equal(0,await Events());
    }
    [Fact]
    public async Task NestedRoleSnapshotsAreRejectedByTheAdditiveSqlGuard()
    {
        await Apply(await Request());
        var error=await Assert.ThrowsAsync<MySqlException>(()=>Exec("INSERT INTO institution_membership_event(institution_id,id,actor_user_id,actor_membership_id,target_membership_id,action,target_role,previous_status,new_status,previous_roles,new_roles,reason,revision_before,revision_after,request_hash,occurred_at_utc) SELECT institution_id,@event,actor_user_id,actor_membership_id,target_membership_id,action,target_role,previous_status,new_status,previous_roles,JSON_ARRAY(JSON_ARRAY('Student'),JSON_ARRAY('Teacher')),reason,revision_before,revision_after,request_hash,occurred_at_utc FROM institution_membership_event WHERE institution_id=@a",("@event",Guid.NewGuid()),("@a",_a)));
        Assert.Equal(1644,error.Number);Assert.Contains("flat catalog strings",error.Message);Assert.Equal(1,await Events());
    }
    [Fact]
    public async Task EventIdentityIsTenantScopedAndCannotAffectOtherTenant()
    {
        var target=Guid.NewGuid();
        await Exec("INSERT INTO institution_membership_role(membership_id,role) VALUES(@actor,'InstitutionAdmin'); INSERT INTO institution_membership(id,institution_id,user_id,status) VALUES(@m,@b,@u,'active'); INSERT INTO institution_membership_role(membership_id,role) VALUES(@m,'Teacher')",("@actor",_members[4]),("@m",target),("@b",_b),("@u",_users[0]));
        var r=await Request();await Apply(r);
        var other=await Direct(r with{MembershipId=target},await Context(4));Assert.Equal(2,other.Revision);
        Assert.Equal(2,await Events());Assert.Equal(2,await Generation());
        await Assert.ThrowsAsync<MembershipConflictException>(()=>Apply(r with{MembershipId=target}));
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>Apply(r with{MembershipId=target,EventId=Guid.NewGuid()}));
    }
    [Fact]
    public async Task OfflineGraphClassifiesValidatedIamLedgerWithoutChangingData()
    {
        await Apply(await Request());var revision=await Generation();
        await using var c=await Open();await using var cmd=new MySqlCommand("SELECT id FROM institution WHERE code='MT3A_LEGACY_TEST'",c);
        var holding=Guid.Parse((await cmd.ExecuteScalarAsync())!.ToString()!);
        await ElectronicRegisterAPI.Infrastructure.Persistence.Migrations.Mt3aOwnershipMigration.ValidateGraphAsync(c,holding);
        Assert.Equal(1,await Events());Assert.Equal(revision,await Generation());
    }
    private sealed class AuditFault:DbCommandInterceptor
    {
        internal bool Reached { get; private set; }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,CommandEventData data,InterceptionResult<int> result,CancellationToken ct=default)
        {
            if(command.CommandText.StartsWith("INSERT INTO institution_membership_event",StringComparison.Ordinal))
            {Reached=true;throw new InvalidOperationException("Synthetic before-append failure");}
            return ValueTask.FromResult(result);
        }
    }
    private sealed class CommitFault:DbTransactionInterceptor
    {
        internal bool Reached { get; private set; }
        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,TransactionEventData data,InterceptionResult result,CancellationToken ct=default)
        {
            await using var cmd=transaction.Connection!.CreateCommand();cmd.Transaction=transaction;
            cmd.CommandText="SELECT COUNT(*) FROM institution_membership_event WHERE reason='Synthetic IAM security test'";
            Assert.Equal(1L,Convert.ToInt64(await cmd.ExecuteScalarAsync(ct)));
            Reached=true;throw new InvalidOperationException("Synthetic after-append before-commit failure");
        }
    }
}
