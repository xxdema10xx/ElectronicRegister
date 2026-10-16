using ElectronicRegisterAPI.Api.ExceptionHandling;

namespace ElectronicRegisterAPI.Api.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();
        // Background worker for expired submissions
        services.AddHostedService<ElectronicRegisterAPI.Api.BackgroundServices.ExpiredExamSubmissionWorker>();

        return services;
    }
}