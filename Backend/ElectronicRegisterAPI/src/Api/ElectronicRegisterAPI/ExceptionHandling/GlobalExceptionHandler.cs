using ElectronicRegisterAPI.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ElectronicRegisterAPI.Api.ExceptionHandling;

internal class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge } =>
                (StatusCodes.Status413PayloadTooLarge, "Richiesta troppo grande"),

            KeyNotFoundException => 
                (StatusCodes.Status404NotFound, "La risorsa non esiste"),

            ArgumentException =>
                (StatusCodes.Status400BadRequest, "Richiesta non valida"),

            UnauthorizedAccessException =>
                (httpContext.User.Identity?.IsAuthenticated == true ? StatusCodes.Status403Forbidden : StatusCodes.Status401Unauthorized, "Non autorizzato"),

            InvalidOperationException when SubmissionErrors.IsInvalidRequest(exception) =>
                (StatusCodes.Status400BadRequest, "Richiesta non valida"),

            InvalidOperationException when SubmissionErrors.IsConflict(exception) =>
                (StatusCodes.Status409Conflict, "Operazione non consentita"),

            BusinessRuleException =>
                (StatusCodes.Status409Conflict, "Operazione non consentita"),

            MembershipConflictException =>
                (StatusCodes.Status409Conflict, "Conflitto sulla membership istituzionale"),

            _ =>
                (StatusCodes.Status500InternalServerError, "Errore interno del server")
        };

        if (statusCode == 500) logger.LogError("Request failed ({ErrorType}).", exception.GetType().Name);
        httpContext.Response.StatusCode = statusCode;

        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = statusCode == StatusCodes.Status500InternalServerError
                    ? "Si è verificato un errore interno."
                    : title
            },
            cancellationToken);

        return true;
    }
}
