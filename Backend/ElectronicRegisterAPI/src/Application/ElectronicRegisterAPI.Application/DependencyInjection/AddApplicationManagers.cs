using Microsoft.Extensions.DependencyInjection;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Application.Managers;

namespace ElectronicRegisterAPI.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationManagers(this IServiceCollection services)
    {
        services.AddScoped<ElectronicRegisterAPI.Application.Services.InstitutionSchoolAuthorization>();
        services.AddScoped<IInstitutionCourseManager, InstitutionCourseManager>();
        services.AddScoped<IInstitutionMembershipAdminManager, InstitutionMembershipAdminManager>();
        services.AddScoped<ElectronicRegisterAPI.Domain.Models.IInstitutionOnboardingManager, InstitutionOnboardingManager>();
        services.AddScoped<ElectronicRegisterAPI.Domain.Models.IInstitutionGovernanceManager, InstitutionGovernanceManager>();
        services.AddScoped<ElectronicRegisterAPI.Domain.Models.IMembershipAuditManager, MembershipAuditManager>();
        services.AddScoped<IInstitutionStudentManager, InstitutionStudentManager>();
        Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddSingleton(services, new ElectronicRegisterAPI.Domain.Models.AcademicTransferOptions());
        Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddSingleton(services, TimeProvider.System);
        services.AddScoped<IInstitutionStudentTransferManager, InstitutionStudentTransferManager>();
        services.AddScoped<IInstitutionTeacherManager, InstitutionTeacherManager>();
        services.AddScoped<IInstitutionSubjectManager, InstitutionSubjectManager>();
        services.AddScoped<IInstitutionClassManager, InstitutionClassManager>();
        services.AddScoped<IInstitutionAcademicCatalogManager, InstitutionAcademicCatalogManager>();
        services.AddScoped<IInstitutionClassSubjectManager, InstitutionClassSubjectManager>();
        services.AddScoped<IInstitutionCampusManager, InstitutionCampusManager>();
        services.AddScoped<IInstitutionExamManager, InstitutionExamManager>();
        services.AddScoped<ElectronicRegisterAPI.Domain.Models.IInstitutionExpiryService, ElectronicRegisterAPI.Application.Services.InstitutionExpiryService>();
        services.AddScoped<IBienniumManager, BienniumManager>();
        services.AddScoped<IStudyAreaManager, StudyAreaManager>();
        services.AddScoped<IStudyPathManager, StudyPathManager>();
        services.AddScoped<IExamManager, ExamManager>();
        services.AddScoped<IExamSubmissionManager, ExamSubmissionManager>();
        services.AddScoped<IExamSubmissionProcessor, ElectronicRegisterAPI.Application.Services.ExamSubmissionProcessor>();
        services.AddScoped<IAuthManager, AuthManager>();
        services.AddScoped<IInstitutionAuthManager, InstitutionAuthManager>();
        services.AddScoped<IInstitutionProfileResolver, ElectronicRegisterAPI.Application.Services.InstitutionProfileResolver>();
        services.AddScoped<IGradeManager, GradeManager>();
        services.AddScoped<IStudentManager, StudentManager>();
        services.AddScoped<ISubjectManager, SubjectManager>();
        services.AddScoped<ITeacherManager, TeacherManager>();
        services.AddScoped<IUserManager, UserManager>();

        return services;
    }
}
