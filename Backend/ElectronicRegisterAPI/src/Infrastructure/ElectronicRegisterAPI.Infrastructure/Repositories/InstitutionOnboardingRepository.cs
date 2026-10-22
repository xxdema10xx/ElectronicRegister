using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal sealed partial class InstitutionOnboardingRepository(ElectronicRegisterContext db, IPlatformAuthority authority,
    IRecipientIdentity identities, IInvitationChannel channel, IMembershipAuthorizationRepository authorization,
    IOnboardingFaultObserver faults, IConfiguration config) : IOnboardingRepository
{
    private sealed record Invite(Guid Id,Guid User,string Kind,string[] Roles,string Status,string TokenHash,string ChannelHash,DateTime Expires,int Attempts,Guid? Membership);
    private sealed record Event(string Hash,string Result);
    private static Guid ReadGuid(DbDataReader reader,int ordinal)=>reader.GetValue(ordinal) is Guid id?id:Guid.Parse(reader.GetString(ordinal));
    private static string Hash(string input)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
    private static string Intent(object value)=>Hash(JsonSerializer.Serialize(value));
    private static string Text(string? value,int max) => !string.IsNullOrWhiteSpace(value) && value.Length<=max && !value.Any(char.IsControl) ? value : throw new ArgumentException("Input non valido.");
    private static void Id(Guid id) { if(id==Guid.Empty)throw new ArgumentException("Identificativo richiesto."); }
    private void Enabled(bool delivery=true)
    {
        if(!bool.TryParse(config["Multitenancy:Onboarding:Enabled"],out var on) || !on || delivery && !channel.IsOperational)
            throw new UnauthorizedAccessException("Onboarding non operativo.");
    }
    private async Task<DbCommand> Command(string sql,CancellationToken ct,params object?[] values)
    {
        await db.Database.OpenConnectionAsync(ct);
        var cmd=db.Database.GetDbConnection().CreateCommand();cmd.CommandText=sql;cmd.Transaction=db.Database.CurrentTransaction?.GetDbTransaction();
        cmd.CommandTimeout=30;
        for(int i=0;i<values.Length;i++) {var p=cmd.CreateParameter();p.ParameterName="@p"+i;p.Value=values[i] is Guid g?g.ToString("D"):values[i]??DBNull.Value;cmd.Parameters.Add(p);}
        return cmd;
    }
    private async Task<int> Execute(string sql,CancellationToken ct,params object?[] args)
    {await using var c=await Command(sql,ct,args);return await c.ExecuteNonQueryAsync(ct);}
    private async Task<string?> Scalar(string sql,CancellationToken ct,params object?[] args)
    {await using var c=await Command(sql,ct,args);var v=await c.ExecuteScalarAsync(ct);return v is null or DBNull?null:Convert.ToString(v,System.Globalization.CultureInfo.InvariantCulture);}
    private async Task<Event?> Prior(Guid owner,Guid id,string hash,CancellationToken ct)
    {
        await using var cmd=await Command("SELECT intent_hash,result_json,institution_id FROM institution_onboarding_event WHERE id=@p0",ct,id);
        await using var r=await cmd.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))return null;
        if(r.GetString(0)!=hash || ReadGuid(r,2)!=owner)throw new MembershipConflictException();
        return new(r.GetString(0),r.GetString(1));
    }
    private Task Mutex(Guid owner,CancellationToken ct)=>Execute("SELECT id FROM institution WHERE id=@p0 FOR UPDATE",ct,owner);
    private async Task RequireAdmin(TenantContext actor,CancellationToken ct)
    {
        var a=await authorization.GetAsync(actor.UserId,actor.InstitutionId,actor.MembershipId,ct);
        if(a is null || a.Status!=MembershipStatus.Active || a.InstitutionStatus!=InstitutionStatus.Active || a.SecurityGeneration is null ||
            a.SecurityGeneration!=actor.SecurityGeneration || !actor.Roles.SetEquals(a.Roles) || !a.Roles.Contains(InstitutionRole.InstitutionAdmin))throw new UnauthorizedAccessException();
    }
    private async Task<string> Address(Guid user,CancellationToken ct) =>
        await Scalar("SELECT email FROM user WHERE id=@p0",ct,user) is string address && !string.IsNullOrWhiteSpace(address) ? address : throw new UnauthorizedAccessException("Destinatario non disponibile.");
    private async Task<Invite> Load(Guid owner,Guid id,CancellationToken ct)
    {
        await using var cmd=await Command("SELECT id,recipient_user_id,kind,roles,status,token_hash,channel_hash,expires_at_utc,attempts,consumed_membership_id FROM institution_invitation WHERE institution_id=@p0 AND id=@p1 FOR UPDATE",ct,owner,id);
        await using var r=await cmd.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))throw new UnauthorizedAccessException("Invito non disponibile.");
        return new(ReadGuid(r,0),ReadGuid(r,1),r.GetString(2),JsonSerializer.Deserialize<string[]>(r.GetString(3))!,r.GetString(4),r.GetString(5),r.GetString(6),DateTime.SpecifyKind(r.GetDateTime(7),DateTimeKind.Utc),r.GetInt32(8),r.IsDBNull(9)?null:ReadGuid(r,9));
    }
    private async Task Rate(Guid owner,Guid actor,CancellationToken ct)
    {
        var count=await Scalar("SELECT COUNT(*) FROM institution_onboarding_event WHERE institution_id=@p0 AND actor_user_id=@p1 AND action IN ('InviteRequested','InviteRevoked','InviteReissued') AND occurred_at_utc>UTC_TIMESTAMP()-INTERVAL 1 MINUTE",ct,owner,actor);
        if(int.Parse(count!)>=5)throw new MembershipConflictException();
    }
    private Task Append(Guid owner,Guid id,string kind,string? op,string? key,Guid? user,Guid? member,string action,Guid? invite,string hash,string reason,string? evidence,object result,CancellationToken ct)=>
        Execute("INSERT INTO institution_onboarding_event(institution_id,id,actor_kind,operator_id,key_id,actor_user_id,actor_membership_id,action,invitation_id,intent_hash,reason,evidence_reference,result_json,occurred_at_utc) VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,@p12,UTC_TIMESTAMP(6))",ct,owner,id,kind,op,key,user,member,action,invite,hash,reason,evidence,JsonSerializer.Serialize(result));
    private async Task<Guid> CreateInvite(Guid owner,Guid user,string kind,string[] roles,CancellationToken ct,Guid? requestedId=null)
    {
        var address=await Address(user,ct);var invite=requestedId??Guid.NewGuid();var challenge=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));var expiry=DateTime.UtcNow.AddMinutes(15);
        await Execute("INSERT INTO institution_invitation(institution_id,id,recipient_user_id,kind,roles,status,token_hash,channel_hash,expires_at_utc,created_at_utc) VALUES(@p0,@p1,@p2,@p3,@p4,'pending',@p5,@p6,@p7,UTC_TIMESTAMP(6))",ct,owner,invite,user,kind,JsonSerializer.Serialize(roles),Hash(challenge),Hash(address),expiry);
        // A notification sent before commit is unusable if the transaction fails.
        // No plaintext token/outbox is persisted; a trusted reissue rotates it.
        await channel.DeliverAsync(new(owner,invite,user,address,challenge,expiry),ct);
        return invite;
    }
    public async Task<PlatformResult> PlatformAsync(SignedPlatformOperation op,CancellationToken ct)
    {
        Enabled();authority.Verify(op);var p=op.Intent;Id(p.InstitutionId);Id(p.RecipientUserId);Text(p.Reason,500);Text(p.EvidenceReference,200);
        if(op.Action is not (PlatformAction.Provision or PlatformAction.ReissueFirstAdmin or PlatformAction.RevokeFirstAdmin))throw new ArgumentException("Azione non disponibile in questa fase.");
        if(op.Action==PlatformAction.Provision){Text(p.Name,200);Text(p.LegalIdentity,200);if(!Regex.IsMatch(p.Code??"","^[a-zA-Z0-9_-]{1,64}$"))throw new ArgumentException();}
        string hash=Intent(new {op.OperatorId,op.EventId,op.Action,op.Intent});
        // New institutions cannot yet provide a row mutex. Connection-scoped lock
        // serializes competing creation intents without introducing a global lock.
        var lockName="er.provision."+p.InstitutionId.ToString("N");
        if(await Scalar("SELECT GET_LOCK(@p0,5)",ct,lockName)!="1")throw new MembershipConflictException();
        try
        {
            await using var tx=await db.Database.BeginTransactionAsync(ct);await Mutex(p.InstitutionId,ct);authority.Verify(op);
            if(await Scalar("SELECT COUNT(*) FROM platform_operation_nonce WHERE operator_id=@p0 AND nonce=@p1",ct,op.OperatorId,op.Nonce)!="0")throw new UnauthorizedAccessException("Replay non consentito.");
            var prior=await Prior(p.InstitutionId,op.EventId,hash,ct);PlatformResult result;
            if(prior is not null)result=JsonSerializer.Deserialize<PlatformResult>(prior.Result)!;
            else if(op.Action==PlatformAction.Provision)
            {
                if(await Scalar("SELECT COUNT(*) FROM institution WHERE id=@p0",ct,p.InstitutionId)!="0")throw new MembershipConflictException();
                await Address(p.RecipientUserId,ct);
                await Execute("INSERT INTO institution(id,name,code,status,legal_tax_code) VALUES(@p0,@p1,@p2,'provisioning',@p3)",ct,p.InstitutionId,p.Name,p.Code,p.LegalIdentity.Length<=32?p.LegalIdentity:null);
                var invite=await CreateInvite(p.InstitutionId,p.RecipientUserId,"FirstAdmin",["InstitutionAdmin"],ct);
                result=new(p.InstitutionId,invite,"provisioning");
                await Append(p.InstitutionId,op.EventId,"Operator",op.OperatorId,op.KeyId,null,null,"Provision",invite,hash,p.Reason,p.EvidenceReference,result,ct);
                await Execute("INSERT INTO institution_provisioning(institution_id,approval_event_id,first_invitation_id,legal_identity,evidence_reference) VALUES(@p0,@p1,@p2,@p3,@p4)",ct,p.InstitutionId,op.EventId,invite,p.LegalIdentity,p.EvidenceReference);
            }
            else
            {
                if(await Scalar("SELECT status FROM institution WHERE id=@p0",ct,p.InstitutionId)!="provisioning")throw new UnauthorizedAccessException();
                var id=Guid.Parse(await Scalar("SELECT first_invitation_id FROM institution_provisioning WHERE institution_id=@p0",ct,p.InstitutionId)??throw new UnauthorizedAccessException());
                var invite=await Load(p.InstitutionId,id,ct);
                if(invite.Status!="pending" || invite.User!=p.RecipientUserId)throw new UnauthorizedAccessException();
                if(op.Action==PlatformAction.RevokeFirstAdmin)await Execute("UPDATE institution_invitation SET status='revoked' WHERE institution_id=@p0 AND id=@p1",ct,p.InstitutionId,id);
                else
                {
                    var address=await Address(invite.User,ct);var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));var expires=DateTime.UtcNow.AddMinutes(15);
                    await Execute("UPDATE institution_invitation SET token_hash=@p0,channel_hash=@p1,expires_at_utc=@p2,attempts=0 WHERE institution_id=@p3 AND id=@p4",ct,Hash(token),Hash(address),expires,p.InstitutionId,id);
                    await channel.DeliverAsync(new(p.InstitutionId,id,invite.User,address,token,expires),ct);
                }
                result=new(p.InstitutionId,id,"provisioning");
                await Append(p.InstitutionId,op.EventId,"Operator",op.OperatorId,op.KeyId,null,null,op.Action.ToString(),id,hash,p.Reason,p.EvidenceReference,result,ct);
            }
            await Execute("INSERT INTO platform_operation_nonce(operator_id,nonce,key_id,institution_id,event_id,expires_at_utc) VALUES(@p0,@p1,@p2,@p3,@p4,@p5)",ct,op.OperatorId,op.Nonce,op.KeyId,p.InstitutionId,op.EventId,DateTimeOffset.FromUnixTimeSeconds(op.ExpiresAt).UtcDateTime);
            authority.Verify(op);Enabled();await faults.BeforeCommitAsync("Provision",ct);await tx.CommitAsync(ct);return result;
        }
        finally {await Scalar("SELECT RELEASE_LOCK(@p0)",CancellationToken.None,lockName);}
    }
    public async Task<InvitationResult> InviteAsync(TenantContext actor,InviteMember request,CancellationToken ct)
    {
        Enabled();Id(request.EventId);Id(request.RecipientUserId);Text(request.Reason,500);
        if(request.RecipientUserId==actor.UserId || request.Roles is null || request.Roles.Length is <1 or >3 || request.Roles.Any(r=>!Enum.IsDefined(r)) || request.Roles.Distinct().Count()!=request.Roles.Length)throw new ArgumentException();
        var roles=request.Roles.Select(x=>x.ToString()).Order(StringComparer.Ordinal).ToArray();
        var hash=Intent(new{actor.InstitutionId,actor.UserId,actor.MembershipId,request.EventId,request.RecipientUserId,Roles=roles,request.Reason});
        await using var tx=await db.Database.BeginTransactionAsync(ct);await Mutex(actor.InstitutionId,ct);await RequireAdmin(actor,ct);
        var old=await Prior(actor.InstitutionId,request.EventId,hash,ct);if(old is not null){await tx.CommitAsync(ct);return JsonSerializer.Deserialize<InvitationResult>(old.Result)!;}
        await Rate(actor.InstitutionId,actor.UserId,ct);
        Guid? invite=null;
        // Neutral response for absent recipients or existing members. No automatic
        // identity lookup/link based on an email address and no implicit rejoin.
        if(await Scalar("SELECT COUNT(*) FROM user WHERE id=@p0",ct,request.RecipientUserId)=="1" &&
           await Scalar("SELECT COUNT(*) FROM institution_membership WHERE institution_id=@p0 AND user_id=@p1",ct,actor.InstitutionId,request.RecipientUserId)=="0")
            invite=await CreateInvite(actor.InstitutionId,request.RecipientUserId,"Membership",roles,ct,request.EventId);
        var result=new InvitationResult(request.EventId,"accepted");
        await Append(actor.InstitutionId,request.EventId,"Member",null,null,actor.UserId,actor.MembershipId,"InviteRequested",invite,hash,request.Reason,null,result,ct);
        await RequireAdmin(actor,ct);await faults.BeforeCommitAsync("Invite",ct);await tx.CommitAsync(ct);return result;
    }
    public async Task<InvitationResult> ChangeAsync(TenantContext actor,ChangeInvitation request,bool revoke,CancellationToken ct)
    {
        Enabled(!revoke);Id(request.EventId);Id(request.InvitationId);Text(request.Reason,500);
        var action=revoke?"InviteRevoked":"InviteReissued";
        var hash=Intent(new{actor.InstitutionId,actor.UserId,actor.MembershipId,request,action});
        await using var tx=await db.Database.BeginTransactionAsync(ct);await Mutex(actor.InstitutionId,ct);await RequireAdmin(actor,ct);
        var old=await Prior(actor.InstitutionId,request.EventId,hash,ct);if(old is not null){await tx.CommitAsync(ct);return JsonSerializer.Deserialize<InvitationResult>(old.Result)!;}
        var invite=await Load(actor.InstitutionId,request.InvitationId,ct);if(invite.Kind!="Membership" || invite.User==actor.UserId || invite.Status!="pending")throw new MembershipConflictException();
        await Rate(actor.InstitutionId,actor.UserId,ct);
        if(revoke)await Execute("UPDATE institution_invitation SET status='revoked' WHERE institution_id=@p0 AND id=@p1",ct,actor.InstitutionId,invite.Id);
        else
        {
            var address=await Address(invite.User,ct);var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));var expires=DateTime.UtcNow.AddMinutes(15);
            await Execute("UPDATE institution_invitation SET token_hash=@p0,channel_hash=@p1,expires_at_utc=@p2,attempts=0 WHERE institution_id=@p3 AND id=@p4",ct,Hash(token),Hash(address),expires,actor.InstitutionId,invite.Id);
            await channel.DeliverAsync(new(actor.InstitutionId,invite.Id,invite.User,address,token,expires),ct);
        }
        var result=new InvitationResult(request.EventId,"accepted");
        await Append(actor.InstitutionId,request.EventId,"Member",null,null,actor.UserId,actor.MembershipId,action,invite.Id,hash,request.Reason,null,result,ct);
        await RequireAdmin(actor,ct);await faults.BeforeCommitAsync(action,ct);await tx.CommitAsync(ct);return result;
    }
    public async Task<InvitationResult> AcceptAsync(string ticket,AcceptInvitation request,CancellationToken ct)
    {
        Enabled();Id(request.InstitutionId);Id(request.InvitationId);Id(request.EventId);
        var user=identities.VerifySelectionTicket(ticket);
        var challenge=request.Challenge??throw new UnauthorizedAccessException();
        if(!Regex.IsMatch(challenge,"^[A-F0-9]{64}$"))throw new UnauthorizedAccessException();
        var hash=Intent(new{user,request.InstitutionId,request.InvitationId,request.EventId,Proof=Hash(challenge)});
        await using var tx=await db.Database.BeginTransactionAsync(ct);await Mutex(request.InstitutionId,ct);
        if(identities.VerifySelectionTicket(ticket)!=user)throw new UnauthorizedAccessException();
        var invite=await Load(request.InstitutionId,request.InvitationId,ct);var address=await Address(user,ct);
        if(invite.User!=user || invite.ChannelHash!=Hash(address) || invite.Status=="revoked" || invite.Expires<=DateTime.UtcNow || invite.Attempts>=5)throw new UnauthorizedAccessException();
        if(!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(invite.TokenHash),SHA256.HashData(Encoding.UTF8.GetBytes(challenge))))
        {
            await Execute("UPDATE institution_invitation SET attempts=attempts+1 WHERE institution_id=@p0 AND id=@p1 AND status='pending'",ct,request.InstitutionId,invite.Id);
            await tx.CommitAsync(ct);throw new UnauthorizedAccessException();
        }
        if(invite.Kind=="Recovery")await AuthorizeRecoveryAcceptance(request.InstitutionId,invite,ct);
        var old=await Prior(request.InstitutionId,request.EventId,hash,ct);if(old is not null){await tx.CommitAsync(ct);return JsonSerializer.Deserialize<InvitationResult>(old.Result)!;}
        if(invite.Status!="pending")throw new MembershipConflictException();
        var status=await Scalar("SELECT status FROM institution WHERE id=@p0",ct,request.InstitutionId);
        if(invite.Kind=="FirstAdmin")
        {
            if(status!="provisioning" || await Scalar("SELECT COUNT(*) FROM institution_provisioning p JOIN institution_onboarding_event e ON e.institution_id=p.institution_id AND e.id=p.approval_event_id WHERE p.institution_id=@p0 AND p.first_invitation_id=@p1 AND e.actor_kind='Operator' AND e.action='Provision'",ct,request.InstitutionId,invite.Id)!="1")throw new UnauthorizedAccessException();
        }
        else if(invite.Kind!="Recovery" && status!="active")throw new UnauthorizedAccessException();
        if(await Scalar("SELECT COUNT(*) FROM institution_membership WHERE institution_id=@p0 AND user_id=@p1",ct,request.InstitutionId,user)!="0")throw new MembershipConflictException();
        var membership=Guid.NewGuid();
        await Execute("INSERT INTO institution_membership(id,institution_id,user_id,status,joined_at) VALUES(@p0,@p1,@p2,'active',UTC_TIMESTAMP(6))",ct,membership,request.InstitutionId,user);
        foreach(var role in invite.Roles)await Execute("INSERT INTO institution_membership_role(membership_id,role) VALUES(@p0,@p1)",ct,membership,role);
        await Execute("UPDATE institution_invitation SET status='consumed',consumed_membership_id=@p0 WHERE institution_id=@p1 AND id=@p2",ct,membership,request.InstitutionId,invite.Id);
        Guid[] suspendedAdmins=[];
        if(invite.Kind=="Recovery")suspendedAdmins=await SuspendPreviousAdministrators(request.InstitutionId,membership,ct);
        if(invite.Kind is "FirstAdmin" or "Recovery")
        {
            if(await Scalar("SELECT COUNT(*) FROM institution_membership m JOIN institution_membership_role r ON r.membership_id=m.id AND r.role='InstitutionAdmin' JOIN institution_membership_security_state s ON s.institution_id=m.institution_id AND s.membership_id=m.id JOIN user u ON u.id=m.user_id WHERE m.institution_id=@p0 AND m.status='active' AND s.security_generation>=0",ct,request.InstitutionId)=="0")throw new UnauthorizedAccessException();
            await Execute("UPDATE institution SET status='active' WHERE id=@p0",ct,request.InstitutionId);
        }
        var result=new InvitationResult(request.EventId,"consumed",membership);
        await Append(request.InstitutionId,request.EventId,"Recipient",null,null,user,null,invite.Kind=="Recovery"?"RecoveryAccepted":"InviteAccepted",invite.Id,hash,"Verified personal invitation acceptance",null,invite.Kind=="Recovery"?new {result.EventId,result.Status,result.MembershipId,SuspendedAdministrators=suspendedAdmins}:(object)result,ct);
        Enabled();identities.VerifySelectionTicket(ticket);await faults.BeforeCommitAsync(invite.Kind=="Recovery"?"RecoveryAccept":"Accept",ct);
        if(invite.Kind=="Recovery")await AuthorizeRecoveryAcceptance(request.InstitutionId,invite,ct,false);await tx.CommitAsync(ct);return result;
    }
}
