using ElectronicRegisterAPI.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
namespace ElectronicRegisterAPI.Api.ExceptionHandling;

public sealed class FormativePlanErrorsAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        if(context.Exception is not FormativePublicationException failure)return;
        var details=new ProblemDetails{Status=409,Title="Piano formativo non pubblicabile"};
        details.Extensions["hourSummary"]=failure.Summary;
        context.Result=new ObjectResult(details){StatusCode=409};context.ExceptionHandled=true;
    }
}
