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
using ElectronicRegisterAPI.Infrastructure.Security;

namespace ElectronicRegisterAPI.Infrastructure.Options;

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
