using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using ElectronicRegisterAPI.Infrastructure.Options;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using ElectronicRegisterAPI.Infrastructure.Repositories;
using ElectronicRegisterAPI.Infrastructure.Security;

namespace ElectronicRegisterAPI.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<ElectronicRegisterContext>(options =>
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<AzureAdOptions>(configuration.GetSection(AzureAdOptions.SectionName));

        var azureTenantId = configuration["AzureAd:TenantId"];
        var stsDiscoveryEndpoint = $"https://login.microsoftonline.com/{azureTenantId}/v2.0/.well-known/openid-configuration";
        services.AddSingleton(new ConfigurationManager<OpenIdConnectConfiguration>(
            stsDiscoveryEndpoint, new OpenIdConnectConfigurationRetriever()));

        services.AddScoped<IGradeRepository, GradeRepository>();
        services.AddScoped<IStudentRepository, StudentRepository>();
        services.AddScoped<ISubjectRepository, SubjectRepository>();
        services.AddScoped<ITeacherRepository, TeacherRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IMicrosoftTokenValidator, MicrosoftTokenValidator>();

        return services;
    }
}
