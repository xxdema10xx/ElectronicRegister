using ElectronicRegisterAPI.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ElectronicRegisterAPI.Api.ExceptionHandling;

internal class GlobalExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            KeyNotFoundException => 
                (StatusCodes.Status404NotFound, "La risorsa non esiste"),

            ArgumentException =>
                (StatusCodes.Status400BadRequest, "Richiesta non valida"),

            UnauthorizedAccessException =>
                (StatusCodes.Status403Forbidden, "Non autorizzato"),

            BusinessRuleException =>
                (StatusCodes.Status409Conflict, "Operazione non consentita"),

            _ =>
                (StatusCodes.Status500InternalServerError, "Errore interno del server")
        };

        httpContext.Response.StatusCode = statusCode;

        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = statusCode == StatusCodes.Status500InternalServerError
                    ? "Si è verificato un errore interno."
                    : exception.Message
            },
            cancellationToken);

        return true;
    }
}