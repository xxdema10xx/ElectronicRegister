using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

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
    public const string Purpose = "purpose";
    public static string SelectionAudience(string audience) => audience + ":institution-selection";
    public static string AccessAudience(string audience) => audience + ":institution-access";
}

internal sealed class InstitutionTokens(IOptions<JwtOptions> options) : IInstitutionTokens
{
    public string Selection(Guid userId) => Create(userId, InstitutionTokenContract.SelectionType,
        InstitutionTokenContract.SelectionAudience(options.Value.Audience), 300, []);

    public string Access(TenantContext context) => Create(context.UserId, InstitutionTokenContract.AccessType,
        InstitutionTokenContract.AccessAudience(options.Value.Audience), 900,
        new[] { new Claim(InstitutionTokenContract.Institution, context.InstitutionId.ToString()),
                new Claim(InstitutionTokenContract.Membership, context.MembershipId.ToString()) }
            .Concat(context.Roles.Order().Select(r => new Claim(InstitutionTokenContract.Role, r.ToString()))));

    private string Create(Guid userId, string type, string audience, int seconds, IEnumerable<Claim> additional)
    {
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(options.Value.Issuer, audience,
            new[] { new Claim("sub", userId.ToString()), new Claim("jti", Guid.NewGuid().ToString()),
                new Claim(InstitutionTokenContract.Purpose, type),
                new Claim("auth_time", new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64) }.Concat(additional),
            notBefore: now, expires: now.AddSeconds(seconds),
            signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Value.Key)), SecurityAlgorithms.HmacSha256));
        token.Header["typ"] = type;
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
