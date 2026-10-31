using ElectronicRegisterAPI.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace ElectronicRegisterAPI.Api.ExceptionHandling;

// SQL conflict translation is confined to the new administrative surface.
public sealed class CourseApiErrorsAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        var ex = context.Exception;
        if (ex is CourseConflictException or DbUpdateConcurrencyException || ex is DbUpdateException { InnerException: MySqlException { Number: 1062 or 1451 or 1452 or 1644 } } || ex is MySqlException { Number: 1062 or 1451 or 1452 or 1644 })
        {
            context.Result = new ObjectResult(new ProblemDetails { Status = 409, Title = "Conflitto sulla struttura formativa" })
            {
                StatusCode = 409
            };
            context.ExceptionHandled = true;
        }
    }
}
