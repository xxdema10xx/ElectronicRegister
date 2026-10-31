using ElectronicRegisterAPI.Infrastructure.Services;
using ElectronicRegisterAPI.Domain.Configuration;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using ElectronicRegisterAPI.Infrastructure.Options;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using ElectronicRegisterAPI.Infrastructure.Repositories;
using ElectronicRegisterAPI.Infrastructure.Mappers;
using ElectronicRegisterAPI.Infrastructure.Security;

namespace ElectronicRegisterAPI.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton<IConfiguration>(configuration);
        services.AddSingleton<ICalendarFeature>(new CalendarFeature(() => bool.TryParse(configuration["Multitenancy:InstitutionCalendar:Enabled"], out var calendar) && calendar && bool.TryParse(configuration["Multitenancy:SchoolApi:Enabled"], out var calendarSchool) && calendarSchool));
        services.AddScoped<ElectronicRegisterAPI.Domain.Interfaces.Repositories.IInstitutionCalendarRepository, InstitutionCalendarRepository>();
        services.AddScoped<ElectronicRegisterAPI.Domain.Interfaces.Services.ICalendarImportFileReader, ElectronicRegisterAPI.Infrastructure.Services.InstitutionImportFileReader>();
        services.TryAddSingleton<ElectronicRegisterAPI.Domain.Interfaces.Services.ICalendarFaultObserver, ElectronicRegisterAPI.Infrastructure.Services.NoCalendarFault>();
        services.AddSingleton<IFormativePlanFeature>(new FormativePlanFeature(() => bool.TryParse(configuration["Multitenancy:FormativePlans:Enabled"], out var enabled) && enabled && bool.TryParse(configuration["Multitenancy:SchoolApi:Enabled"], out var school) && school && bool.TryParse(configuration["Multitenancy:CourseApi:Enabled"], out var course) && course));
        services.AddScoped<ElectronicRegisterAPI.Domain.Interfaces.Repositories.IInstitutionFormativePlanRepository, InstitutionFormativePlanRepository>();
        services.TryAddSingleton<ElectronicRegisterAPI.Domain.Interfaces.Services.IFormativePlanFaultObserver, ElectronicRegisterAPI.Infrastructure.Services.NoFormativePlanFault>();
        services.AddScoped<ElectronicRegisterAPI.Domain.Interfaces.Services.IInstitutionImportFileReader, ElectronicRegisterAPI.Infrastructure.Services.InstitutionImportFileReader>();
        int ImportNumber(string key, int fallback) => configuration["Multitenancy:InstitutionImport:" + key] is { } raw ? int.Parse(raw, System.Globalization.CultureInfo.InvariantCulture) : fallback;
        var importFeature = new ElectronicRegisterAPI.Domain.Configuration.InstitutionImportFeature(bool.TryParse(configuration["Multitenancy:InstitutionImport:Enabled"], out var importOn) && importOn, bool.TryParse(configuration["Multitenancy:SchoolApi:Enabled"], out var schoolOn) && schoolOn, new(ImportNumber("Limits:MaxFileBytes", 10 * 1024 * 1024), ImportNumber("Limits:MaxDataRows", 2000), ImportNumber("Limits:MaxColumns", 16), ImportNumber("Limits:MaxCellCharacters", 512), ImportNumber("Limits:MaxRecordCharacters", 8192), ImportNumber("Limits:MaxZipEntries", 128), ImportNumber("Limits:MaxUncompressedBytes", 40 * 1024 * 1024), ImportNumber("Limits:MaxXmlNodes", 200000)), ImportNumber("TtlMinutes", 30), ImportNumber("MaxStageBytes", 2 * 1024 * 1024), ImportNumber("MaxPendingPreviews", 20));
        var importRuntime = new InstitutionImportRuntimeFeature(importFeature, new RuntimeFeatureAvailability(() => bool.TryParse(configuration["Multitenancy:InstitutionImport:Enabled"], out var currentImport) && currentImport && bool.TryParse(configuration["Multitenancy:SchoolApi:Enabled"], out var currentSchool) && currentSchool));
        importFeature.Validate();
        services.AddSingleton<IInstitutionImportFeature>(importRuntime);
        services.AddScoped<ElectronicRegisterAPI.Domain.Interfaces.Repositories.IInstitutionImportRepository, InstitutionImportRepository>();
        services.AddScoped<ElectronicRegisterAPI.Domain.Interfaces.Services.IInstitutionImportRetention, ElectronicRegisterAPI.Infrastructure.Services.InstitutionImportRetention>();
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        services.AddDbContext<ElectronicRegisterContext>(options =>
        {
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
            if (TestDatabaseSession.Enabled)
                options.AddInterceptors(new TestDatabaseSession());
        });
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<PlatformAuthorityOptions>(configuration.GetSection("PlatformAuthority"));
        services.AddScoped<ElectronicRegisterAPI.Domain.Interfaces.Security.IPlatformAuthority, PlatformAuthority>();
        services.AddScoped<ElectronicRegisterAPI.Domain.Interfaces.Security.IRecipientIdentity, RecipientIdentity>();
        services.AddScoped<ElectronicRegisterAPI.Domain.Interfaces.Repositories.IOnboardingRepository, InstitutionOnboardingRepository>();
        services.AddScoped<ElectronicRegisterAPI.Domain.Interfaces.Repositories.IInstitutionGovernanceRepository, InstitutionOnboardingRepository>();
        services.AddScoped<ElectronicRegisterAPI.Domain.Interfaces.Repositories.IMembershipAuditRepository, MembershipAuditRepository>();
        services.AddSingleton<ElectronicRegisterAPI.Domain.Interfaces.Security.IInvitationChannel, ClosedInvitationChannel>();
        services.AddOwnerInstalledInvitationChannel(configuration);
        services.AddSingleton<ElectronicRegisterAPI.Domain.Interfaces.Services.IOnboardingFaultObserver, NoOnboardingFault>();
        services.Configure<AzureAdOptions>(configuration.GetSection(AzureAdOptions.SectionName));
        services.Configure<MicrosoftIdentityOptions>(configuration.GetSection("Identity:Microsoft"));
        services.AddScoped<IMembershipAuthorizationRepository, MembershipAuthorizationRepository>();
        services.AddScoped<IInstitutionMembershipDirectoryRepository, InstitutionMembershipDirectoryRepository>();
        services.AddScoped<IInstitutionMembershipMutationRepository, InstitutionMembershipMutationRepository>();
        services.AddScoped<IInstitutionMembershipSecurityStateRepository, InstitutionMembershipSecurityStateRepository>();
        services.AddScoped<IInstitutionProfileRepository, InstitutionProfileRepository>();
        services.AddScoped<InstitutionSchoolQueries>();
        services.AddSingleton(new ElectronicRegisterAPI.Domain.Configuration.InstitutionEmailFeature(bool.TryParse(configuration["Multitenancy:InstitutionEmail:Enabled"], out var emailEnabled) && emailEnabled));
        services.AddScoped<ElectronicRegisterAPI.Domain.Interfaces.Repositories.IInstitutionEmailRepository, InstitutionEmailRepository>();
        services.AddScoped<IInstitutionWriteAuthorization, InstitutionWriteAuthorization>();
        services.AddScoped<ICourseStructureRepository, CourseStructureRepository>();
        services.AddScoped<IInstitutionStudentRepository, InstitutionStudentRepository>();
        services.AddScoped<IInstitutionStudentTransferRepository, InstitutionStudentTransferRepository>();
        services.AddScoped<IInstitutionTeacherRepository, InstitutionTeacherRepository>();
        services.AddScoped<IInstitutionSubjectRepository, InstitutionSubjectRepository>();
        services.AddScoped<IInstitutionClassRepository, InstitutionClassRepository>();
        services.AddScoped<IInstitutionAcademicCatalogRepository, InstitutionAcademicCatalogRepository>();
        services.AddScoped<IInstitutionClassSubjectRepository, InstitutionClassSubjectRepository>();
        services.AddScoped<IInstitutionCampusRepository, InstitutionCampusRepository>();
        services.AddScoped<InstitutionAdministrationPersistence>();
        services.AddScoped<IInstitutionExamRepositoryFactory, InstitutionExamRepositoryFactory>();
        services.AddScoped<ElectronicRegisterAPI.Domain.Interfaces.Repositories.ISubmissionExpiryStore, SubmissionExpiryStore>();
        services.AddScoped<IInstitutionTokens, InstitutionTokens>();
        services.AddScoped<IMicrosoftIdentityLink, MicrosoftIdentityLink>();
        var azureTenantId = configuration["AzureAd:TenantId"];
        var stsDiscoveryEndpoint = $"https://login.microsoftonline.com/{azureTenantId}/v2.0/.well-known/openid-configuration";
        services.AddSingleton(new ConfigurationManager<OpenIdConnectConfiguration>(stsDiscoveryEndpoint, new OpenIdConnectConfigurationRetriever()));
        services.AddScoped<IIdentityAccountRepository, IdentityAccountRepository>();
        services.AddScoped<ElectronicRegisterAPI.Domain.Interfaces.Services.ITimeProvider, ElectronicRegisterAPI.Infrastructure.Services.SystemTimeProvider>();
        services.AddScoped<ITransactionManager, TransactionManager>();
        // Preparatory MT-1 persistence. Not consumed by legacy auth/API/worker.
        services.AddScoped<IInstitutionRepository, InstitutionRepository>();
        services.AddScoped<ICampusRepository, CampusRepository>();
        services.AddScoped<IInstitutionMembershipRepository, InstitutionMembershipRepository>();
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IMicrosoftTokenValidator, MicrosoftTokenValidator>();
        return services;
    }
}
