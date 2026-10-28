using ElectronicRegisterAPI.Domain.DTOs.Import;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Calendar;

public sealed record CalendarImportRow(
    int RowNumber,
    CalendarLessonInput? Lesson,
    string? ClassName,
    string? ModuleName,
    string? TeacherName,
    int? DurationMinutes,
    IReadOnlyList<ImportIssue> Issues,
    IReadOnlyList<CalendarConflict> Conflicts,
    Guid PreviewLessonId,
    string? ClassReference = null,
    string? ModuleReference = null,
    string? TeacherReference = null
);
