using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace ElectronicRegisterAPI.Api.Security;

[AttributeUsage(AttributeTargets.Class)]
public sealed class InstitutionIdentityEndpointAttribute : Attribute;

internal sealed class CurrentTenant : ICurrentTenant
{
    private TenantContext? _value;
    public TenantContext Value => _value ?? throw new UnauthorizedAccessException("Contesto non autorizzato.");
    internal void Set(TenantContext value) => _value = value;
}

public static class InstitutionAuthentication
{
    public static IServiceCollection AddInstitutionAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var issuer = configuration["Jwt:Issuer"];
        var audience = configuration["Jwt:Audience"];
        var key = configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience) || Encoding.UTF8.GetByteCount(key ?? "") < 32)
            throw new InvalidOperationException("Configurazione JWT istituzionale mancante o non valida.");
        services.AddAuthentication()
            .AddJwtBearer(InstitutionTokenContract.SelectionScheme, options => Configure(options, true, issuer, audience, key!))
            .AddJwtBearer(InstitutionTokenContract.AccessScheme, options => Configure(options, false, issuer, audience, key!));
        services.AddScoped<CurrentTenant>();
        services.AddScoped<ICurrentTenant>(p => p.GetRequiredService<CurrentTenant>());
        services.AddScoped<IAuthorizationHandler, InstitutionAuthorizationHandler>();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(InstitutionTokenContract.SelectionScheme, p => p
                .AddAuthenticationSchemes(InstitutionTokenContract.SelectionScheme).RequireAuthenticatedUser().AddRequirements(new InstitutionRequirement(true)));
            options.AddPolicy(InstitutionTokenContract.AccessScheme, p => p
                .AddAuthenticationSchemes(InstitutionTokenContract.AccessScheme).RequireAuthenticatedUser().AddRequirements(new InstitutionRequirement(false)));
        });
        return services;
    }

    private static void Configure(JwtBearerOptions options, bool selection, string issuer, string audience, string key)
    {
        var type = selection ? InstitutionTokenContract.SelectionType : InstitutionTokenContract.AccessType;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new()
        {
            ValidateIssuer = true, ValidIssuer = issuer,
            ValidateAudience = true, ValidAudience = selection ? InstitutionTokenContract.SelectionAudience(audience) : InstitutionTokenContract.AccessAudience(audience),
            ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
            ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256], ValidTypes = [type], ClockSkew = TimeSpan.Zero,
            NameClaimType = "sub", RoleClaimType = InstitutionTokenContract.Role
        };
        options.Events = new()
        {
            OnTokenValidated = context =>
            {
                try
                {
                    var principal = context.Principal!;
                    if (One(principal, InstitutionTokenContract.Purpose) != type) throw new SecurityTokenException();
                    _ = GuidClaim(principal, "sub");
                    _ = GuidClaim(principal, "jti");
                    if (!long.TryParse(One(principal, "auth_time"), out var timestamp) || timestamp <= 0 ||
                        timestamp > DateTimeOffset.UtcNow.ToUnixTimeSeconds()) throw new SecurityTokenException();
                    foreach (var claim in new[] { "iss", "aud", "exp" }) _ = One(principal, claim);
                    if (principal.HasClaim(c => c.Type is "role" or "roles" or "student_id" or "teacher_id" or "studentId" or "teacherId" || c.Type == ClaimTypes.Role))
                        throw new SecurityTokenException();
                    if (selection)
                    {
                        if (principal.HasClaim(c => c.Type is InstitutionTokenContract.Institution or InstitutionTokenContract.Membership or InstitutionTokenContract.Role))
                            throw new SecurityTokenException();
                    }
                    else _ = ParseTenant(principal);
                    // Reject duplicate JSON properties even if a JWT reader would overwrite them.
                    var raw = context.Request.Headers.Authorization.ToString()["Bearer ".Length..];
                    using var payload = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(raw.Split('.')[1]));
                    if (payload.RootElement.EnumerateObject().GroupBy(p => p.Name).Any(g => g.Count() != 1))
                        throw new SecurityTokenException();
                }
                catch (Exception ex) when (ex is ArgumentException or SecurityTokenException or JsonException or IndexOutOfRangeException)
                { context.Fail("Token non valido."); }
                return Task.CompletedTask;
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.Headers.WWWAuthenticate = "Bearer";
                await Results.Problem(statusCode: 401, title: "Non autenticato").ExecuteAsync(context.HttpContext);
            },
            OnForbidden = context => Results.Problem(statusCode: 403, title: "Non autorizzato").ExecuteAsync(context.HttpContext)
        };
    }

    internal static string One(ClaimsPrincipal principal, string type)
    {
        var values = principal.FindAll(type).ToArray();
        return values.Length == 1 && !string.IsNullOrWhiteSpace(values[0].Value) ? values[0].Value : throw new SecurityTokenException();
    }

    internal static Guid GuidClaim(ClaimsPrincipal principal, string type) =>
        Guid.TryParse(One(principal, type), out var id) && id != Guid.Empty ? id : throw new SecurityTokenException();

    internal static TenantContext ParseTenant(ClaimsPrincipal principal)
    {
        var raw = principal.FindAll(InstitutionTokenContract.Role).Select(c => c.Value).ToArray();
        if (raw.Length == 0 || raw.Distinct().Count() != raw.Length) throw new SecurityTokenException();
        var roles = raw.Select(v => Enum.TryParse<InstitutionRole>(v, out var role) && Enum.IsDefined(role) && role.ToString() == v
            ? role : throw new SecurityTokenException()).ToArray();
        return new(GuidClaim(principal, "sub"), GuidClaim(principal, InstitutionTokenContract.Institution),
            GuidClaim(principal, InstitutionTokenContract.Membership), roles);
    }
}

internal sealed record InstitutionRequirement(bool Selection) : IAuthorizationRequirement;

internal sealed class InstitutionAuthorizationHandler(IInstitutionAuthManager manager, CurrentTenant current,
    IHttpContextAccessor accessor) : AuthorizationHandler<InstitutionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, InstitutionRequirement requirement)
    {
        var ct = accessor.HttpContext?.RequestAborted ?? CancellationToken.None;
        if (context.User.Identity?.IsAuthenticated != true) return;
        if (requirement.Selection)
        {
            if (await manager.IdentityExistsAsync(InstitutionAuthentication.GuidClaim(context.User, "sub"))) context.Succeed(requirement);
        }
        else
        {
            var tenant = await manager.AuthorizeAsync(InstitutionAuthentication.ParseTenant(context.User), ct);
            if (tenant is not null) { current.Set(tenant); context.Succeed(requirement); }
        }
    }
}
