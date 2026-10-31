using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Calendar;

// Public lesson projection deliberately excludes actor IDs and administrative reasons.
public sealed record CalendarLesson(
    Guid Id,
    Guid PeriodId,
    Guid ClassId,
    Guid CourseEditionId,
    Guid VersionId,
    Guid ModuleId,
    Guid SubjectId,
    Guid ClassSubjectId,
    Guid OriginalTeacherId,
    Guid TeacherId,
    DateOnly Date,
    TimeOnly StartsAt,
    TimeOnly EndsAt,
    string TimeZoneId,
    DateTime StartUtc,
    DateTime EndUtc,
    string Publication,
    string Status,
    long Revision,
    int? DeliveredMinutes,
    Guid? RecoveryOfLessonId,
    string? Room,
    string? Description,
    string? ExternalReference,
    string? ClassName = null,
    string? ModuleName = null,
    string? TeacherName = null
)
{
    public int DurationMinutes => checked((int)(EndUtc - StartUtc).TotalMinutes);
}
