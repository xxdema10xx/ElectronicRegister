using System.IdentityModel.Tokens.Jwt;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;

namespace ElectronicRegisterAPI.Infrastructure.Security;

public static class InstitutionTokenContract
{
    public const string SelectionScheme = "InstitutionSelection";
    public const string AccessScheme = "InstitutionAccess";
    public const string SelectionType = "er-selection+jwt";
    public const string AccessType = "er-institution+jwt";
    public const string Institution = "institution_id";
    public const string Membership = "membership_id";
    public const string Role = "institution_role";
    public const string SecurityGeneration = "security_generation";
    public const string Purpose = "purpose";
    public const string IdentityGeneration = "identity_generation";
    public static long ReadIdentityGeneration(ClaimsPrincipal principal, JsonElement? payload = null)
    {
        var values = principal.FindAll(IdentityGeneration).ToArray();
        long generation = 0;
        if (values.Length != 0 && (values.Length != 1 || !long.TryParse(values[0].Value, NumberStyles.None, CultureInfo.InvariantCulture, out generation) || generation < 0 || values[0].Value != generation.ToString(CultureInfo.InvariantCulture)))
            throw new SecurityTokenException("Generation identità non valida.");
        if (payload is { } json && json.TryGetProperty(IdentityGeneration, out var raw) && (raw.ValueKind != JsonValueKind.Number || !raw.TryGetInt64(out var value) || value != generation || raw.GetRawText() != generation.ToString(CultureInfo.InvariantCulture)))
            throw new SecurityTokenException("Generation identità non valida.");
        return generation;
    }

    public static string SelectionAudience(string audience) => audience + ":institution-selection";
    public static string AccessAudience(string audience) => audience + ":institution-access";
}
