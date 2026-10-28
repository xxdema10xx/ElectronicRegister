using System.Text.Json;
using ElectronicRegisterAPI.Api.Security;
using ElectronicRegisterAPI.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectronicRegisterAPI.Api.Controllers;

[ApiController,InstitutionSchoolEndpoint,ElectronicRegisterAPI.Api.ExceptionHandling.CalendarErrors,
 Route("api/institution/calendar"),RequestSizeLimit(12*1024*1024)]
public sealed class InstitutionCalendarController(IInstitutionCalendarManager manager) : ControllerBase
{
    [HttpGet("settings"),Authorize(Roles="InstitutionAdmin,Staff")]
    [ProducesResponseType(typeof(CalendarSettings),200)]
    public async Task<IActionResult> Settings(CancellationToken ct)=>Ok(await manager.SettingsAsync(ct));
    [HttpPut("settings"),Authorize(Roles="InstitutionAdmin,Staff")]
    [ProducesResponseType(typeof(CalendarReceipt),200)]
    public async Task<IActionResult> Configure(CalendarSettingsCommand request,CancellationToken ct)=>Ok(await manager.ConfigureAsync(request,ct));
    [HttpGet,Authorize(Roles="InstitutionAdmin,Staff")]
    [ProducesResponseType(typeof(CalendarLesson[]),200)]
    public async Task<IActionResult> List([FromQuery] CalendarQuery query,CancellationToken ct)=>Ok(await manager.ListAsync(query,"admin",ct));
    [HttpGet("classes/{classId:guid}"),Authorize(Roles="InstitutionAdmin,Staff")]
    [ProducesResponseType(typeof(CalendarLesson[]),200)]
    public async Task<IActionResult> Class(Guid classId,[FromQuery] CalendarQuery query,CancellationToken ct)=>Ok(await manager.ListAsync(query with{ClassId=classId},"admin",ct));
    [HttpGet("teachers/me"),Authorize(Roles="Teacher")]
    [ProducesResponseType(typeof(CalendarLesson[]),200)]
    public async Task<IActionResult> Teacher([FromQuery] CalendarQuery query,CancellationToken ct)=>Ok(await manager.ListAsync(query,"teacher",ct));
    [HttpGet("students/me"),Authorize(Roles="Student")]
    [ProducesResponseType(typeof(CalendarLesson[]),200)]
    public async Task<IActionResult> Student([FromQuery] CalendarQuery query,CancellationToken ct)=>Ok(await manager.ListAsync(query,"student",ct));
    [HttpGet("months/{year:int}/{month:int}/classes/{classId:guid}"),Authorize(Roles="InstitutionAdmin,Staff")]
    [ProducesResponseType(typeof(CalendarPeriod),200)]
    public async Task<IActionResult> Period(Guid classId,int year,int month,CancellationToken ct)=>Ok(await manager.PeriodAsync(classId,year,month,ct));
    [HttpPost("months/{year:int}/{month:int}/classes/{classId:guid}/publish"),Authorize(Roles="InstitutionAdmin,Staff")]
    [ProducesResponseType(typeof(CalendarReceipt),200)]
    public async Task<IActionResult> Publish(Guid classId,int year,int month,CalendarCommand request,CancellationToken ct)=>Ok(await manager.PublishAsync(classId,year,month,request,ct));
    [HttpGet("lessons/{id:guid}"),Authorize(Roles="InstitutionAdmin,Staff,Teacher,Student")]
    [ProducesResponseType(typeof(CalendarLesson),200)]
    public async Task<IActionResult> Lesson(Guid id,CancellationToken ct)=>Ok(await manager.GetAsync(id,ct));
    [HttpGet("lessons/{id:guid}/module"),Authorize(Roles="InstitutionAdmin,Staff,Teacher,Student")]
    [ProducesResponseType(typeof(FormativeModuleSnapshot),200)]
    public async Task<IActionResult> Module(Guid id,CancellationToken ct)=>Ok(await manager.ModuleAsync(id,ct));
    [HttpPost("lessons"),Authorize(Roles="InstitutionAdmin,Staff")]
    [ProducesResponseType(typeof(CalendarReceipt),200)]
    public async Task<IActionResult> Create(CalendarSaveLesson request,CancellationToken ct)=>Ok(await manager.SaveAsync(null,request,ct));
    [HttpPut("lessons/{id:guid}"),Authorize(Roles="InstitutionAdmin,Staff")]
    [ProducesResponseType(typeof(CalendarReceipt),200)]
    public async Task<IActionResult> Update(Guid id,CalendarSaveLesson request,CancellationToken ct)=>Ok(await manager.SaveAsync(id,request,ct));
    [HttpPost("lessons/{id:guid}/cancel"),Authorize(Roles="InstitutionAdmin,Staff")]
    [ProducesResponseType(typeof(CalendarReceipt),200)]
    public async Task<IActionResult> Cancel(Guid id,CalendarCommand request,CancellationToken ct)=>Ok(await manager.TransitionAsync(id,"LessonCancelled",request,null,null,null,ct));
    [HttpPost("lessons/{id:guid}/reschedule"),Authorize(Roles="InstitutionAdmin,Staff")]
    [ProducesResponseType(typeof(CalendarReceipt),200)]
    public async Task<IActionResult> Reschedule(Guid id,CalendarRescheduleCommand request,CancellationToken ct)=>Ok(await manager.TransitionAsync(id,"LessonRescheduled",new(request.EventId,request.ExpectedRevision,request.Reason),null,request,null,ct));
    [HttpPost("lessons/{id:guid}/substitute"),Authorize(Roles="InstitutionAdmin,Staff")]
    [ProducesResponseType(typeof(CalendarReceipt),200)]
    public async Task<IActionResult> Substitute(Guid id,CalendarSubstituteCommand request,CancellationToken ct)=>Ok(await manager.TransitionAsync(id,"TeacherSubstituted",new(request.EventId,request.ExpectedRevision,request.Reason),null,null,request,ct));
    [HttpPost("lessons/{id:guid}/complete"),Authorize(Roles="InstitutionAdmin,Staff,Teacher")]
    [ProducesResponseType(typeof(CalendarReceipt),200)]
    public async Task<IActionResult> Complete(Guid id,CalendarDeliveredCommand request,CancellationToken ct)=>Ok(await manager.TransitionAsync(id,"LessonCompleted",new(request.EventId,request.ExpectedRevision,request.Reason),request,null,null,ct));
    [HttpPost("lessons/{id:guid}/correct-delivered-time"),Authorize(Roles="InstitutionAdmin,Staff")]
    [ProducesResponseType(typeof(CalendarReceipt),200)]
    public async Task<IActionResult> Correct(Guid id,CalendarDeliveredCommand request,CancellationToken ct)=>Ok(await manager.TransitionAsync(id,"DeliveredMinutesCorrected",new(request.EventId,request.ExpectedRevision,request.Reason),request,null,null,ct));
    [HttpGet("lessons/{id:guid}/history"),Authorize(Roles="InstitutionAdmin,Staff")]
    [ProducesResponseType(typeof(CalendarHistory[]),200)]
    public async Task<IActionResult> History(Guid id,CancellationToken ct,int page=1,int size=20)=>Ok(await manager.HistoryAsync(id,page,size,ct));
    [HttpGet("hours"),Authorize(Roles="InstitutionAdmin,Staff")]
    [ProducesResponseType(typeof(CalendarHoursReport),200)]
    public async Task<IActionResult> Hours(Guid classId,CancellationToken ct)=>Ok(await manager.HoursAsync(classId,ct));
    [HttpPost("imports/preview"),Authorize(Roles="InstitutionAdmin,Staff"),RequestFormLimits(MultipartBodyLengthLimit=12*1024*1024)]
    [ProducesResponseType(typeof(CalendarImportPreview),200)]
    public async Task<IActionResult> Preview([FromForm] IFormFile file,[FromForm] string settings,[FromForm] string? parsing,CancellationToken ct)
    {
        if(Request.Form.Files.Count!=1||Request.Form.Keys.Any(k=>k is not("settings" or "parsing")))return BadRequest();
        CalendarImportSettings options;ImportParsingOptions parse;
        try{var json=new JsonSerializerOptions(JsonSerializerDefaults.Web);options=JsonSerializer.Deserialize<CalendarImportSettings>(settings,json)??throw new JsonException();parse=parsing is null?new():JsonSerializer.Deserialize<ImportParsingOptions>(parsing,json)??throw new JsonException();}
        catch(JsonException){return BadRequest(new{code="calendar.invalid-import-settings"});}
        await using var stream=file.OpenReadStream();return Ok(await manager.PreviewAsync(stream,file.FileName,parse,options,ct));
    }
    [HttpGet("imports/{id:guid}"),Authorize(Roles="InstitutionAdmin,Staff")]
    [ProducesResponseType(typeof(CalendarImportPreview),200)]
    public async Task<IActionResult> Import(Guid id,CancellationToken ct)=>Ok(await manager.ImportAsync(id,ct));
    [HttpPost("imports/{id:guid}/confirm"),Authorize(Roles="InstitutionAdmin,Staff")]
    [ProducesResponseType(typeof(CalendarReceipt),200)]
    public async Task<IActionResult> Confirm(Guid id,CalendarConfirmImport request,CancellationToken ct)=>Ok(await manager.ConfirmAsync(id,request,ct));
}
