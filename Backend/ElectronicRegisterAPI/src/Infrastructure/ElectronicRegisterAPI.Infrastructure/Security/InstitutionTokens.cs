using System.IdentityModel.Tokens.Jwt;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ElectronicRegisterAPI.Infrastructure.Security;

internal sealed class InstitutionTokens(IOptions<JwtOptions> options) : IInstitutionTokens
{
    public string Selection(Guid userId, long identityGeneration = 0) => Create(userId, InstitutionTokenContract.SelectionType, InstitutionTokenContract.SelectionAudience(options.Value.Audience), 300, [IdentityClaim(identityGeneration)]);
    public string Access(TenantContext context, long identityGeneration = 0)
    {
        if (context.SecurityGeneration is not long generation || generation < 0)
            throw new UnauthorizedAccessException("Stato di sicurezza richiesto.");
        return Create(context.UserId, InstitutionTokenContract.AccessType, InstitutionTokenContract.AccessAudience(options.Value.Audience), 900, new[] { IdentityClaim(identityGeneration), new Claim(InstitutionTokenContract.Institution, context.InstitutionId.ToString()), new Claim(InstitutionTokenContract.Membership, context.MembershipId.ToString()), new Claim(InstitutionTokenContract.SecurityGeneration, generation.ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64) }.Concat(context.Roles.Order().Select(r => new Claim(InstitutionTokenContract.Role, r.ToString()))));
    }

    private static Claim IdentityClaim(long generation) => generation >= 0 ? new(InstitutionTokenContract.IdentityGeneration, generation.ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64) : throw new UnauthorizedAccessException();
    private string Create(
        Guid userId,
        string type,
        string audience,
        int seconds,
        IEnumerable<Claim> additional
    )
    {
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(options.Value.Issuer, audience, new[] { new Claim("sub", userId.ToString()), new Claim("jti", Guid.NewGuid().ToString()), new Claim(InstitutionTokenContract.Purpose, type), new Claim("auth_time", new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64) }.Concat(additional), notBefore: now, expires: now.AddSeconds(seconds), signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Value.Key)), SecurityAlgorithms.HmacSha256));
        token.Header["typ"] = type;
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
