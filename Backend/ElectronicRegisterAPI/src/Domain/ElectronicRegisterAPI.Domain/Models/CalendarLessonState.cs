using ElectronicRegisterAPI.Domain.Enums;
using System.Globalization;

namespace ElectronicRegisterAPI.Domain.Models;

// Immutable state for one canonical lesson. Class/module budgets belong to the published plan.
public sealed record CalendarLessonState(
    Guid Id,
    Guid InstitutionId,
    Guid CourseEditionId,
    Guid ClassId,
    Guid PlanVersionId,
    Guid ModuleId,
    Guid SubjectId,
    Guid OriginalTeacherId,
    Guid ScheduledTeacherId,
    CalendarInterval Interval,
    CalendarPublicationStatus Publication,
    CalendarLessonStatus Status,
    long Revision,
    int? DeliveredMinutes = null,
    Guid? RecoveryOfLessonId = null,
    string? SubstitutionReason = null
);
