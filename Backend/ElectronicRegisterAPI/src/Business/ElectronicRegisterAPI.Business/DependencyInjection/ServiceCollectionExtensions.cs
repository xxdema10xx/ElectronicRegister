using Microsoft.Extensions.DependencyInjection;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Business.Services;

namespace ElectronicRegisterAPI.Business.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBusinessServices(this IServiceCollection services)
    {
        services.AddSingleton<ElectronicRegisterAPI.Domain.Interfaces.Services.ICalendarImportRules, CalendarImportRules>();
        services.AddSingleton<ElectronicRegisterAPI.Domain.Interfaces.Services.IInstitutionEmailGenerator, InstitutionEmailGenerator>();
        services.AddSingleton<ElectronicRegisterAPI.Domain.Interfaces.Services.IFormativeDraftBuilder, FormativeDraftBuilder>();
        services.AddSingleton<ElectronicRegisterAPI.Domain.Interfaces.Services.IInstitutionImportRules, InstitutionImportRules>();
        services.AddScoped<IBienniumService, BienniumService>();
        services.AddScoped<IStudyAreaService, StudyAreaService>();
        services.AddScoped<IStudyPathService, StudyPathService>();
        services.AddScoped<IGradeService, GradeService>();
        services.AddScoped<IStudentService, StudentService>();
        services.AddScoped<ISubjectService, SubjectService>();
        services.AddScoped<ITeacherService, TeacherService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IExamService, ExamService>();
        services.AddScoped<ISubmissionGradingService, SubmissionGradingService>();
        return services;
    }
}
