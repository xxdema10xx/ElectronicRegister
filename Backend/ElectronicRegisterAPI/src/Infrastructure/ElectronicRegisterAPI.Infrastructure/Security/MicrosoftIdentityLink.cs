using ElectronicRegisterAPI.Domain.Interfaces.Security;
using ElectronicRegisterAPI.Infrastructure.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ElectronicRegisterAPI.Infrastructure.Security;

internal sealed class MicrosoftIdentityLink(IMicrosoftTokenValidator validator, IOptions<MicrosoftIdentityOptions> bindings, IOptions<AzureAdOptions> azure) : IMicrosoftIdentityLink
{
    public async Task<Guid?> AuthenticateAsync(string token)
    {
        System.Security.Claims.ClaimsPrincipal principal;
        try
        {
            principal = await validator.ValidateAsync(token);
        }
        catch (SecurityTokenException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }

        // Default JwtSecurityTokenHandler may map oid. Email/preferred_username are NEVER used.
        var tids = principal.FindAll("tid").Concat(principal.FindAll("http://schemas.microsoft.com/identity/claims/tenantid")).ToArray();
        var oids = principal.FindAll("oid").Concat(principal.FindAll("http://schemas.microsoft.com/identity/claims/objectidentifier")).ToArray();
        if (tids.Length != 1 || oids.Length != 1 || !Guid.TryParse(tids[0].Value, out var tid) || !Guid.TryParse(oids[0].Value, out var oid) || !Guid.TryParse(azure.Value.TenantId, out var expected) || tid != expected)
            return null;
        var links = bindings.Value.Bindings.Where(b => b.TenantId == tid && b.ObjectId == oid && b.UserId != Guid.Empty).ToArray();
        return links.Length == 1 ? links[0].UserId : null;
    }
}
