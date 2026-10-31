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

public static class PlatformSigning
{
    // The exact typed intent, operation identity, time and nonce are signed. No JSON
    // supplied by the caller is executed, and signatures cannot cross protocols.
    public static byte[] Bytes(SignedPlatformOperation op) => Encoding.UTF8.GetBytes("ElectronicRegister.Platform.v1\n" + JsonSerializer.Serialize(new { op.OperatorId, op.KeyId, op.EventId, op.Nonce, op.IssuedAt, op.ExpiresAt, op.Action, op.Intent }));
}
