using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ElectronicRegisterAPI.Infrastructure.Security;

public sealed class PlatformAuthorityOptions
{
    public bool Enabled { get; set; }
    public PlatformPublicKey[] Keys { get; set; } = [];
}
public sealed class PlatformPublicKey
{
    public string OperatorId { get; set; } = "";
    public string KeyId { get; set; } = "";
    public string PublicKeyPem { get; set; } = "";
    public bool Revoked { get; set; }
    public DateTime NotBeforeUtc { get; set; }
    public DateTime NotAfterUtc { get; set; }
    public PlatformAction[] Actions { get; set; } = [];
}
public static class PlatformSigning
{
    // The exact typed intent, operation identity, time and nonce are signed. No JSON
    // supplied by the caller is executed, and signatures cannot cross protocols.
    public static byte[] Bytes(SignedPlatformOperation op) => Encoding.UTF8.GetBytes("ElectronicRegister.Platform.v1\n"+
        JsonSerializer.Serialize(new { op.OperatorId, op.KeyId, op.EventId, op.Nonce, op.IssuedAt, op.ExpiresAt, op.Action, op.Intent }));
}
internal sealed class PlatformAuthority(IOptionsMonitor<PlatformAuthorityOptions> options) : IPlatformAuthority
{
    private static bool Active(PlatformPublicKey key) => !key.Revoked && key.NotBeforeUtc <= DateTime.UtcNow && key.NotAfterUtc > DateTime.UtcNow;
    private static bool DistinctIdentities(PlatformPublicKey[] keys)
    {
        try
        {
            var fingerprints=keys.Where(Active).Select(k=>{
                using var rsa=RSA.Create();if(k.PublicKeyPem.Contains("PRIVATE KEY",StringComparison.Ordinal))throw new CryptographicException();rsa.ImportFromPem(k.PublicKeyPem);
                if(rsa.KeySize<3072)throw new CryptographicException();
                return new {k.OperatorId,Fingerprint=Convert.ToHexString(SHA256.HashData(rsa.ExportSubjectPublicKeyInfo()))};}).ToArray();
            return !fingerprints.GroupBy(k=>k.Fingerprint).Any(g=>g.Select(k=>k.OperatorId).Distinct(StringComparer.Ordinal).Count()>1);
        }
        catch(Exception e) when(e is CryptographicException or ArgumentException){return false;}
    }
    public int ActiveOperatorCount => options.CurrentValue.Enabled && DistinctIdentities(options.CurrentValue.Keys) ? options.CurrentValue.Keys.Where(Active).Where(k=>k.Actions.Contains(PlatformAction.ApproveRecovery)).Select(k=>k.OperatorId).Distinct(StringComparer.Ordinal).Count() : 0;
    public void Verify(SignedPlatformOperation op)=>VerifyCore(op,false);
    public void VerifyCommitted(SignedPlatformOperation op)
    {
        if(op is null || op.Action is not (PlatformAction.RequestRecovery or PlatformAction.ApproveRecovery))throw new UnauthorizedAccessException();
        VerifyCore(op,true);
    }
    private void VerifyCore(SignedPlatformOperation op,bool committed)
    {
        var config=options.CurrentValue;
        var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if(!config.Enabled || op is null || op.Intent is null || op.EventId==Guid.Empty ||
            !Enum.IsDefined(op.Action) || !Regex.IsMatch(op.OperatorId??"", "^[a-zA-Z0-9_-]{3,64}$") ||
            !Regex.IsMatch(op.KeyId??"", "^[a-zA-Z0-9_-]{3,64}$") || !Regex.IsMatch(op.Nonce??"", "^[A-F0-9]{64}$") ||
            op.IssuedAt<=0 || op.IssuedAt>now+30 || (!committed && op.IssuedAt<now-300) || op.ExpiresAt>now+330 || (!committed && op.ExpiresAt<=now) || op.ExpiresAt<=op.IssuedAt || op.ExpiresAt-op.IssuedAt>300)
            throw new UnauthorizedAccessException("Operazione piattaforma non autorizzata.");
        var matches=config.Keys.Where(k=>k.OperatorId==op.OperatorId && k.KeyId==op.KeyId && Active(k) && k.Actions.Contains(op.Action)).ToArray();
        if(matches.Length!=1 || !DistinctIdentities(config.Keys) || config.Keys.GroupBy(k=>k.KeyId,StringComparer.Ordinal).Any(g=>g.Count()!=1)) throw new UnauthorizedAccessException();
        try
        {
            if(matches[0].PublicKeyPem.Contains("PRIVATE KEY",StringComparison.Ordinal))throw new UnauthorizedAccessException();
            using var rsa=RSA.Create();rsa.ImportFromPem(matches[0].PublicKeyPem);
            if(rsa.KeySize<3072 || !rsa.VerifyData(PlatformSigning.Bytes(op),Convert.FromBase64String(op.Signature),HashAlgorithmName.SHA256,RSASignaturePadding.Pss)) throw new UnauthorizedAccessException();
        }
        catch(Exception ex) when(ex is CryptographicException or FormatException or ArgumentException) { throw new UnauthorizedAccessException("Firma non valida."); }
    }
}
internal sealed class RecipientIdentity(IOptions<JwtOptions> options) : IRecipientIdentity
{
    public Guid VerifySelectionTicket(string ticket)
    {
        try
        {
            var opt=options.Value;var handler=new JwtSecurityTokenHandler{MapInboundClaims=false};
            var principal=handler.ValidateToken(ticket,new TokenValidationParameters {
                ValidateIssuer=true,ValidIssuer=opt.Issuer,ValidateAudience=true,ValidAudience=InstitutionTokenContract.SelectionAudience(opt.Audience),
                ValidateLifetime=true,RequireExpirationTime=true,RequireSignedTokens=true,ValidateIssuerSigningKey=true,
                IssuerSigningKey=new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opt.Key)),ValidAlgorithms=[SecurityAlgorithms.HmacSha256],
                ValidTypes=[InstitutionTokenContract.SelectionType],ClockSkew=TimeSpan.Zero },out var validated);
            var jwt=(JwtSecurityToken)validated;
            using var json=JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(ticket.Split('.')[1]));
            using var header=JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(ticket.Split('.')[0]));
            string One(string name) {var claims=principal.FindAll(name).ToArray();return claims.Length==1?claims[0].Value:throw new UnauthorizedAccessException();}
            if(json.RootElement.EnumerateObject().GroupBy(x=>x.Name).Any(g=>g.Count()!=1) ||
               header.RootElement.EnumerateObject().GroupBy(x=>x.Name).Any(g=>g.Count()!=1) ||
               principal.FindAll("purpose").Count()!=1 || principal.FindFirst("purpose")!.Value!=InstitutionTokenContract.SelectionType ||
               principal.Claims.Any(c=>c.Type is "institution_id" or "membership_id" or "security_generation" or "role" or "roles" or "institution_role" or "student_id" or "teacher_id" or "studentId" or "teacherId" || c.Type==System.Security.Claims.ClaimTypes.Role) ||
               principal.FindAll("sub").Count()!=1 || !Guid.TryParse(principal.FindFirst("sub")?.Value,out var id) || id==Guid.Empty)
                throw new UnauthorizedAccessException();
            if(!Guid.TryParse(One("jti"),out var tokenId) || tokenId==Guid.Empty ||
               !long.TryParse(One("auth_time"),out var time) || time<=0 || time>DateTimeOffset.UtcNow.ToUnixTimeSeconds() ||
               One("aud")!=InstitutionTokenContract.SelectionAudience(opt.Audience) ||
               json.RootElement.GetProperty("sub").ValueKind!=JsonValueKind.String ||
               json.RootElement.GetProperty("purpose").ValueKind!=JsonValueKind.String)throw new UnauthorizedAccessException();
            return id;
        }
        catch(Exception ex) when(ex is SecurityTokenException or ArgumentException or JsonException or IndexOutOfRangeException) { throw new UnauthorizedAccessException("Identità destinatario non verificata."); }
    }
}
internal sealed class ClosedInvitationChannel : IInvitationChannel
{
    public bool IsOperational=>false;
    public Task DeliverAsync(InvitationDelivery delivery,CancellationToken ct)=>throw new UnauthorizedAccessException("Canale di consegna non configurato.");
}
internal sealed class NoOnboardingFault : IOnboardingFaultObserver
{
    public Task BeforeCommitAsync(string action,CancellationToken ct)=>Task.CompletedTask;
}
