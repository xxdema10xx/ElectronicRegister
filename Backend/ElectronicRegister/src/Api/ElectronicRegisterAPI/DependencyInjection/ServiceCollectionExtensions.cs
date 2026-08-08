using ElectronicRegisterAPI.Api.ExceptionHandling;
using Microsoft.Extensions.DependencyInjection;

namespace ElectronicRegisterAPI.Api.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        return services;
    }
}