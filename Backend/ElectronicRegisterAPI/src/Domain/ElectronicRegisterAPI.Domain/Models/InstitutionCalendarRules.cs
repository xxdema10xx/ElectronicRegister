using System.Globalization;

namespace ElectronicRegisterAPI.Domain.Models;

// Calendar values are independent of persistence and never authorize an HTTP operation.
public sealed class CalendarValidationException(string code) : ArgumentException(code)
{
    public string Code { get; } = code;
}

public sealed record CalendarInterval
{
    public DateOnly LocalDate { get; }
    public TimeOnly StartsAt { get; }
    public TimeOnly EndsAt { get; }
    public string TimeZoneId { get; }
    public DateTime StartUtc { get; }
    public DateTime EndUtc { get; }
    public int DurationMinutes { get; }

    private CalendarInterval(DateOnly date, TimeOnly start, TimeOnly end, string zone, DateTime startUtc, DateTime endUtc)
    {
        LocalDate = date; StartsAt = start; EndsAt = end; TimeZoneId = zone;
        StartUtc = startUtc; EndUtc = endUtc;
        DurationMinutes = checked((int)((endUtc.Ticks - startUtc.Ticks) / TimeSpan.TicksPerMinute));
    }

    public static CalendarInterval Create(DateOnly date, TimeOnly start, TimeOnly end, string? timeZoneId)
    {
        if (start.Ticks % TimeSpan.TicksPerMinute != 0 || end.Ticks % TimeSpan.TicksPerMinute != 0)
            throw new CalendarValidationException("calendar.minute-precision-required");
        if (end <= start) throw new CalendarValidationException("calendar.same-day-positive-interval-required");
        var zone = InstitutionCalendarRules.TimeZone(timeZoneId);
        var localStart = date.ToDateTime(start, DateTimeKind.Unspecified);
        var localEnd = date.ToDateTime(end, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(localStart) || zone.IsInvalidTime(localEnd))
            throw new CalendarValidationException("calendar.nonexistent-local-time");
        if (zone.IsAmbiguousTime(localStart) || zone.IsAmbiguousTime(localEnd))
            throw new CalendarValidationException("calendar.ambiguous-local-time");

        // Reject intervals crossing an ambiguous or nonexistent local minute as well.
        // This version deliberately has no offset-selection workflow.
        for (var minute = localStart; minute < localEnd; minute = minute.AddMinutes(1))
        {
            if (zone.IsInvalidTime(minute)) throw new CalendarValidationException("calendar.nonexistent-local-time");
            if (zone.IsAmbiguousTime(minute)) throw new CalendarValidationException("calendar.ambiguous-local-time");
        }
        var utcStart = TimeZoneInfo.ConvertTimeToUtc(localStart, zone);
        var utcEnd = TimeZoneInfo.ConvertTimeToUtc(localEnd, zone);
        if (utcEnd <= utcStart || (utcEnd.Ticks - utcStart.Ticks) % TimeSpan.TicksPerMinute != 0)
            throw new CalendarValidationException("calendar.invalid-utc-duration");
        return new(date, start, end, zone.Id, utcStart, utcEnd);
    }

    public bool Overlaps(CalendarInterval other) => StartUtc < other.EndUtc && other.StartUtc < EndUtc;
}

public enum CalendarLessonStatus { Scheduled, Cancelled, Completed }
public enum CalendarPublicationStatus { Draft, Published }
[System.Text.Json.Serialization.JsonConverter(typeof(ImportCatalogConverter<CalendarImportMode>))]
public enum CalendarImportMode { AppendDraft, ReplaceDraftScope }
public enum CalendarConflictKind { Duplicate, ClassOverlap, TeacherOverlap }
public sealed record CalendarConflict(Guid FirstLessonId, Guid SecondLessonId, CalendarConflictKind Kind);

// Immutable state for one canonical lesson. Class/module budgets belong to the published plan.
public sealed record CalendarLessonState(Guid Id, Guid InstitutionId, Guid CourseEditionId, Guid ClassId,
    Guid PlanVersionId, Guid ModuleId, Guid SubjectId, Guid OriginalTeacherId, Guid ScheduledTeacherId,
    CalendarInterval Interval, CalendarPublicationStatus Publication, CalendarLessonStatus Status,
    long Revision, int? DeliveredMinutes = null, Guid? RecoveryOfLessonId = null, string? SubstitutionReason = null);

public sealed record CalendarModuleHours(Guid ClassId, Guid ModuleId, long PlannedMinutes,
    long ScheduledMinutes, long DeliveredMinutes)
{
    public long RemainingToScheduleMinutes => Math.Max(0, PlannedMinutes - ScheduledMinutes);
    public long RemainingToDeliverMinutes => Math.Max(0, PlannedMinutes - DeliveredMinutes);
    public long ScheduledExcessMinutes => Math.Max(0, ScheduledMinutes - PlannedMinutes);
    public long DeliveredExcessMinutes => Math.Max(0, DeliveredMinutes - PlannedMinutes);
    public IReadOnlyList<string> Warnings => new[]
    {
        ScheduledMinutes < PlannedMinutes ? "calendar.under-scheduled" : null,
        ScheduledMinutes > PlannedMinutes ? "calendar.over-scheduled" : null,
        DeliveredMinutes < PlannedMinutes ? "calendar.delivery-incomplete" : null,
        DeliveredMinutes > PlannedMinutes ? "calendar.over-delivered" : null
    }.OfType<string>().ToArray();
}

public sealed record CalendarApproval(DateOnly DeadlineLocalDate, bool IsPublished, bool IsLate, bool IsOverdue);

public static class InstitutionCalendarRules
{
    public static TimeZoneInfo TimeZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || id.Any(char.IsControl))
            throw new CalendarValidationException("calendar.invalid-time-zone");
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        { throw new CalendarValidationException("calendar.invalid-time-zone"); }
    }

    public static DateOnly ParseDate(string value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date : throw new CalendarValidationException("calendar.invalid-date");

    public static TimeOnly ParseTime(string value) =>
        TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            ? time : throw new CalendarValidationException("calendar.invalid-time");

    public static CalendarApproval Approval(int year, int month, int approvalLeadDays, string timeZoneId,
        DateTime nowUtc, DateTime? publishedAtUtc)
    {
        if (approvalLeadDays is < 0 or > 366) throw new CalendarValidationException("calendar.invalid-approval-lead-days");
        if (nowUtc.Kind != DateTimeKind.Utc || publishedAtUtc is { Kind: not DateTimeKind.Utc })
            throw new CalendarValidationException("calendar.utc-required");
        var zone = TimeZone(timeZoneId);
        DateOnly deadline;
        try { deadline = new DateOnly(year, month, 1).AddDays(-approvalLeadDays); }
        catch (ArgumentOutOfRangeException) { throw new CalendarValidationException("calendar.invalid-period"); }
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(nowUtc, zone));
        var late = publishedAtUtc is {} published && DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(published, zone)) > deadline;
        return new(deadline, publishedAtUtc.HasValue, late, !publishedAtUtc.HasValue && today > deadline);
    }

    public static void ValidateState(CalendarLessonState lesson)
    {
        if (new[] { lesson.Id, lesson.InstitutionId, lesson.CourseEditionId, lesson.ClassId, lesson.PlanVersionId,
            lesson.ModuleId, lesson.SubjectId, lesson.OriginalTeacherId, lesson.ScheduledTeacherId }.Any(id => id == Guid.Empty)
            || lesson.Revision < 0 || !Enum.IsDefined(lesson.Publication) || !Enum.IsDefined(lesson.Status)
            || lesson.Interval is null || lesson.RecoveryOfLessonId == Guid.Empty || lesson.RecoveryOfLessonId == lesson.Id)
            throw new CalendarValidationException("calendar.invalid-lesson-state");
        if (lesson.Status == CalendarLessonStatus.Completed)
        {
            if (lesson.Publication != CalendarPublicationStatus.Published || lesson.DeliveredMinutes is not (> 0 and <= 1440))
                throw new CalendarValidationException("calendar.invalid-delivered-minutes");
        }
        else if (lesson.DeliveredMinutes.HasValue)
            throw new CalendarValidationException("calendar.unconfirmed-delivered-minutes");
        if (lesson.ScheduledTeacherId != lesson.OriginalTeacherId) Reason(lesson.SubstitutionReason);
    }

    // Caller must provide a tenant-scoped set while holding persistent SQL locks.
    // This pure predicate cannot certify concurrent database writes.
    public static IReadOnlyList<CalendarConflict> Conflicts(Guid institutionId, IReadOnlyList<CalendarLessonState> lessons)
    {
        if (institutionId == Guid.Empty || lessons.Any(l => l.InstitutionId != institutionId))
            throw new CalendarValidationException("calendar.tenant-scope-required");
        foreach (var lesson in lessons) ValidateState(lesson);
        if (lessons.Select(l => l.Id).Distinct().Count() != lessons.Count)
            throw new CalendarValidationException("calendar.duplicate-identity");
        var active = lessons.Where(l => l.Status != CalendarLessonStatus.Cancelled).OrderBy(l => l.Interval.StartUtc).ThenBy(l => l.Id).ToArray();
        var result = new List<CalendarConflict>();
        for (var i = 0; i < active.Length; i++)
        for (var j = i + 1; j < active.Length && active[j].Interval.StartUtc < active[i].Interval.EndUtc; j++)
        {
            var a = active[i]; var b = active[j];
            if (a.ClassId == b.ClassId && a.ModuleId == b.ModuleId && a.ScheduledTeacherId == b.ScheduledTeacherId &&
                a.Interval.StartUtc == b.Interval.StartUtc && a.Interval.EndUtc == b.Interval.EndUtc)
                result.Add(new(a.Id, b.Id, CalendarConflictKind.Duplicate));
            else
            {
                if (a.ClassId == b.ClassId) result.Add(new(a.Id, b.Id, CalendarConflictKind.ClassOverlap));
                if (a.ScheduledTeacherId == b.ScheduledTeacherId) result.Add(new(a.Id, b.Id, CalendarConflictKind.TeacherOverlap));
            }
        }
        return result;
    }

    public static IReadOnlyList<CalendarModuleHours> Hours(Guid institutionId, Guid classId,
        FormativePlanSnapshot plan, IEnumerable<CalendarLessonState> source)
    {
        if (institutionId == Guid.Empty || classId == Guid.Empty || plan.InstitutionId != institutionId || plan.Status != FormativeVersionStatus.Published)
            throw new CalendarValidationException("calendar.published-plan-required");
        var modules = plan.Units.SelectMany(u => u.Modules).ToArray();
        if (modules.Any(m => m.PlannedMinutes <= 0) || modules.Select(m => m.Id).Distinct().Count() != modules.Length)
            throw new CalendarValidationException("calendar.invalid-plan-modules");
        var moduleIds = modules.Select(m => m.Id).ToHashSet();
        var moduleSubjects = modules.ToDictionary(m => m.Id, m => m.SubjectId);
        var lessons = source.ToArray();
        foreach (var lesson in lessons)
        {
            ValidateState(lesson);
            if (lesson.InstitutionId != institutionId || lesson.ClassId != classId || lesson.PlanVersionId != plan.Id || !moduleIds.Contains(lesson.ModuleId))
                throw new CalendarValidationException("calendar.inconsistent-hours-scope");
            if (moduleSubjects[lesson.ModuleId] != lesson.SubjectId)
                throw new CalendarValidationException("calendar.inconsistent-module-subject");
        }
        // Duplicate projections must never silently inflate a report.
        if (lessons.Select(l => l.Id).Distinct().Count() != lessons.Length)
            throw new CalendarValidationException("calendar.duplicate-identity");
        return modules.Select(m => new CalendarModuleHours(classId, m.Id, m.PlannedMinutes,
            lessons.Where(l => l.ModuleId == m.Id && l.Publication == CalendarPublicationStatus.Published && l.Status != CalendarLessonStatus.Cancelled)
                .Sum(l => (long)l.Interval.DurationMinutes),
            lessons.Where(l => l.ModuleId == m.Id && l.Status == CalendarLessonStatus.Completed)
                .Sum(l => (long)l.DeliveredMinutes!.Value))).ToArray();
    }

    public static string Reason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500 || reason.Any(char.IsControl))
            throw new CalendarValidationException("calendar.reason-required");
        return reason.Trim();
    }

    private static void Revision(CalendarLessonState lesson, long expected)
    {
        ValidateState(lesson);
        if (expected < 0 || lesson.Revision != expected || lesson.Revision == long.MaxValue)
            throw new CalendarValidationException("calendar.revision-conflict");
    }

    public static CalendarLessonState Publish(CalendarLessonState lesson, long expectedRevision)
    {
        Revision(lesson, expectedRevision);
        if (lesson.Publication != CalendarPublicationStatus.Draft || lesson.Status != CalendarLessonStatus.Scheduled)
            throw new CalendarValidationException("calendar.draft-required");
        return lesson with { Publication = CalendarPublicationStatus.Published, Revision = lesson.Revision + 1 };
    }

    public static CalendarLessonState Cancel(CalendarLessonState lesson, long expectedRevision, string reason)
    {
        Revision(lesson, expectedRevision); Reason(reason);
        if (lesson.Status != CalendarLessonStatus.Scheduled) throw new CalendarValidationException("calendar.scheduled-required");
        return lesson with { Status = CalendarLessonStatus.Cancelled, Revision = lesson.Revision + 1 };
    }

    public static CalendarLessonState Reschedule(CalendarLessonState lesson, long expectedRevision, CalendarInterval interval, string reason)
    {
        Revision(lesson, expectedRevision); Reason(reason);
        if (lesson.Status != CalendarLessonStatus.Scheduled) throw new CalendarValidationException("calendar.scheduled-required");
        ArgumentNullException.ThrowIfNull(interval);
        return lesson with { Interval = interval, Revision = lesson.Revision + 1 };
    }

    public static CalendarLessonState Substitute(CalendarLessonState lesson, long expectedRevision, Guid teacherId, string reason)
    {
        Revision(lesson, expectedRevision); var normalizedReason = Reason(reason);
        if (lesson.Status != CalendarLessonStatus.Scheduled || teacherId == Guid.Empty)
            throw new CalendarValidationException("calendar.invalid-substitution");
        return lesson with { ScheduledTeacherId = teacherId, SubstitutionReason = normalizedReason, Revision = lesson.Revision + 1 };
    }

    public static CalendarLessonState Complete(CalendarLessonState lesson, long expectedRevision, int deliveredMinutes)
    {
        Revision(lesson, expectedRevision);
        if (lesson.Publication != CalendarPublicationStatus.Published || lesson.Status != CalendarLessonStatus.Scheduled)
            throw new CalendarValidationException("calendar.published-scheduled-required");
        if (deliveredMinutes is < 1 or > 1440) throw new CalendarValidationException("calendar.invalid-delivered-minutes");
        return lesson with { Status = CalendarLessonStatus.Completed, DeliveredMinutes = deliveredMinutes, Revision = lesson.Revision + 1 };
    }

    public static CalendarLessonState CorrectDelivered(CalendarLessonState lesson, long expectedRevision, int deliveredMinutes, string reason)
    {
        Revision(lesson, expectedRevision); Reason(reason);
        if (lesson.Status != CalendarLessonStatus.Completed) throw new CalendarValidationException("calendar.completed-required");
        if (deliveredMinutes is < 1 or > 1440) throw new CalendarValidationException("calendar.invalid-delivered-minutes");
        return lesson with { DeliveredMinutes = deliveredMinutes, Revision = lesson.Revision + 1 };
    }
}
