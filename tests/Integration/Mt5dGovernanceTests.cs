using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

public sealed partial class Mt5cOnboardingTests
{
    private static readonly RSA RecoverySigner=RSA.Create(3072);
    private PlatformPublicKey ConfigureGovernance(bool second=true)
    {
        host.Host.Services.GetRequiredService<IConfiguration>()["Multitenancy:Governance:Enabled"]="true";
        key.Actions=Enum.GetValues<PlatformAction>();
        var other=new PlatformPublicKey{OperatorId="operator-two",KeyId="key-two",PublicKeyPem=RecoverySigner.ExportSubjectPublicKeyInfoPem(),NotBeforeUtc=DateTime.UtcNow.AddDays(-1),NotAfterUtc=DateTime.UtcNow.AddDays(1),Actions=Enum.GetValues<PlatformAction>()};
        host.Host.Services.GetRequiredService<IOptionsMonitor<PlatformAuthorityOptions>>().CurrentValue.Keys=second?[key,other]:[key];return other;
    }
    private SignedPlatformOperation GovernanceOp(Guid owner,PlatformAction action,int recipient=1,Guid recovery=default)
    {
        var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return Sign(new("operator-one","key-one",Guid.NewGuid(),Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),now,now+240,action,new(owner,users[recipient],"","","Synthetic legally verified authority","test-evidence:recovery-review","Synthetic governance decision",recovery),""));
    }
    private SignedPlatformOperation OtherOperator(SignedPlatformOperation op)=>op with{OperatorId="operator-two",KeyId="key-two",Signature=Convert.ToBase64String(RecoverySigner.SignData(PlatformSigning.Bytes(op with{OperatorId="operator-two",KeyId="key-two"}),HashAlgorithmName.SHA256,RSASignaturePadding.Pss))};
    private async Task<PlatformResult> Govern(SignedPlatformOperation op)
    {await using var scope=host.Host.Services.CreateAsyncScope();return await scope.ServiceProvider.GetRequiredService<IInstitutionGovernanceRepository>().ExecuteAsync(op,CancellationToken.None);}
    private SignedPlatformOperation Fresh(SignedPlatformOperation op)=>Sign(op with{Nonce=Convert.ToHexString(RandomNumberGenerator.GetBytes(32))});
    private SignedPlatformOperation Approval(SignedPlatformOperation request)=>Sign(request with{Action=PlatformAction.ApproveRecovery,EventId=Guid.NewGuid(),Nonce=Convert.ToHexString(RandomNumberGenerator.GetBytes(32))});
    private async Task<(PlatformResult P,SignedPlatformOperation Request,Guid OldMember,string OldToken)> Recovery(bool approve=true)
    {
        ConfigureGovernance();var a=await Active();await Govern(GovernanceOp(a.P.InstitutionId,PlatformAction.Suspend));
        var request=GovernanceOp(a.P.InstitutionId,PlatformAction.RequestRecovery,recovery:Guid.NewGuid());var p=await Govern(request);
        Assert.False(channel.Inbox.ContainsKey(p.InvitationId!.Value));
        if(approve)
        {
            await Govern(Approval(request));Assert.False(channel.Inbox.ContainsKey(p.InvitationId.Value));
            await Govern(OtherOperator(Approval(request)));Assert.True(channel.Inbox.ContainsKey(p.InvitationId.Value));
        }
        return(p,request,a.Member,a.Token);
    }
    [Theory][InlineData(PlatformAction.Suspend)][InlineData(PlatformAction.Archive)]
    public async Task Governance_LifecycleRevokesOnlyTheSelectedInstitutionAndArchiveIsTerminal(PlatformAction action)
    {
        ConfigureGovernance();var a=await Active();var b=await Active();
        var before=await Count("SELECT security_generation FROM institution_membership_security_state WHERE membership_id=@p0",a.Member);
        var op=GovernanceOp(a.P.InstitutionId,action);await Govern(op);
        Assert.Equal(before+1,await Count("SELECT security_generation FROM institution_membership_security_state WHERE membership_id=@p0",a.Member));
        await Expect(await Http("/api/Auth/context",a.Token),HttpStatusCode.Forbidden);await Expect(await Http("/api/Auth/context",b.Token),HttpStatusCode.OK);
        if(action==PlatformAction.Suspend)
        {
            await Govern(GovernanceOp(a.P.InstitutionId,PlatformAction.Reactivate));await Expect(await Http("/api/Auth/context",a.Token),HttpStatusCode.Forbidden);
            await Expect(await Http("/api/Auth/context",await Access(a.P.InstitutionId,a.Member)),HttpStatusCode.OK);
        }
        else
        {
            await Assert.ThrowsAsync<MembershipConflictException>(()=>Govern(GovernanceOp(a.P.InstitutionId,PlatformAction.Reactivate)));
            var error=await Assert.ThrowsAsync<MySqlConnector.MySqlException>(()=>Exec("UPDATE institution SET status='active' WHERE id=@p0",a.P.InstitutionId));Assert.Equal(1644,error.Number);
        }
        Assert.Equal(1,await Count("SELECT COUNT(*) FROM institution_onboarding_event WHERE institution_id=@p0 AND id=@p1 AND actor_kind='Operator'",a.P.InstitutionId,op.EventId));
    }
    [Fact]
    public async Task Governance_DefaultClosedAndUnauthorizedOperatorsNeverMutateLifecycle()
    {
        var a=await Active();var op=GovernanceOp(a.P.InstitutionId,PlatformAction.Suspend);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Govern(op));
        ConfigureGovernance();key.Revoked=true;await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Govern(op));
        Assert.Equal(1,await Count("SELECT COUNT(*) FROM institution WHERE id=@p0 AND status='active'",a.P.InstitutionId));
    }
    [Fact]
    public async Task Governance_ReactivationRequiresAValidAdminAndOverflowRollsBack()
    {
        ConfigureGovernance();var a=await Active();await Govern(GovernanceOp(a.P.InstitutionId,PlatformAction.Suspend));
        await Exec("DELETE FROM institution_membership_role WHERE membership_id=@p0 AND role='InstitutionAdmin'",a.Member);
        await Assert.ThrowsAsync<MembershipConflictException>(()=>Govern(GovernanceOp(a.P.InstitutionId,PlatformAction.Reactivate)));
        await Exec("INSERT INTO institution_membership_role(membership_id,role) VALUES(@p0,'InstitutionAdmin')",a.Member);
        await using var connection=await Open();await using var tx=await connection.BeginTransactionAsync();
        await using(var set=new MySqlConnector.MySqlCommand("UPDATE institution_membership_security_state SET security_generation=9223372036854775807 WHERE membership_id=@id",connection,tx))
        {set.Parameters.AddWithValue("@id",a.Member);await set.ExecuteNonQueryAsync();}
        await using(var update=new MySqlConnector.MySqlCommand("UPDATE institution SET status='active' WHERE id=@id",connection,tx))
        {update.Parameters.AddWithValue("@id",a.P.InstitutionId);var error=await Assert.ThrowsAsync<MySqlConnector.MySqlException>(()=>update.ExecuteNonQueryAsync());Assert.Equal(1644,error.Number);}
        await tx.RollbackAsync();
        Assert.Equal(1,await Count("SELECT COUNT(*) FROM institution WHERE id=@p0 AND status='suspended'",a.P.InstitutionId));
    }
    [Fact]
    public async Task Governance_FaultRollsBackStatusGenerationAuditAndNonce()
    {
        ConfigureGovernance();var a=await Active();var op=GovernanceOp(a.P.InstitutionId,PlatformAction.Suspend);fault.Action="Suspend";
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Govern(op));Assert.True(fault.Reached);
        Assert.Equal(0,await Count("SELECT COUNT(*) FROM institution_onboarding_event WHERE id=@p0",op.EventId));
        Assert.Equal(0,await Count("SELECT COUNT(*) FROM platform_operation_nonce WHERE nonce=@p0",op.Nonce));
        await Expect(await Http("/api/Auth/context",a.Token),HttpStatusCode.OK);
    }
    [Fact]
    public async Task Governance_ConcurrentRetriesAreIdempotentAndRevokedOperatorsCannotReplay()
    {
        ConfigureGovernance();var a=await Active();var op=GovernanceOp(a.P.InstitutionId,PlatformAction.Suspend);
        var result=await Task.WhenAll(Govern(op),Govern(Fresh(op)));Assert.Equal(result[0],result[1]);
        Assert.Equal(1,await Count("SELECT COUNT(*) FROM institution_onboarding_event WHERE id=@p0",op.EventId));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Govern(op));
        await Assert.ThrowsAsync<MembershipConflictException>(()=>Govern(Sign(Fresh(op) with{Intent=op.Intent with{Reason="Different intent"}})));
        key.Revoked=true;await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Govern(Fresh(op)));
    }
    [Fact]
    public async Task Governance_RecoveryRequiresTwoIndependentApprovalsAndRecipientAcceptance()
    {
        var b=await Active(1);var flow=await Recovery();
        Assert.Equal(1,await Count("SELECT COUNT(*) FROM institution_membership WHERE id=@p0 AND status='active'",flow.OldMember));
        var accepted=await Accept(flow.P,1);
        Assert.Equal(1,await Count("SELECT COUNT(*) FROM institution WHERE id=@p0 AND status='active'",flow.P.InstitutionId));
        Assert.Equal(1,await Count("SELECT COUNT(*) FROM institution_membership WHERE id=@p0 AND status='suspended'",flow.OldMember));
        Assert.Equal(0,await Count("SELECT COUNT(*) FROM institution_membership_event WHERE institution_id=@p0",flow.P.InstitutionId));
        Assert.Equal(1,await Count("SELECT COUNT(*) FROM institution_onboarding_event WHERE institution_id=@p0 AND action='RecoveryAccepted' AND actor_kind='Recipient' AND actor_user_id=@p1",flow.P.InstitutionId,users[1]));
        await Expect(await Http("/api/Auth/context",flow.OldToken),HttpStatusCode.Forbidden);
        await Expect(await Http("/api/Auth/context",await Access(flow.P.InstitutionId,accepted.MembershipId!.Value,1)),HttpStatusCode.OK);
        await Expect(await Http("/api/Auth/context",b.Token),HttpStatusCode.OK);
    }
    [Fact]
    public async Task Governance_OneOperatorOrDuplicateApprovalCannotEnableRecovery()
    {
        ConfigureGovernance(false);var a=await Active();await Govern(GovernanceOp(a.P.InstitutionId,PlatformAction.Suspend));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Govern(GovernanceOp(a.P.InstitutionId,PlatformAction.RequestRecovery,recovery:Guid.NewGuid())));
        ConfigureGovernance();var request=GovernanceOp(a.P.InstitutionId,PlatformAction.RequestRecovery,recovery:Guid.NewGuid());var p=await Govern(request);
        await Govern(Approval(request));await Assert.ThrowsAsync<MembershipConflictException>(()=>Govern(Approval(request)));
        Assert.False(channel.Inbox.ContainsKey(p.InvitationId!.Value));Assert.Equal(1,await Count("SELECT COUNT(*) FROM institution_recovery_approval WHERE institution_id=@p0",p.InstitutionId));
    }
    [Fact]
    public async Task Governance_RevokedApprovalAndCrossTenantIntentCannotBeAccepted()
    {
        var flow=await Recovery();var b=await Active(2);var bad=OtherOperator(Sign(Approval(flow.Request) with{Intent=flow.Request.Intent with{InstitutionId=b.P.InstitutionId}}));
        await Assert.ThrowsAnyAsync<Exception>(()=>Govern(bad));
        host.Host.Services.GetRequiredService<IOptionsMonitor<PlatformAuthorityOptions>>().CurrentValue.Keys.Single(k=>k.OperatorId=="operator-two").Revoked=true;
        var delivery=channel.Inbox[flow.P.InvitationId!.Value];
        await Expect(await Http("/api/Auth/invitations/"+delivery.InvitationId+"/accept",await Ticket(1),new{flow.P.InstitutionId,EventId=Guid.NewGuid(),delivery.Challenge}),HttpStatusCode.Forbidden);
        Assert.Equal(0,await Count("SELECT COUNT(*) FROM institution_membership WHERE institution_id=@p0 AND user_id=@p1",flow.P.InstitutionId,users[1]));
    }
    [Fact]
    public async Task Governance_RecoveryAcceptFaultRollsBackAndConcurrentRetryConsumesOnce()
    {
        var flow=await Recovery();var delivery=channel.Inbox[flow.P.InvitationId!.Value];var ticket=await Ticket(1);var eventId=Guid.NewGuid();var body=new{flow.P.InstitutionId,EventId=eventId,delivery.Challenge};var path="/api/Auth/invitations/"+delivery.InvitationId+"/accept";
        fault.Action="RecoveryAccept";await Expect(await Http(path,ticket,body),HttpStatusCode.InternalServerError);Assert.True(fault.Reached);
        Assert.Equal(0,await Count("SELECT COUNT(*) FROM institution_membership WHERE institution_id=@p0 AND user_id=@p1",flow.P.InstitutionId,users[1]));
        Assert.Equal(1,await Count("SELECT COUNT(*) FROM institution_membership WHERE id=@p0 AND status='active'",flow.OldMember));
        fault.Action=null;var results=await Task.WhenAll(Http(path,ticket,body),Http(path,ticket,body));foreach(var r in results)await Expect(r,HttpStatusCode.OK);
        Assert.Equal(await results[0].Content.ReadAsStringAsync(),await results[1].Content.ReadAsStringAsync());
        Assert.Equal(1,await Count("SELECT COUNT(*) FROM institution_onboarding_event WHERE institution_id=@p0 AND action='RecoveryAccepted'",flow.P.InstitutionId));
    }

    [Fact]
    public async Task Governance_RootKnowingAProofCannotBypassIndependentApprovals()
    {
        var flow=await Recovery(false);var proof=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        await Exec("UPDATE institution_invitation SET token_hash=@p0 WHERE institution_id=@p1 AND id=@p2",Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(proof))),flow.P.InstitutionId,flow.P.InvitationId!.Value);
        var body=new{flow.P.InstitutionId,EventId=Guid.NewGuid(),Challenge=proof};var path="/api/Auth/invitations/"+flow.P.InvitationId+"/accept";
        await Expect(await Http(path,await Ticket(1),body),HttpStatusCode.Forbidden);
        await Govern(Approval(flow.Request));await Expect(await Http(path,await Ticket(1),body),HttpStatusCode.Forbidden);
        Assert.Equal(0,await Count("SELECT COUNT(*) FROM institution_membership WHERE institution_id=@p0 AND user_id=@p1",flow.P.InstitutionId,users[1]));
    }
    [Fact]
    public async Task Governance_OperatorAliasesUsingTheSameKeyDoNotCountAsIndependentPeople()
    {
        var other=ConfigureGovernance();other.PublicKeyPem=Signer.ExportSubjectPublicKeyInfoPem();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Platform(Operation()));
    }
    [Fact]
    public async Task Governance_RecoveryRequestsAndApprovalsAreAppendOnly()
    {
        var flow=await Recovery();
        foreach(var sql in new[]{"UPDATE institution_recovery SET expires_at_utc=UTC_TIMESTAMP() WHERE institution_id=@p0","DELETE FROM institution_recovery_approval WHERE institution_id=@p0","UPDATE institution_recovery_approval SET operator_id='self-appointed' WHERE institution_id=@p0"})
        {
            var error=await Assert.ThrowsAsync<MySqlConnector.MySqlException>(()=>Exec(sql,flow.P.InstitutionId));Assert.Equal(1644,error.Number);
        }
    }

    [Fact]
    public async Task Governance_ConcurrentRecoveryRequestsAndApprovalsHaveOnePersistentEffect()
    {
        ConfigureGovernance();var a=await Active();await Govern(GovernanceOp(a.P.InstitutionId,PlatformAction.Suspend));
        var request=GovernanceOp(a.P.InstitutionId,PlatformAction.RequestRecovery,recovery:Guid.NewGuid());
        var requests=await Task.WhenAll(Govern(request),Govern(Fresh(request)));Assert.Equal(requests[0],requests[1]);
        var approvals=await Task.WhenAll(Govern(Approval(request)),Govern(OtherOperator(Approval(request))));
        Assert.Equal(1,await Count("SELECT COUNT(*) FROM institution_recovery WHERE institution_id=@p0",a.P.InstitutionId));
        Assert.Equal(2,await Count("SELECT COUNT(*) FROM institution_recovery_approval WHERE institution_id=@p0",a.P.InstitutionId));
        Assert.True(channel.Inbox.ContainsKey(request.Intent.RecoveryId));
        await Accept(requests[0],1);
    }

    [Fact]
    public async Task Governance_ArchivePreservesMembershipsAndRejectsOtherwiseValidHttpMutations()
    {
        ConfigureGovernance();var a=await AuditFixture();await Govern(GovernanceOp(a.Owner.InstitutionId,PlatformAction.Archive));
        await Expect(await Http($"/api/institution/admin/members/{a.Target}/mutations",a.AdminToken,new{EventId=Guid.NewGuid(),ExpectedRevision=2,Action="AddRole",Role="Student",Reason="Forbidden archived write"}),HttpStatusCode.Forbidden);
        Assert.Equal(2,await Count("SELECT COUNT(*) FROM institution_membership WHERE institution_id=@p0",a.Owner.InstitutionId));
        Assert.Equal(0,await Count("SELECT COUNT(*) FROM institution_membership_event WHERE institution_id=@p0",a.Owner.InstitutionId));
        Assert.Equal(0,await Count("SELECT COUNT(*) FROM institution_membership_role WHERE membership_id=@p0 AND role='Student'",a.Target));
    }

    [Fact]
    public async Task Governance_ActualCliUsesTheDedicatedManagerWithoutRequiringNotificationDelivery()
    {
        ConfigureGovernance();var a=await Active();var op=GovernanceOp(a.P.InstitutionId,PlatformAction.Suspend);
        var root=System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory,"../../../../../"));
        var folder=System.IO.Path.Combine(root,"migrations/mt4/private/mt5a-mamp-13306/governance-cli-"+Guid.NewGuid().ToString("N"));System.IO.Directory.CreateDirectory(folder);PrivateTestFiles.ProtectNewDirectory(folder);
        var json=new System.Text.Json.JsonSerializerOptions();json.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter<PlatformAction>());
        var configFile=System.IO.Path.Combine(folder,"admin.json");var opFile=System.IO.Path.Combine(folder,"signed.json");
        await System.IO.File.WriteAllTextAsync(configFile,System.Text.Json.JsonSerializer.Serialize(new {ConnectionStrings=new{DefaultConnection=connection},Multitenancy=new{Governance=new{Enabled=true}},PlatformAuthority=new{Enabled=true,Keys=new[]{key}}},json));
        await System.IO.File.WriteAllTextAsync(opFile,System.Text.Json.JsonSerializer.Serialize(op,json));
        var start=new System.Diagnostics.ProcessStartInfo("dotnet"){WorkingDirectory=root,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var arg in new[]{"run","--no-build","--project","tools/PlatformAdmin","--",configFile,opFile})start.ArgumentList.Add(arg);
        using var child=System.Diagnostics.Process.Start(start)!;var stdout=child.StandardOutput.ReadToEndAsync();var stderr=child.StandardError.ReadToEndAsync();await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        Assert.True(child.ExitCode==0,"Governance CLI failed; inspect private test evidence");
        Assert.Equal("suspended",System.Text.Json.JsonSerializer.Deserialize<PlatformResult>(await stdout)!.Status);
        await stderr;await Expect(await Http("/api/Auth/context",a.Token),HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Governance_RecoveryCanBeRevokedEvenWhenAnotherApproverHasBeenRevoked()
    {
        var flow=await Recovery();key.Revoked=true;
        var revoke=OtherOperator(Approval(flow.Request) with{Action=PlatformAction.RevokeRecovery,Intent=flow.Request.Intent with{Reason="Administrative withdrawal of the recovery case"}});
        await Govern(revoke);
        var delivery=channel.Inbox[flow.P.InvitationId!.Value];
        await Expect(await Http("/api/Auth/invitations/"+delivery.InvitationId+"/accept",await Ticket(1),new{flow.P.InstitutionId,EventId=Guid.NewGuid(),delivery.Challenge}),HttpStatusCode.Forbidden);
        Assert.Equal(1,await Count("SELECT COUNT(*) FROM institution_invitation WHERE institution_id=@p0 AND id=@p1 AND status='revoked'",flow.P.InstitutionId,flow.P.InvitationId.Value));
    }

    [Fact]
    public async Task Governance_RecoveryReissueInvalidatesOldProofAndPreservesIndependentApprovals()
    {
        var flow=await Recovery();var old=channel.Inbox[flow.P.InvitationId!.Value];
        var reissue=Sign(Approval(flow.Request) with{Action=PlatformAction.ReissueRecovery,Intent=flow.Request.Intent with{Reason="Recipient requested secure redelivery"}});
        await Govern(reissue);var next=channel.Inbox[flow.P.InvitationId.Value];Assert.NotEqual(old.Challenge,next.Challenge);
        await Expect(await Http("/api/Auth/invitations/"+old.InvitationId+"/accept",await Ticket(1),new{flow.P.InstitutionId,EventId=Guid.NewGuid(),old.Challenge}),HttpStatusCode.Forbidden);
        await Accept(flow.P,1);
        Assert.Equal(2,await Count("SELECT COUNT(*) FROM institution_recovery_approval WHERE institution_id=@p0",flow.P.InstitutionId));
    }

    [Theory][InlineData("approval")][InlineData("accept")]
    public async Task Governance_AuthorityRevokedDuringAwaitedWorkCannotCommitRecovery(string stage)
    {
        var flow=await Recovery(false);await Govern(Approval(flow.Request));
        var second=OtherOperator(Approval(flow.Request));
        if(stage=="accept")await Govern(second);
        fault.OnBeforeCommit=action=>{if(action==(stage=="accept"?"RecoveryAccept":"ApproveRecovery"))key.Revoked=true;};
        if(stage=="approval")
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Govern(second));
            Assert.Equal(1,await Count("SELECT COUNT(*) FROM institution_recovery_approval WHERE institution_id=@p0",flow.P.InstitutionId));
            Assert.Equal(0,await Count("SELECT COUNT(*) FROM institution_onboarding_event WHERE id=@p0",second.EventId));
        }
        else
        {
            var d=channel.Inbox[flow.P.InvitationId!.Value];
            await Expect(await Http("/api/Auth/invitations/"+d.InvitationId+"/accept",await Ticket(1),new{flow.P.InstitutionId,EventId=Guid.NewGuid(),d.Challenge}),HttpStatusCode.Forbidden);
            Assert.Equal(0,await Count("SELECT COUNT(*) FROM institution_onboarding_event WHERE institution_id=@p0 AND action='RecoveryAccepted'",flow.P.InstitutionId));
        }
        Assert.Equal(0,await Count("SELECT COUNT(*) FROM institution_membership WHERE institution_id=@p0 AND user_id=@p1",flow.P.InstitutionId,users[1]));
        Assert.Equal(1,await Count("SELECT COUNT(*) FROM institution WHERE id=@p0 AND status='suspended'",flow.P.InstitutionId));
    }
}
