// Api/ExceptionHandling/GlobalExceptionHandler.cs
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ElectronicRegisterAPI.Api.ExceptionHandling;

internal class GlobalExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            ArgumentException => (StatusCodes.Status400BadRequest, "Richiesta non valida"),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Non autorizzato"),
            InvalidOperationException => (StatusCodes.Status409Conflict, "Operazione non consentita"),
            _ => (StatusCodes.Status500InternalServerError, "Errore interno del server")
        };

        httpContext.Response.StatusCode = statusCode;

        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = exception.Message
        }, cancellationToken);

        return true;
    }
}
