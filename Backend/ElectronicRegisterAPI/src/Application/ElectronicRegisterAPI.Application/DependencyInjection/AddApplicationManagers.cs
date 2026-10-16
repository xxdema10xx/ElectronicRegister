using Microsoft.Extensions.DependencyInjection;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Application.Managers;

namespace ElectronicRegisterAPI.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationManagers(this IServiceCollection services)
    {
        services.AddScoped<IBienniumManager, BienniumManager>();
        services.AddScoped<IStudyAreaManager, StudyAreaManager>();
        services.AddScoped<IStudyPathManager, StudyPathManager>();
        services.AddScoped<IExamManager, ExamManager>();
        services.AddScoped<IExamSubmissionManager, ExamSubmissionManager>();
        services.AddScoped<IExamSubmissionProcessor, ElectronicRegisterAPI.Application.Services.ExamSubmissionProcessor>();
        services.AddScoped<IAuthManager, AuthManager>();
        services.AddScoped<IGradeManager, GradeManager>();
        services.AddScoped<IStudentManager, StudentManager>();
        services.AddScoped<ISubjectManager, SubjectManager>();
        services.AddScoped<ITeacherManager, TeacherManager>();
        services.AddScoped<IUserManager, UserManager>();

        return services;
    }
}