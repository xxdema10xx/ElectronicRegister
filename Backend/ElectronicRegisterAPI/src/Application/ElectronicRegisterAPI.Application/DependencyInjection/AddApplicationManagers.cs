using Microsoft.Extensions.DependencyInjection;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Application.Managers;

namespace ElectronicRegisterAPI.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationManagers(this IServiceCollection services)
    {
        services.AddScoped<IAuthManager, AuthManager>();
        services.AddScoped<IGradeManager, GradeManager>();
        services.AddScoped<IStudentManager, StudentManager>();
        services.AddScoped<ISubjectManager, SubjectManager>();
        services.AddScoped<ITeacherManager, TeacherManager>();
        services.AddScoped<IUserManager, UserManager>();

        return services;
    }
}