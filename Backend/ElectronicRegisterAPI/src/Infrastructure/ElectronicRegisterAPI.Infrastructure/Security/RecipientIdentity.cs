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
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;

namespace ElectronicRegisterAPI.Infrastructure.Security;

internal sealed class RecipientIdentity(IOptions<JwtOptions> options, IIdentityAccountRepository accounts) : IRecipientIdentity
{
    public async Task<Guid> VerifySelectionTicketAsync(string ticket, CancellationToken ct = default)
    {
        try
        {
            var opt = options.Value;
            var handler = new JwtSecurityTokenHandler
            {
                MapInboundClaims = false
            };
            var principal = handler.ValidateToken(ticket, new TokenValidationParameters { ValidateIssuer = true, ValidIssuer = opt.Issuer, ValidateAudience = true, ValidAudience = InstitutionTokenContract.SelectionAudience(opt.Audience), ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true, ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opt.Key)), ValidAlgorithms = [SecurityAlgorithms.HmacSha256], ValidTypes = [InstitutionTokenContract.SelectionType], ClockSkew = TimeSpan.Zero }, out var validated);
            var jwt = (JwtSecurityToken)validated;
            using var json = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(ticket.Split('.')[1]));
            using var header = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(ticket.Split('.')[0]));
            string One(string name)
            {
                var claims = principal.FindAll(name).ToArray();
                return claims.Length == 1 ? claims[0].Value : throw new UnauthorizedAccessException();
            }

            if (json.RootElement.EnumerateObject().GroupBy(x => x.Name).Any(g => g.Count() != 1) || header.RootElement.EnumerateObject().GroupBy(x => x.Name).Any(g => g.Count() != 1) || principal.FindAll("purpose").Count() != 1 || principal.FindFirst("purpose")!.Value != InstitutionTokenContract.SelectionType || principal.Claims.Any(c => c.Type is "institution_id" or "membership_id" or "security_generation" or "role" or "roles" or "institution_role" or "student_id" or "teacher_id" or "studentId" or "teacherId" || c.Type == System.Security.Claims.ClaimTypes.Role) || principal.FindAll("sub").Count() != 1 || !Guid.TryParse(principal.FindFirst("sub")?.Value, out var id) || id == Guid.Empty)
                throw new UnauthorizedAccessException();
            if (!Guid.TryParse(One("jti"), out var tokenId) || tokenId == Guid.Empty || !long.TryParse(One("auth_time"), out var time) || time <= 0 || time > DateTimeOffset.UtcNow.ToUnixTimeSeconds() || One("aud") != InstitutionTokenContract.SelectionAudience(opt.Audience) || json.RootElement.GetProperty("sub").ValueKind != JsonValueKind.String || json.RootElement.GetProperty("purpose").ValueKind != JsonValueKind.String)
                throw new UnauthorizedAccessException();
            await accounts.RequireGenerationAsync(id, InstitutionTokenContract.ReadIdentityGeneration(principal, json.RootElement), ct);
            return id;
        }
        catch (Exception ex)when (ex is SecurityTokenException or ArgumentException or JsonException or IndexOutOfRangeException)
        {
            throw new UnauthorizedAccessException("Identità destinatario non verificata.");
        }
    }
}
