using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

[Collection("Mt5aIdentityOnly")]
public sealed partial class Mt5cOnboardingTests : IAsyncLifetime
{
    private static readonly RSA Signer=RSA.Create(3072);
    private static readonly string Password="Synthetic-MT5C-only-password!";
    private readonly Guid[] users=Enumerable.Range(0,4).Select(_=>Guid.NewGuid()).ToArray();
    private readonly System.Collections.Generic.List<Guid> owners=[];
    private readonly Channel channel=new();
    private readonly Fault fault=new();
    private readonly PlatformPublicKey key=new(){OperatorId="operator-one",KeyId="key-one",PublicKeyPem=Signer.ExportSubjectPublicKeyInfoPem(),NotBeforeUtc=DateTime.UtcNow.AddDays(-1),NotAfterUtc=DateTime.UtcNow.AddDays(1),Actions=[PlatformAction.Provision,PlatformAction.ReissueFirstAdmin,PlatformAction.RevokeFirstAdmin]};
    private string connection=null!;
    private Mt3cHosting.ProgramHost host=null!;
    private string Email(int i)=>users[i].ToString("N")+"@mt5c.example.test";
    private sealed class Channel : IInvitationChannel
    {
        public bool Ready=true;
        public bool IsOperational=>Ready;
        public ConcurrentDictionary<Guid,InvitationDelivery> Inbox=new();
        public Task DeliverAsync(InvitationDelivery value,CancellationToken ct){Inbox[value.InvitationId]=value;return Task.CompletedTask;}
    }
    private sealed class Fault : IOnboardingFaultObserver
    {
        public string? Action;public bool Reached;
        public System.Action<string>? OnBeforeCommit;
        public Task BeforeCommitAsync(string action,CancellationToken ct){OnBeforeCommit?.Invoke(action);if(action==Action){Reached=true;throw new InvalidOperationException("Synthetic transaction fault");}return Task.CompletedTask;}
    }
    private async Task<MySqlConnection> Open()
    {
        var c=new MySqlConnection(connection);await c.OpenAsync();
        await using var cmd=new MySqlCommand("SELECT VERSION(),@@hostname,@@port,REPLACE(@@datadir,CHAR(92),'/'),DATABASE()",c);
        await using var r=await cmd.ExecuteReaderAsync();Assert.True(await r.ReadAsync());Assert.Equal("5.7.24",r.GetString(0));Assert.Equal(Environment.MachineName,r.GetString(1),true);Assert.Equal((int)TestMySqlTarget.Port,r.GetInt32(2));Assert.EndsWith(TestMySqlTarget.DataDirectorySuffix,r.GetString(3),StringComparison.OrdinalIgnoreCase);Assert.Equal("electronic_register_test",r.GetString(4));return c;
    }
    private async Task Exec(string sql,params object[] values)
    {await using var c=await Open();await using var cmd=new MySqlCommand(sql,c);for(int i=0;i<values.Length;i++)cmd.Parameters.AddWithValue("@p"+i,values[i]);await cmd.ExecuteNonQueryAsync();}
    private async Task<long> Count(string sql,params object[] values)
    {await using var c=await Open();await using var cmd=new MySqlCommand(sql,c);for(int i=0;i<values.Length;i++)cmd.Parameters.AddWithValue("@p"+i,values[i]);return Convert.ToInt64(await cmd.ExecuteScalarAsync());}
    public async Task InitializeAsync()
    {
        connection=new MySqlConnectionStringBuilder(IntegrationFixture.ValidateLocalConnectionString(Environment.GetEnvironmentVariable("TEST_MYSQL_CONNECTION")??throw new InvalidOperationException())){AllowUserVariables=true}.ConnectionString;
        var hash=BCrypt.Net.BCrypt.HashPassword(Password);
        for(int i=0;i<users.Length;i++)await Exec("INSERT INTO user(id,email,password_hash,role) VALUES(@p0,@p1,@p2,'admin')",users[i],Email(i),hash);
        host=await Mt3cHosting.ProgramHost.StartAsync(connection,Guid.NewGuid(),[],enabled:false,membershipAdmin:true,membershipWrite:true,onboarding:true,configureServices:s=>
        {s.Configure<PlatformAuthorityOptions>(o=>{o.Enabled=true;o.Keys=[key];});s.AddSingleton<IInvitationChannel>(channel);s.AddSingleton<IOnboardingFaultObserver>(fault);});
    }
    public async Task DisposeAsync()
    {
        if(host is not null)await host.DisposeAsync();
        if(connection is null)return;
        foreach(var owner in owners.Distinct())await Exec("SET @er_mt5b_fixture_owners=@p0; DELETE FROM institution_recovery_approval WHERE institution_id=@p0; DELETE FROM institution_recovery WHERE institution_id=@p0; DELETE FROM institution_provisioning WHERE institution_id=@p0; DELETE FROM platform_operation_nonce WHERE institution_id=@p0; DELETE FROM institution_onboarding_event WHERE institution_id=@p0; DELETE FROM institution_invitation WHERE institution_id=@p0; DELETE FROM institution_membership_event WHERE institution_id=@p0; DELETE r FROM institution_membership_role r JOIN institution_membership m ON m.id=r.membership_id WHERE m.institution_id=@p0; DELETE FROM institution_membership WHERE institution_id=@p0; DELETE FROM institution WHERE id=@p0; SET @er_mt5b_fixture_owners=NULL",owner);
        foreach(var user in users)await Exec("DELETE FROM user WHERE id=@p0",user);
    }
    private SignedPlatformOperation Sign(SignedPlatformOperation op)=>op with{Signature=Convert.ToBase64String(Signer.SignData(PlatformSigning.Bytes(op),HashAlgorithmName.SHA256,RSASignaturePadding.Pss))};
    private SignedPlatformOperation Operation(int recipient=0)
    {
        var owner=Guid.NewGuid();owners.Add(owner);var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return Sign(new("operator-one","key-one",Guid.NewGuid(),Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),now,now+240,PlatformAction.Provision,
            new(owner,users[recipient],"Synthetic verified ITS","MT5BP_"+owner.ToString("N"),"Synthetic declared legal identity","test-evidence:verified-authority","Synthetic owner approval"),""));
    }
    private async Task<PlatformResult> Platform(SignedPlatformOperation op)
    {await using var scope=host.Host.Services.CreateAsyncScope();return await scope.ServiceProvider.GetRequiredService<IOnboardingRepository>().PlatformAsync(op,CancellationToken.None);}
    private async Task<HttpResponseMessage> Http(string path,string? ticket=null,object? body=null)
    {
        var msg=new HttpRequestMessage(body is null?HttpMethod.Get:HttpMethod.Post,path);if(ticket is not null)msg.Headers.Authorization=new AuthenticationHeaderValue("Bearer",ticket);if(body is not null)msg.Content=JsonContent.Create(body);return await host.Client.SendAsync(msg);
    }
    private static async Task Expect(HttpResponseMessage response,HttpStatusCode status)=>Assert.True(response.StatusCode==status,$"Expected {status}, got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    private async Task<string> Ticket(int user=0)
    {using var r=await Http("/api/Auth/identity/login",body:new LoginDto{Email=Email(user),Password=Password});await Expect(r,HttpStatusCode.OK);return(await r.Content.ReadFromJsonAsync<IdentityTicketDto>())!.Ticket;}
    private async Task<string> Access(Guid owner,Guid member,int user=0)
    {using var r=await Http("/api/Auth/select-institution",await Ticket(user),new SelectInstitutionDto{InstitutionId=owner,MembershipId=member});await Expect(r,HttpStatusCode.OK);return(await r.Content.ReadFromJsonAsync<InstitutionAccessDto>())!.Token;}
    private async Task<InvitationResult> Accept(PlatformResult p,int user=0,Guid? eventId=null)
    {
        var d=channel.Inbox[p.InvitationId!.Value];using var r=await Http("/api/Auth/invitations/"+d.InvitationId+"/accept",await Ticket(user),new {p.InstitutionId,EventId=eventId??Guid.NewGuid(),d.Challenge});await Expect(r,HttpStatusCode.OK);return(await r.Content.ReadFromJsonAsync<InvitationResult>())!;
    }
    private async Task<(PlatformResult P,Guid Member,string Token)> Active(int user=0)
    {var p=await Platform(Operation(user));var a=await Accept(p,user);return(p,a.MembershipId!.Value,await Access(p.InstitutionId,a.MembershipId!.Value,user));}

    [Theory]
    [InlineData("signature")][InlineData("operator")][InlineData("key")][InlineData("expired")][InlineData("future")][InlineData("lifetime")][InlineData("revoked")][InlineData("intent")][InlineData("nonce")][InlineData("unregistered")]
    public async Task UntrustedOperatorsAndInvalidSignedOperationsNeverCreateAnInstitution(string failure)
    {
        var op=Operation();
        switch(failure)
        {
            case "signature":op=op with{Signature=Convert.ToBase64String(new byte[384])};break;
            case "operator":op=Sign(op with{OperatorId="global-admin"});break;
            case "key":op=Sign(op with{KeyId="self-appointed-key"});break;
            case "expired":op=Sign(op with{IssuedAt=op.IssuedAt-300,ExpiresAt=op.IssuedAt-1});break;
            case "future":op=Sign(op with{IssuedAt=op.IssuedAt+100,ExpiresAt=op.ExpiresAt+100});break;
            case "lifetime":op=Sign(op with{ExpiresAt=op.IssuedAt+600});break;
            case "revoked":key.Revoked=true;break;
            case "intent":op=op with{Intent=op.Intent with{Name="Tampered"}};break;
            case "nonce":op=Sign(op with{Nonce="not-high-entropy-canonical"});break;
            case "unregistered":key.PublicKeyPem=RSA.Create(3072).ExportSubjectPublicKeyInfoPem();break;
        }
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Platform(op));Assert.Equal(0,await Count("SELECT COUNT(*) FROM institution WHERE id=@p0",op.Intent.InstitutionId));
    }
    [Fact]
    public async Task ProvisioningDoesNotAssignRolesUntilPersonallyAcceptedAndAuditsDistinctActors()
    {
        var p=await Platform(Operation());Assert.Equal(0,await Count("SELECT COUNT(*) FROM institution_membership WHERE institution_id=@p0",p.InstitutionId));
        var accepted=await Accept(p);Assert.Equal(1,await Count("SELECT COUNT(*) FROM institution WHERE id=@p0 AND status='active'",p.InstitutionId));
        Assert.Equal(1,await Count("SELECT COUNT(*) FROM institution_membership_security_state WHERE institution_id=@p0 AND membership_id=@p1 AND security_generation=1",p.InstitutionId,accepted.MembershipId!.Value));
        Assert.Equal(1,await Count("SELECT COUNT(*) FROM institution_onboarding_event WHERE institution_id=@p0 AND actor_kind='Operator' AND operator_id='operator-one' AND actor_user_id IS NULL AND evidence_reference='test-evidence:verified-authority'",p.InstitutionId));
        Assert.Equal(1,await Count("SELECT COUNT(*) FROM institution_onboarding_event WHERE institution_id=@p0 AND actor_kind='Recipient' AND actor_user_id=@p1 AND actor_membership_id IS NULL",p.InstitutionId,users[0]));
        Assert.Equal(0,await Count("SELECT COUNT(*) FROM institution_membership_event WHERE institution_id=@p0",p.InstitutionId));
        await Expect(await Http("/api/Auth/context",await Access(p.InstitutionId,accepted.MembershipId.Value)),HttpStatusCode.OK);
    }
    [Fact]
    public async Task ExactNonceReplayIsDeniedButFreshlySignedIdempotentRetryRevalidatesAuthority()
    {
        var op=Operation();var first=await Platform(op);await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Platform(op));
        var retry=Sign(op with{Nonce=Convert.ToHexString(RandomNumberGenerator.GetBytes(32))});Assert.Equal(first,await Platform(retry));
        Assert.Equal(1,await Count("SELECT COUNT(*) FROM institution_onboarding_event WHERE institution_id=@p0",first.InstitutionId));
        Assert.Equal(2,await Count("SELECT COUNT(*) FROM platform_operation_nonce WHERE institution_id=@p0",first.InstitutionId));
        key.Revoked=true;await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Platform(Sign(retry with{Nonce=Convert.ToHexString(RandomNumberGenerator.GetBytes(32))})));
    }
    [Theory][InlineData("evidence")][InlineData("identity")]
    public async Task UnverifiedLegalRequestIsNotTreatedAsVerified(string missing)
    {var op=Operation();op=Sign(op with{Intent=missing=="evidence"?op.Intent with{EvidenceReference=""}:op.Intent with{LegalIdentity=""}});await Assert.ThrowsAsync<ArgumentException>(()=>Platform(op));Assert.Equal(0,await Count("SELECT COUNT(*) FROM institution WHERE id=@p0",op.Intent.InstitutionId));}
    [Fact]
    public async Task UnconfiguredTrustedChannelKeepsTheWorkflowClosed()
    {channel.Ready=false;var op=Operation();await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Platform(op));Assert.Equal(0,await Count("SELECT COUNT(*) FROM institution WHERE id=@p0",op.Intent.InstitutionId));}
    [Fact]
    public async Task RecipientMustAuthenticateAndPossessDeliveredProofForTheExactUserAndTenant()
    {
        var p=await Platform(Operation());var d=channel.Inbox[p.InvitationId!.Value];var body=new{p.InstitutionId,EventId=Guid.NewGuid(),d.Challenge};var path="/api/Auth/invitations/"+d.InvitationId+"/accept";
        await Expect(await Http(path,body:body),HttpStatusCode.Unauthorized);
        await Expect(await Http(path,await Ticket(1),body),HttpStatusCode.Forbidden);
        await Expect(await Http(path,await Ticket(),new{InstitutionId=Guid.NewGuid(),EventId=Guid.NewGuid(),d.Challenge}),HttpStatusCode.Forbidden);
        await Exec("UPDATE user SET email=@p0 WHERE id=@p1","changed@mt5c.example.test",users[0]);
        await Expect(await Http(path,await TicketAfterEmailChange(),body),HttpStatusCode.Forbidden);
        Assert.Equal(0,await Count("SELECT COUNT(*) FROM institution_membership WHERE institution_id=@p0",p.InstitutionId));
    }
    private async Task<string> TicketAfterEmailChange()
    {using var r=await Http("/api/Auth/identity/login",body:new LoginDto{Email="changed@mt5c.example.test",Password=Password});await Expect(r,HttpStatusCode.OK);return(await r.Content.ReadFromJsonAsync<IdentityTicketDto>())!.Ticket;}
    [Fact]
    public async Task ConcurrentAcceptanceAndRetryConsumeOnlyOnceAndKeepRolesApproved()
    {
        var p=await Platform(Operation());var d=channel.Inbox[p.InvitationId!.Value];var ticket=await Ticket();var body=new{p.InstitutionId,EventId=Guid.NewGuid(),d.Challenge};var path="/api/Auth/invitations/"+d.InvitationId+"/accept";
        var responses=await Task.WhenAll(Http(path,ticket,body),Http(path,ticket,body));foreach(var r in responses)await Expect(r,HttpStatusCode.OK);
        Assert.Equal(await responses[0].Content.ReadAsStringAsync(),await responses[1].Content.ReadAsStringAsync());
        await Expect(await Http(path,ticket,new{p.InstitutionId,EventId=Guid.NewGuid(),d.Challenge}),HttpStatusCode.Conflict);
        Assert.Equal(1,await Count("SELECT COUNT(*) FROM institution_membership WHERE institution_id=@p0",p.InstitutionId));
        Assert.Equal(1,await Count("SELECT COUNT(*) FROM institution_onboarding_event WHERE institution_id=@p0 AND action='InviteAccepted'",p.InstitutionId));
    }
    [Fact]
    public async Task InviteSharedUserWithoutChangingBAndReissueInvalidatesPreviousChallenge()
    {
        var a=await Active();var b=await Active(1);var bBefore=await Count("SELECT security_generation FROM institution_membership_security_state WHERE institution_id=@p0",b.P.InstitutionId);
        var eventId=Guid.NewGuid();var body=new{EventId=eventId,RecipientUserId=users[1],Roles=new[]{"Teacher"},Reason="Synthetic tenant-scoped invitation"};
        var r=await Http("/api/institution/admin/invitations",a.Token,body);await Expect(r,HttpStatusCode.OK);
        var delivery=channel.Inbox.Values.Single(x=>x.InstitutionId==a.P.InstitutionId && x.RecipientUserId==users[1]);
        await Expect(await Http("/api/institution/admin/invitations",a.Token,body),HttpStatusCode.OK);
        await Expect(await Http("/api/institution/admin/invitations/"+delivery.InvitationId+"/reissue",a.Token,new{EventId=Guid.NewGuid(),Reason="Rotate delivery challenge"}),HttpStatusCode.OK);
        var ticket=await Ticket(1);var path="/api/Auth/invitations/"+delivery.InvitationId+"/accept";
        await Expect(await Http(path,ticket,new{a.P.InstitutionId,EventId=Guid.NewGuid(),delivery.Challenge}),HttpStatusCode.Forbidden);
        var fresh=channel.Inbox[delivery.InvitationId];var accepted=await Http(path,ticket,new{a.P.InstitutionId,EventId=Guid.NewGuid(),fresh.Challenge});await Expect(accepted,HttpStatusCode.OK);
        var m=(await accepted.Content.ReadFromJsonAsync<InvitationResult>())!.MembershipId!.Value;
        var token=await Access(a.P.InstitutionId,m,1);
        await Expect(await Http("/api/institution/admin/invitations",token,new{EventId=Guid.NewGuid(),RecipientUserId=users[2],Roles=new[]{"InstitutionAdmin"},Reason="Teacher cannot elevate"}),HttpStatusCode.Forbidden);
        Assert.Equal(bBefore,await Count("SELECT security_generation FROM institution_membership_security_state WHERE institution_id=@p0",b.P.InstitutionId));
        await Expect(await Http("/api/Auth/context",b.Token),HttpStatusCode.OK);
    }
    [Fact]
    public async Task RevokedInvitationCannotBeAcceptedAndForeignAdminCannotRevokeIt()
    {
        var a=await Active();var b=await Active(1);
        await Expect(await Http("/api/institution/admin/invitations",a.Token,new{EventId=Guid.NewGuid(),RecipientUserId=users[2],Roles=new[]{"Student"},Reason="Scoped test"}),HttpStatusCode.OK);
        var d=channel.Inbox.Values.Single(x=>x.RecipientUserId==users[2]);var path="/api/institution/admin/invitations/"+d.InvitationId+"/revoke";
        await Expect(await Http(path,b.Token,new{EventId=Guid.NewGuid(),Reason="Foreign actor"}),HttpStatusCode.Forbidden);
        await Expect(await Http(path,a.Token,new{EventId=Guid.NewGuid(),Reason="Revoke scoped invitation"}),HttpStatusCode.OK);
        await Expect(await Http("/api/Auth/invitations/"+d.InvitationId+"/accept",await Ticket(2),new{a.P.InstitutionId,EventId=Guid.NewGuid(),d.Challenge}),HttpStatusCode.Forbidden);
    }
    [Theory][InlineData("Provision")][InlineData("Accept")][InlineData("Invite")]
    public async Task FaultAfterWritesRollsBackMembershipApprovalAuditAndNonce(string action)
    {
        if(action=="Provision") {var op=Operation();fault.Action=action;await Assert.ThrowsAsync<InvalidOperationException>(()=>Platform(op));Assert.Equal(0,await Count("SELECT COUNT(*) FROM institution WHERE id=@p0",op.Intent.InstitutionId));}
        else if(action=="Accept") {var p=await Platform(Operation());fault.Action=action;var d=channel.Inbox[p.InvitationId!.Value];await Expect(await Http("/api/Auth/invitations/"+d.InvitationId+"/accept",await Ticket(),new{p.InstitutionId,EventId=Guid.NewGuid(),d.Challenge}),HttpStatusCode.InternalServerError);Assert.Equal(0,await Count("SELECT COUNT(*) FROM institution_membership WHERE institution_id=@p0",p.InstitutionId));}
        else {var a=await Active();fault.Action=action;await Expect(await Http("/api/institution/admin/invitations",a.Token,new{EventId=Guid.NewGuid(),RecipientUserId=users[1],Roles=new[]{"Teacher"},Reason="Fault injection"}),HttpStatusCode.InternalServerError);Assert.Equal(1,await Count("SELECT COUNT(*) FROM institution_invitation WHERE institution_id=@p0",a.P.InstitutionId));}
        Assert.True(fault.Reached);
    }
    [Fact]
    public async Task ConcurrentProvisioningIsIdempotentAndEventIntentChangesConflict()
    {
        var op=Operation();var other=Sign(op with{Nonce=Convert.ToHexString(RandomNumberGenerator.GetBytes(32))});
        var results=await Task.WhenAll(Platform(op),Platform(other));Assert.Equal(results[0],results[1]);
        await Assert.ThrowsAsync<ElectronicRegisterAPI.Domain.Exceptions.MembershipConflictException>(()=>Platform(Sign(op with{Nonce=Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),Intent=op.Intent with{EvidenceReference="different-evidence"}})));
        Assert.Equal(1,await Count("SELECT COUNT(*) FROM institution_provisioning WHERE institution_id=@p0",op.Intent.InstitutionId));
    }
    [Fact]
    public async Task WrongChallengesArePersistentlyLimitedAndSignedReissueRotatesTheProof()
    {
        var op=Operation();var p=await Platform(op);var old=channel.Inbox[p.InvitationId!.Value];var ticket=await Ticket();var path="/api/Auth/invitations/"+old.InvitationId+"/accept";
        for(int i=0;i<5;i++)await Expect(await Http(path,ticket,new{p.InstitutionId,EventId=Guid.NewGuid(),Challenge=new string('A',64)}),HttpStatusCode.Forbidden);
        Assert.Equal(5,await Count("SELECT attempts FROM institution_invitation WHERE institution_id=@p0 AND id=@p1",p.InstitutionId,old.InvitationId));
        await Expect(await Http(path,ticket,new{p.InstitutionId,EventId=Guid.NewGuid(),old.Challenge}),HttpStatusCode.Forbidden);
        await Platform(Sign(op with{Action=PlatformAction.ReissueFirstAdmin,EventId=Guid.NewGuid(),Nonce=Convert.ToHexString(RandomNumberGenerator.GetBytes(32))}));
        Assert.NotEqual(old.Challenge,channel.Inbox[old.InvitationId].Challenge);
        await Expect(await Http(path,ticket,new{p.InstitutionId,EventId=Guid.NewGuid(),old.Challenge}),HttpStatusCode.Forbidden);
        await Accept(p);
        Assert.Equal(0,await Count("SELECT COUNT(*) FROM institution_onboarding_event WHERE institution_id=@p0 AND LOCATE(@p1,result_json)>0",p.InstitutionId,old.Challenge));
    }
    [Theory][InlineData("pending")][InlineData("left")]
    public async Task ExistingPendingOrLeftMembershipIsNeverImplicitlyRejoined(string status)
    {
        var a=await Active();await Expect(await Http("/api/institution/admin/invitations",a.Token,new{EventId=Guid.NewGuid(),RecipientUserId=users[1],Roles=new[]{"Teacher"},Reason="Synthetic invitation"}),HttpStatusCode.OK);
        var d=channel.Inbox.Values.Single(x=>x.RecipientUserId==users[1]);var member=Guid.NewGuid();await Exec("INSERT INTO institution_membership(id,institution_id,user_id,status) VALUES(@p0,@p1,@p2,@p3)",member,a.P.InstitutionId,users[1],status);
        await Expect(await Http("/api/Auth/invitations/"+d.InvitationId+"/accept",await Ticket(1),new{a.P.InstitutionId,EventId=Guid.NewGuid(),d.Challenge}),HttpStatusCode.Conflict);
        Assert.Equal(0,await Count("SELECT COUNT(*) FROM institution_membership_role WHERE membership_id=@p0",member));
        Assert.Equal(0,await Count("SELECT security_generation FROM institution_membership_security_state WHERE membership_id=@p0",member));
    }
    [Fact]
    public async Task UnknownRecipientAndExistingMemberHaveNeutralResponsesAndPersistentRateLimits()
    {
        var a=await Active();
        for(int i=0;i<5;i++)await Expect(await Http("/api/institution/admin/invitations",a.Token,new{EventId=Guid.NewGuid(),RecipientUserId=i==0?users[0]:Guid.NewGuid(),Roles=new[]{"Teacher"},Reason="Neutral synthetic request"}),i==0?HttpStatusCode.BadRequest:HttpStatusCode.OK);
        await Expect(await Http("/api/institution/admin/invitations",a.Token,new{EventId=Guid.NewGuid(),RecipientUserId=Guid.NewGuid(),Roles=new[]{"Teacher"},Reason="Fifth neutral request"}),HttpStatusCode.OK);
        await Expect(await Http("/api/institution/admin/invitations",a.Token,new{EventId=Guid.NewGuid(),RecipientUserId=Guid.NewGuid(),Roles=new[]{"Teacher"},Reason="Rate limited request"}),HttpStatusCode.Conflict);
    }
    [Fact]
    public async Task ExpiredChallengeAndUnapprovedFirstAdminCannotCreateMemberships()
    {
        var owner=Guid.NewGuid();owners.Add(owner);var invite=Guid.NewGuid();var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        await Exec("INSERT INTO institution(id,code,name,status) VALUES(@p0,@p1,'Unapproved synthetic foundation','provisioning')",owner,"MT5BP_"+owner.ToString("N"));
        await Exec("INSERT INTO institution_invitation(institution_id,id,recipient_user_id,kind,roles,status,token_hash,channel_hash,expires_at_utc,created_at_utc) VALUES(@p0,@p1,@p2,'FirstAdmin',JSON_ARRAY('InstitutionAdmin'),'pending',@p3,@p4,UTC_TIMESTAMP()+INTERVAL 15 MINUTE,UTC_TIMESTAMP())",owner,invite,users[0],Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))),Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Email(0)))));
        await Expect(await Http("/api/Auth/invitations/"+invite+"/accept",await Ticket(),new{InstitutionId=owner,EventId=Guid.NewGuid(),Challenge=token}),HttpStatusCode.Forbidden);
        var expired=Guid.NewGuid();await Exec("INSERT INTO institution_invitation(institution_id,id,recipient_user_id,kind,roles,status,token_hash,channel_hash,expires_at_utc,created_at_utc) VALUES(@p0,@p1,@p2,'Membership',JSON_ARRAY('Teacher'),'pending',@p3,@p4,UTC_TIMESTAMP()-INTERVAL 15 MINUTE,UTC_TIMESTAMP()-INTERVAL 30 MINUTE)",owner,expired,users[0],Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))),Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Email(0)))));
        await Expect(await Http("/api/Auth/invitations/"+expired+"/accept",await Ticket(),new{InstitutionId=owner,EventId=Guid.NewGuid(),Challenge=token}),HttpStatusCode.Forbidden);
        Assert.Equal(0,await Count("SELECT COUNT(*) FROM institution_membership WHERE institution_id=@p0",owner));
    }
    [Fact]
    public async Task DefaultClosedRolloutRejectsOtherwiseValidRecipientAndAdminRequests()
    {
        var a=await Active();var op=Operation(1);var p=await Platform(op);
        host.Host.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()["Multitenancy:Onboarding:Enabled"]="false";
        var d=channel.Inbox[p.InvitationId!.Value];
        await Expect(await Http("/api/Auth/invitations/"+d.InvitationId+"/accept",await Ticket(1),new{p.InstitutionId,EventId=Guid.NewGuid(),d.Challenge}),HttpStatusCode.Forbidden);
        await Expect(await Http("/api/institution/admin/invitations",a.Token,new{EventId=Guid.NewGuid(),RecipientUserId=users[2],Roles=new[]{"Teacher"},Reason="Closed rollout"}),HttpStatusCode.Forbidden);
    }
    [Fact]
    public async Task LegacyTechnicalWritersCannotPreseedAdministratorsIntoVerifiedOnboarding()
    {
        var p=await Platform(Operation());await using var scope=host.Host.Services.CreateAsyncScope();
        var repo=scope.ServiceProvider.GetRequiredService<ElectronicRegisterAPI.Domain.Interfaces.Repositories.IInstitutionMembershipRepository>();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>repo.AddAsync(new(){Id=Guid.NewGuid(),InstitutionId=p.InstitutionId,UserId=users[1],Status=ElectronicRegisterAPI.Domain.Enums.MembershipStatus.Active,Roles=[ElectronicRegisterAPI.Domain.Enums.InstitutionRole.InstitutionAdmin]}));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>repo.AddRoleAsync(p.InstitutionId,Guid.NewGuid(),ElectronicRegisterAPI.Domain.Enums.InstitutionRole.InstitutionAdmin));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>repo.SuspendAsync(p.InstitutionId,Guid.NewGuid(),DateTime.UtcNow));
        Assert.Equal(0,await Count("SELECT COUNT(*) FROM institution_membership WHERE institution_id=@p0",p.InstitutionId));
    }
    [Fact]
    public async Task ActualAdministrativeCliVerifiesSignedIntentAndUsesOnlyOwnerInstalledChannel()
    {
        var root=System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory,"../../../../../"));
        var folder=System.IO.Path.Combine(root,"migrations/mt4/private/mt5a-mamp-13306/cli-test-"+Guid.NewGuid().ToString("N"));System.IO.Directory.CreateDirectory(folder);PrivateTestFiles.ProtectNewDirectory(folder);
        var op=Operation();var json=new System.Text.Json.JsonSerializerOptions();json.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter<PlatformAction>());
        var configFile=System.IO.Path.Combine(folder,"admin.json");var operationFile=System.IO.Path.Combine(folder,"signed.json");
        var assembly=typeof(PlatformCliTestChannel).Assembly.Location;
        await System.IO.File.WriteAllTextAsync(configFile,System.Text.Json.JsonSerializer.Serialize(new {
            ConnectionStrings=new{DefaultConnection=connection},TestDatabase=new{Port=TestMySqlTarget.Port,ExpectedDataDir=TestMySqlTarget.DataDirectorySuffix},Multitenancy=new{Onboarding=new{Enabled=true}},
            PlatformAuthority=new{Enabled=true,Keys=new[]{key}},TestInbox=folder,
            InvitationChannel=new{AssemblyPath=assembly,SHA256=Convert.ToHexString(SHA256.HashData(await System.IO.File.ReadAllBytesAsync(assembly))),Type=typeof(PlatformCliTestChannel).FullName}},json));
        await System.IO.File.WriteAllTextAsync(operationFile,System.Text.Json.JsonSerializer.Serialize(op,json));
        var process=new System.Diagnostics.ProcessStartInfo("dotnet"){WorkingDirectory=root,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var arg in new[]{"run","--no-build","--project","tools/PlatformAdmin","--",configFile,operationFile})process.ArgumentList.Add(arg);
        using var child=System.Diagnostics.Process.Start(process)!;var stdout=child.StandardOutput.ReadToEndAsync();var stderr=child.StandardError.ReadToEndAsync();await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        Assert.True(child.ExitCode==0,"CLI refused the test operator; inspect private test configuration");
        var result=System.Text.Json.JsonSerializer.Deserialize<PlatformResult>(await stdout)!;
        var delivery=System.Text.Json.JsonSerializer.Deserialize<InvitationDelivery>(await System.IO.File.ReadAllTextAsync(System.IO.Path.Combine(folder,result.InvitationId+".json")))!;
        await Expect(await Http("/api/Auth/invitations/"+delivery.InvitationId+"/accept",await Ticket(),new{result.InstitutionId,EventId=Guid.NewGuid(),delivery.Challenge}),HttpStatusCode.OK);
        Assert.DoesNotContain(delivery.Challenge,await stderr);
    }
}
