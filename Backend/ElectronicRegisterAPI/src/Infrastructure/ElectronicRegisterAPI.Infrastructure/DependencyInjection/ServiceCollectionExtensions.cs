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
        services.AddSingleton(new ElectronicRegisterAPI.Domain.Models.CalendarFeature(() =>
            bool.TryParse(configuration["Multitenancy:InstitutionCalendar:Enabled"],out var calendar) && calendar &&
            bool.TryParse(configuration["Multitenancy:SchoolApi:Enabled"],out var calendarSchool) && calendarSchool));
        services.AddScoped<ElectronicRegisterAPI.Domain.Models.IInstitutionCalendarRepository, InstitutionCalendarRepository>();
        services.AddScoped<ElectronicRegisterAPI.Domain.Models.ICalendarImportFileReader, ElectronicRegisterAPI.Infrastructure.Services.InstitutionImportFileReader>();
        services.TryAddSingleton<ElectronicRegisterAPI.Domain.Models.ICalendarFaultObserver, ElectronicRegisterAPI.Domain.Models.NoCalendarFault>();
        services.AddSingleton(new ElectronicRegisterAPI.Domain.Models.FormativePlanFeature(() =>
            bool.TryParse(configuration["Multitenancy:FormativePlans:Enabled"],out var enabled) && enabled &&
            bool.TryParse(configuration["Multitenancy:SchoolApi:Enabled"],out var school) && school &&
            bool.TryParse(configuration["Multitenancy:CourseApi:Enabled"],out var course) && course));
        services.AddScoped<ElectronicRegisterAPI.Domain.Models.IInstitutionFormativePlanRepository, InstitutionFormativePlanRepository>();
        services.TryAddSingleton<ElectronicRegisterAPI.Domain.Models.IFormativePlanFaultObserver, ElectronicRegisterAPI.Domain.Models.NoFormativePlanFault>();
        services.AddScoped<ElectronicRegisterAPI.Domain.Models.IInstitutionImportFileReader, ElectronicRegisterAPI.Infrastructure.Services.InstitutionImportFileReader>();
        int ImportNumber(string key,int fallback)=>configuration["Multitenancy:InstitutionImport:"+key] is {} raw?int.Parse(raw,System.Globalization.CultureInfo.InvariantCulture):fallback;
        var importFeature=new ElectronicRegisterAPI.Domain.Models.InstitutionImportFeature(
            bool.TryParse(configuration["Multitenancy:InstitutionImport:Enabled"],out var importOn)&&importOn,
            bool.TryParse(configuration["Multitenancy:SchoolApi:Enabled"],out var schoolOn)&&schoolOn,
            new(ImportNumber("Limits:MaxFileBytes",10*1024*1024),ImportNumber("Limits:MaxDataRows",2000),ImportNumber("Limits:MaxColumns",16),ImportNumber("Limits:MaxCellCharacters",512),ImportNumber("Limits:MaxRecordCharacters",8192),ImportNumber("Limits:MaxZipEntries",128),ImportNumber("Limits:MaxUncompressedBytes",40*1024*1024),ImportNumber("Limits:MaxXmlNodes",200000)),
            ImportNumber("TtlMinutes",30),ImportNumber("MaxStageBytes",2*1024*1024),ImportNumber("MaxPendingPreviews",20))
            {CurrentAvailability=()=>bool.TryParse(configuration["Multitenancy:InstitutionImport:Enabled"],out var currentImport)&&currentImport&&bool.TryParse(configuration["Multitenancy:SchoolApi:Enabled"],out var currentSchool)&&currentSchool};
        importFeature.Validate();services.AddSingleton(importFeature);
        services.AddScoped<ElectronicRegisterAPI.Domain.Models.IInstitutionImportRepository, InstitutionImportRepository>();
        services.AddScoped<ElectronicRegisterAPI.Domain.Models.IInstitutionImportRetention, ElectronicRegisterAPI.Infrastructure.Services.InstitutionImportRetention>();
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<ElectronicRegisterContext>(options =>
        {
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
            if (TestDatabaseSession.Enabled) options.AddInterceptors(new TestDatabaseSession());
        });

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<PlatformAuthorityOptions>(configuration.GetSection("PlatformAuthority"));
        services.AddScoped<ElectronicRegisterAPI.Domain.Models.IPlatformAuthority, PlatformAuthority>();
        services.AddScoped<ElectronicRegisterAPI.Domain.Models.IRecipientIdentity, RecipientIdentity>();
        services.AddScoped<ElectronicRegisterAPI.Domain.Models.IOnboardingRepository, InstitutionOnboardingRepository>();
        services.AddScoped<ElectronicRegisterAPI.Domain.Models.IInstitutionGovernanceRepository, InstitutionOnboardingRepository>();
        services.AddScoped<ElectronicRegisterAPI.Domain.Models.IMembershipAuditRepository, MembershipAuditRepository>();
        services.AddSingleton<ElectronicRegisterAPI.Domain.Models.IInvitationChannel, ClosedInvitationChannel>();
        services.AddOwnerInstalledInvitationChannel(configuration);
        services.AddSingleton<ElectronicRegisterAPI.Domain.Models.IOnboardingFaultObserver, NoOnboardingFault>();
        services.Configure<AzureAdOptions>(configuration.GetSection(AzureAdOptions.SectionName));
        services.Configure<LegacyIdentityOptions>(configuration.GetSection("Identity:Legacy"));
        services.AddOptions<LegacyIdentityOptions>()
            .Validate(o => !o.Enabled || (o.InstitutionId != Guid.Empty && o.UserIds.Length > 0 &&
                o.UserIds.All(id => id != Guid.Empty) && o.UserIds.Distinct().Count() == o.UserIds.Length),
                "Legacy auth requires an explicit institution and nonempty unique user allowlist.")
            .ValidateOnStart();
        services.Configure<MicrosoftIdentityOptions>(configuration.GetSection("Identity:Microsoft"));
        services.AddScoped<IMembershipAuthorizationRepository, MembershipAuthorizationRepository>();
        services.AddScoped<IInstitutionMembershipDirectoryRepository, InstitutionMembershipDirectoryRepository>();
        services.AddScoped<IInstitutionMembershipMutationRepository, InstitutionMembershipMutationRepository>();
        services.AddScoped<IInstitutionMembershipSecurityStateRepository, InstitutionMembershipSecurityStateRepository>();
        services.AddScoped<IInstitutionProfileRepository, InstitutionProfileRepository>();
        services.AddScoped<InstitutionSchoolQueries>();
        services.AddSingleton(new ElectronicRegisterAPI.Domain.Models.InstitutionEmailFeature(
            bool.TryParse(configuration["Multitenancy:InstitutionEmail:Enabled"], out var emailEnabled) && emailEnabled));
        services.AddScoped<ElectronicRegisterAPI.Domain.Models.IInstitutionEmailRepository, InstitutionEmailRepository>();
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
        services.AddScoped<ElectronicRegisterAPI.Domain.Models.ISubmissionExpiryStore, SubmissionExpiryStore>();
        services.AddScoped<IInstitutionTokens, InstitutionTokens>();
        services.AddScoped<ILegacyAccess, LegacyAccess>();
        services.AddScoped<IMicrosoftIdentityLink, MicrosoftIdentityLink>();

        var azureTenantId = configuration["AzureAd:TenantId"];
        var stsDiscoveryEndpoint = $"https://login.microsoftonline.com/{azureTenantId}/v2.0/.well-known/openid-configuration";
        services.AddSingleton(new ConfigurationManager<OpenIdConnectConfiguration>(
            stsDiscoveryEndpoint, new OpenIdConnectConfigurationRetriever()));

        services.AddScoped<IBienniumRepository, BienniumRepository>();
        services.AddScoped<IStudyAreaRepository, StudyAreaRepository>();
        services.AddScoped<IStudyPathRepository, StudyPathRepository>();
        services.AddScoped<IGradeRepository, GradeRepository>();
        services.AddScoped<IStudentRepository, StudentRepository>();
        services.AddScoped<ISubjectRepository, SubjectRepository>();
        services.AddScoped<IClassSubjectRepository, ClassSubjectRepository>();
        services.AddScoped<ITeacherRepository, TeacherRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IExamRepository, ExamRepository>();
        services.AddScoped<IExamSubmissionRepository, ExamSubmissionRepository>();
        services.AddScoped<ElectronicRegisterAPI.Domain.Interfaces.Services.ITimeProvider, ElectronicRegisterAPI.Infrastructure.Services.SystemTimeProvider>();
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<ITransactionManager, TransactionManager>();
        // Preparatory MT-1 persistence. Not consumed by legacy auth/API/worker.
        services.AddScoped<IInstitutionRepository, InstitutionRepository>();
        services.AddScoped<ICampusRepository, CampusRepository>();
        services.AddScoped<IInstitutionMembershipRepository, InstitutionMembershipRepository>();
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IMicrosoftTokenValidator, MicrosoftTokenValidator>();

        return services;
    }
}
