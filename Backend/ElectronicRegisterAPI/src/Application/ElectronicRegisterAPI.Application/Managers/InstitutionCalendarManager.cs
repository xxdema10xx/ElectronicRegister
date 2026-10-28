using ElectronicRegisterAPI.Domain.Authorization;
using ElectronicRegisterAPI.Domain.Configuration;
using ElectronicRegisterAPI.Domain.DTOs.Calendar;
using ElectronicRegisterAPI.Domain.DTOs.Import;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Application.Services;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Application.Managers;

internal sealed class InstitutionCalendarManager(
    InstitutionSchoolAuthorization authorization,
    IInstitutionCalendarRepository repository,
    ICalendarImportFileReader reader,
    CalendarFeature feature
) : IInstitutionCalendarManager
{
    private async Task<TenantContext> Actor(CancellationToken ct, bool operational = true)
    {
        if (!feature.Enabled)
            throw new UnauthorizedAccessException();
        var t = await authorization.RequireAsync(ct);
        if (operational && !CalendarPermissions.Can(t.Roles, CalendarCapability.Manage))
            throw new UnauthorizedAccessException();
        return t;
    }

    public async Task<CalendarSettings> SettingsAsync(CancellationToken ct) => await repository.SettingsAsync(await Actor(ct), ct);
    public async Task<CalendarReceipt> ConfigureAsync(CalendarSettingsCommand r, CancellationToken ct) => await repository.ConfigureAsync(await Actor(ct), r, ct);
    public async Task<IReadOnlyList<CalendarLesson>> ListAsync(CalendarQuery q, string view, CancellationToken ct) => await repository.ListAsync(await Actor(ct, view == "admin"), q, view, ct);
    public async Task<CalendarLesson> GetAsync(Guid id, CancellationToken ct) => await repository.GetAsync(await Actor(ct, false), id, ct);
    public async Task<FormativeModuleSnapshot> ModuleAsync(Guid lessonId, CancellationToken ct) => await repository.ModuleAsync(await Actor(ct, false), lessonId, ct);
    public async Task<CalendarPeriod> PeriodAsync(
        Guid classId,
        int year,
        int month,
        CancellationToken ct
    ) => await repository.PeriodAsync(await Actor(ct), classId, year, month, ct);
    public async Task<CalendarReceipt> SaveAsync(Guid? id, CalendarSaveLesson r, CancellationToken ct) => await repository.SaveAsync(await Actor(ct), id, r, ct);
    public async Task<CalendarReceipt> PublishAsync(
        Guid classId,
        int year,
        int month,
        CalendarCommand r,
        CancellationToken ct
    ) => await repository.PublishAsync(await Actor(ct), classId, year, month, r, ct);
    public async Task<CalendarReceipt> TransitionAsync(
        Guid id,
        string operation,
        CalendarCommand r,
        CalendarDeliveredCommand? delivered,
        CalendarRescheduleCommand? reschedule,
        CalendarSubstituteCommand? substitute,
        CancellationToken ct
    ) => await repository.TransitionAsync(await Actor(ct, operation != "LessonCompleted"), id, operation, r, delivered, reschedule, substitute, ct);
    public async Task<IReadOnlyList<CalendarHistory>> HistoryAsync(
        Guid id,
        int page,
        int size,
        CancellationToken ct
    ) => await repository.HistoryAsync(await Actor(ct), id, page, size, ct);
    public async Task<CalendarHoursReport> HoursAsync(Guid classId, CancellationToken ct) => await repository.HoursAsync(await Actor(ct), classId, ct);
    public async Task<CalendarImportPreview> PreviewAsync(
        Stream file,
        string fileName,
        ImportParsingOptions parsing,
        CalendarImportSettings settings,
        CancellationToken ct
    )
    {
        var actor = await Actor(ct);
        var table = await reader.ReadCalendarAsync(file, fileName, parsing, new(), settings.Mapping, ct);
        return await repository.PreviewAsync(actor, table, settings, ct);
    }

    public async Task<CalendarImportPreview> ImportAsync(Guid id, CancellationToken ct) => await repository.ImportAsync(await Actor(ct), id, ct);
    public async Task<CalendarReceipt> ConfirmAsync(Guid id, CalendarConfirmImport r, CancellationToken ct) => await repository.ConfirmAsync(await Actor(ct), id, r, ct);
}
