using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace ElectronicRegisterAPI.Api.ExceptionHandling;

// Scope SQL concurrency translation to attributed IAM/invitation actions;
// unexpected failures stay opaque 500. No automatic mutation retry.
public sealed class MembershipApiErrorsAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        var ex = context.Exception;
        if (ex is DbUpdateConcurrencyException || ex is MySqlException { Number: 1062 or 1205 or 1213 } || ex is DbUpdateException { InnerException: MySqlException { Number: 1062 or 1205 or 1213 } })
        {
            context.Result = new ObjectResult(new ProblemDetails { Status = 409, Title = "Conflitto sulla membership istituzionale" })
            {
                StatusCode = 409
            };
            context.ExceptionHandled = true;
        }
    }
}
