using ElectronicRegisterAPI.Domain.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
namespace ElectronicRegisterAPI.Api.ExceptionHandling;
public sealed class CalendarErrorsAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        string? code=null;object? details=null;var status=400;
        if(context.Exception is CalendarConflictException conflict){code=conflict.Code;details=conflict.Details;status=409;}
        else if(context.Exception is CalendarValidationException validation){code=validation.Code;if(code.Contains("revision")||code.Contains("required")&&code is not("calendar.minute-precision-required" or "calendar.reason-required"))status=409;}
        else if(context.Exception is ImportInputException input){code=input.Code;status=code=="FileTooLarge"?413:400;}
        if(code is null)return;
        var problem=new ProblemDetails{Status=status,Title=code};problem.Extensions["code"]=code;if(details is not null)problem.Extensions["details"]=details;
        context.Result=new ObjectResult(problem){StatusCode=status};context.ExceptionHandled=true;
    }
}
