using ElectronicRegisterAPI.Api.Security;
using ElectronicRegisterAPI.Api.ExceptionHandling;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ElectronicRegisterAPI.Api.Controllers;

namespace ElectronicRegisterAPI.Api.ExceptionHandling;

public sealed class StudentTransferErrorsAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is ElectronicRegisterAPI.Domain.Exceptions.BusinessRuleException error)
        {
            context.Result = new ObjectResult(new ProblemDetails { Status = 409, Title = "Trasferimento non consentito", Detail = error.Message })
            {
                StatusCode = 409
            };
            context.ExceptionHandled = true;
        }
    }
}
