using ElectronicRegisterAPI.Api.Security;
using ElectronicRegisterAPI.Api.ExceptionHandling;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ElectronicRegisterAPI.Api.Controllers;

namespace ElectronicRegisterAPI.Api.Filters;

public sealed class RequireTransferCourseApiAttribute() : TypeFilterAttribute(typeof(RequireTransferCourseApiFilter));
