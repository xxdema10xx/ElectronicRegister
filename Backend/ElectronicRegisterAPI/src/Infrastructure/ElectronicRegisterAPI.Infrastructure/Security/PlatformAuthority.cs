using ElectronicRegisterAPI.Domain.DTOs.Onboarding;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
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

internal sealed class PlatformAuthority(IOptionsMonitor<PlatformAuthorityOptions> options) : IPlatformAuthority
{
    private static bool Active(PlatformPublicKey key) => !key.Revoked && key.NotBeforeUtc <= DateTime.UtcNow && key.NotAfterUtc > DateTime.UtcNow;
    private static bool DistinctIdentities(PlatformPublicKey[] keys)
    {
        try
        {
            var fingerprints = keys.Where(Active).Select(k =>
            {
                using var rsa = RSA.Create();
                if (k.PublicKeyPem.Contains("PRIVATE KEY", StringComparison.Ordinal))
                    throw new CryptographicException();
                rsa.ImportFromPem(k.PublicKeyPem);
                if (rsa.KeySize < 3072)
                    throw new CryptographicException();
                return new
                {
                    k.OperatorId,
                    Fingerprint = Convert.ToHexString(SHA256.HashData(rsa.ExportSubjectPublicKeyInfo()))
                };
            }).ToArray();
            return !fingerprints.GroupBy(k => k.Fingerprint).Any(g => g.Select(k => k.OperatorId).Distinct(StringComparer.Ordinal).Count() > 1);
        }
        catch (Exception e)when (e is CryptographicException or ArgumentException)
        {
            return false;
        }
    }

    public int ActiveOperatorCount => options.CurrentValue.Enabled && DistinctIdentities(options.CurrentValue.Keys) ? options.CurrentValue.Keys.Where(Active).Where(k => k.Actions.Contains(PlatformAction.ApproveRecovery)).Select(k => k.OperatorId).Distinct(StringComparer.Ordinal).Count() : 0;

    public void Verify(SignedPlatformOperation op) => VerifyCore(op, false);
    public void VerifyCommitted(SignedPlatformOperation op)
    {
        if (op is null || op.Action is not (PlatformAction.RequestRecovery or PlatformAction.ApproveRecovery))
            throw new UnauthorizedAccessException();
        VerifyCore(op, true);
    }

    private void VerifyCore(SignedPlatformOperation op, bool committed)
    {
        var config = options.CurrentValue;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (!config.Enabled || op is null || op.Intent is null || op.EventId == Guid.Empty || !Enum.IsDefined(op.Action) || !Regex.IsMatch(op.OperatorId ?? "", "^[a-zA-Z0-9_-]{3,64}$") || !Regex.IsMatch(op.KeyId ?? "", "^[a-zA-Z0-9_-]{3,64}$") || !Regex.IsMatch(op.Nonce ?? "", "^[A-F0-9]{64}$") || op.IssuedAt <= 0 || op.IssuedAt > now + 30 || (!committed && op.IssuedAt < now - 300) || op.ExpiresAt > now + 330 || (!committed && op.ExpiresAt <= now) || op.ExpiresAt <= op.IssuedAt || op.ExpiresAt - op.IssuedAt > 300)
            throw new UnauthorizedAccessException("Operazione piattaforma non autorizzata.");
        var matches = config.Keys.Where(k => k.OperatorId == op.OperatorId && k.KeyId == op.KeyId && Active(k) && k.Actions.Contains(op.Action)).ToArray();
        if (matches.Length != 1 || !DistinctIdentities(config.Keys) || config.Keys.GroupBy(k => k.KeyId, StringComparer.Ordinal).Any(g => g.Count() != 1))
            throw new UnauthorizedAccessException();
        try
        {
            if (matches[0].PublicKeyPem.Contains("PRIVATE KEY", StringComparison.Ordinal))
                throw new UnauthorizedAccessException();
            using var rsa = RSA.Create();
            rsa.ImportFromPem(matches[0].PublicKeyPem);
            if (rsa.KeySize < 3072 || !rsa.VerifyData(PlatformSigning.Bytes(op), Convert.FromBase64String(op.Signature), HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
                throw new UnauthorizedAccessException();
        }
        catch (Exception ex)when (ex is CryptographicException or FormatException or ArgumentException)
        {
            throw new UnauthorizedAccessException("Firma non valida.");
        }
    }
}
