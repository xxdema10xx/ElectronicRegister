using ElectronicRegisterAPI.Domain.DTOs.Calendar;
using ElectronicRegisterAPI.Domain.DTOs.Import;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Managers;

public interface IInstitutionCalendarManager
{
    Task<CalendarSettings> SettingsAsync(CancellationToken ct);
    Task<CalendarReceipt> ConfigureAsync(CalendarSettingsCommand command, CancellationToken ct);
    Task<IReadOnlyList<CalendarLesson>> ListAsync(CalendarQuery query, string view, CancellationToken ct);
    Task<CalendarLesson> GetAsync(Guid id, CancellationToken ct);
    Task<FormativeModuleSnapshot> ModuleAsync(Guid lessonId, CancellationToken ct);
    Task<CalendarPeriod> PeriodAsync(
        Guid classId,
        int year,
        int month,
        CancellationToken ct
    );
    Task<CalendarReceipt> SaveAsync(Guid? id, CalendarSaveLesson command, CancellationToken ct);
    Task<CalendarReceipt> PublishAsync(
        Guid classId,
        int year,
        int month,
        CalendarCommand command,
        CancellationToken ct
    );
    Task<CalendarReceipt> TransitionAsync(
        Guid id,
        string operation,
        CalendarCommand command,
        CalendarDeliveredCommand? delivered,
        CalendarRescheduleCommand? reschedule,
        CalendarSubstituteCommand? substitute,
        CancellationToken ct
    );
    Task<IReadOnlyList<CalendarHistory>> HistoryAsync(
        Guid id,
        int page,
        int size,
        CancellationToken ct
    );
    Task<CalendarHoursReport> HoursAsync(Guid classId, CancellationToken ct);
    Task<CalendarImportPreview> PreviewAsync(
        Stream file,
        string fileName,
        ImportParsingOptions parsing,
        CalendarImportSettings settings,
        CancellationToken ct
    );
    Task<CalendarImportPreview> ImportAsync(Guid id, CancellationToken ct);
    Task<CalendarReceipt> ConfirmAsync(Guid id, CalendarConfirmImport command, CancellationToken ct);
}
