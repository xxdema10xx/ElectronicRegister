using ElectronicRegisterAPI.Api.ExceptionHandling;

namespace ElectronicRegisterAPI.Api.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();
        services.AddHttpContextAccessor();
        services.AddScoped<ElectronicRegisterAPI.Api.Security.LegacyEndpointGate>();
        services.Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(options =>
            options.Filters.AddService<ElectronicRegisterAPI.Api.Security.LegacyEndpointGate>());
        // Background worker for expired submissions
        services.AddHostedService<ElectronicRegisterAPI.Api.BackgroundServices.ExpiredExamSubmissionWorker>();

        return services;
    }
}
