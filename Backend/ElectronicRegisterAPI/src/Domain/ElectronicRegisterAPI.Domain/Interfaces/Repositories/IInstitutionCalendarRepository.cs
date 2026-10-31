using ElectronicRegisterAPI.Domain.DTOs.Calendar;
using ElectronicRegisterAPI.Domain.DTOs.Import;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface IInstitutionCalendarRepository
{
    Task<CalendarSettings> SettingsAsync(TenantContext actor, CancellationToken ct);
    Task<CalendarReceipt> ConfigureAsync(TenantContext actor, CalendarSettingsCommand command, CancellationToken ct);
    Task<IReadOnlyList<CalendarLesson>> ListAsync(
        TenantContext actor,
        CalendarQuery query,
        string view,
        CancellationToken ct
    );
    Task<CalendarLesson> GetAsync(TenantContext actor, Guid id, CancellationToken ct);
    Task<FormativeModuleSnapshot> ModuleAsync(TenantContext actor, Guid lessonId, CancellationToken ct);
    Task<CalendarPeriod> PeriodAsync(
        TenantContext actor,
        Guid classId,
        int year,
        int month,
        CancellationToken ct
    );
    Task<CalendarReceipt> SaveAsync(
        TenantContext actor,
        Guid? id,
        CalendarSaveLesson command,
        CancellationToken ct
    );
    Task<CalendarReceipt> PublishAsync(
        TenantContext actor,
        Guid classId,
        int year,
        int month,
        CalendarCommand command,
        CancellationToken ct
    );
    Task<CalendarReceipt> TransitionAsync(
        TenantContext actor,
        Guid id,
        string operation,
        CalendarCommand command,
        CalendarDeliveredCommand? delivered,
        CalendarRescheduleCommand? reschedule,
        CalendarSubstituteCommand? substitute,
        CancellationToken ct
    );
    Task<IReadOnlyList<CalendarHistory>> HistoryAsync(
        TenantContext actor,
        Guid id,
        int page,
        int size,
        CancellationToken ct
    );
    Task<CalendarHoursReport> HoursAsync(TenantContext actor, Guid classId, CancellationToken ct);
    Task<CalendarImportPreview> PreviewAsync(
        TenantContext actor,
        ImportTable table,
        CalendarImportSettings settings,
        CancellationToken ct
    );
    Task<CalendarImportPreview> ImportAsync(TenantContext actor, Guid id, CancellationToken ct);
    Task<CalendarReceipt> ConfirmAsync(
        TenantContext actor,
        Guid id,
        CalendarConfirmImport command,
        CancellationToken ct
    );
}
