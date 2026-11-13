using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Policies;
#nullable enable
using System;
using System.Linq;
using ElectronicRegisterAPI.Domain.Models;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Unit;

public sealed class InstitutionCalendarRulesTests
{
    private static readonly Guid Tenant = Guid.NewGuid(), Class = Guid.NewGuid(), Edition = Guid.NewGuid(),
        Version = Guid.NewGuid(), Module = Guid.NewGuid(), Subject = Guid.NewGuid(), Teacher = Guid.NewGuid();
    private static CalendarInterval Interval(string start = "09:15", string end = "11:45", string date = "2026-11-05", string zone = "Europe/Rome") =>
        CalendarInterval.Create(InstitutionCalendarRules.ParseDate(date), InstitutionCalendarRules.ParseTime(start), InstitutionCalendarRules.ParseTime(end), zone);
    private static CalendarLessonState Lesson(CalendarPublicationStatus publication = CalendarPublicationStatus.Published) => new(
        Guid.NewGuid(), Tenant, Edition, Class, Version, Module, Subject, Teacher, Teacher, Interval(), publication, CalendarLessonStatus.Scheduled, 0);
    private static FormativePlanSnapshot Plan(int minutes = 4800) => new(Version, Tenant, Guid.NewGuid(), Guid.NewGuid(), 1, "Plan",
        FormativeVersionStatus.Published, 1, minutes, 0,
        new[] { new FormativeUnitSnapshot(Guid.NewGuid(), "UF", "Unit", null, 0,
            new[] { new FormativeModuleSnapshot(Module, "DB", "Database", null, minutes, 0, Subject, Array.Empty<FormativeTopic>(), Array.Empty<FormativeTopic>()) }) });
    private static void Error(string code, Action action) => Assert.Equal(code, Assert.Throws<CalendarValidationException>(action).Code);

    [Theory]
    [InlineData("09:00", "13:00", 240)]
    [InlineData("08:00", "14:00", 360)]
    [InlineData("09:15", "11:45", 150)]
    [InlineData("10:20", "12:05", 105)]
    [InlineData("13:30", "17:10", 220)]
    [InlineData("15:45", "18:00", 135)]
    [InlineData("00:00", "23:59", 1439)]
    [InlineData("09:01", "09:02", 1)]
    public void FlexibleDurations(string start, string end, int minutes) => Assert.Equal(minutes, Interval(start, end).DurationMinutes);

    [Theory]
    [InlineData("09:00", "09:00")]
    [InlineData("10:00", "09:00")]
    [InlineData("23:00", "01:00")]
    public void PositiveSameDayRequired(string start, string end) => Error("calendar.same-day-positive-interval-required", () => Interval(start, end));
    [Fact] public void SecondsAreNotRounded() => Error("calendar.minute-precision-required", () =>
        CalendarInterval.Create(new(2026, 11, 5), new(9, 0, 1), new(11, 0), "Europe/Rome"));
    [Fact] public void FractionalSecondsAreNotRounded() => Error("calendar.minute-precision-required", () =>
        CalendarInterval.Create(new(2026, 11, 5), new TimeOnly(9, 0).Add(TimeSpan.FromTicks(1)), new(11, 0), "Europe/Rome"));
    [Fact] public void AdjacentIntervalsAreCompatible() => Assert.False(Interval("09:00", "11:00").Overlaps(Interval("11:00", "13:00")));
    [Fact] public void OverlappingIntervalsConflict() => Assert.True(Interval("09:00", "11:30").Overlaps(Interval("11:00", "13:00")));
    [Theory]
    [InlineData("2026-03-29", "02:15", "03:15", "calendar.nonexistent-local-time")]
    [InlineData("2026-10-25", "02:15", "03:15", "calendar.ambiguous-local-time")]
    [InlineData("2026-03-29", "01:00", "04:00", "calendar.nonexistent-local-time")]
    [InlineData("2026-10-25", "01:00", "04:00", "calendar.ambiguous-local-time")]
    public void DstIsNeverResolvedSilently(string date, string start, string end, string code) => Error(code, () => Interval(start, end, date));
    [Theory]
    [InlineData("")]
    [InlineData("Invalid/TimeZone")]
    [InlineData("Europe/Rome\0")]
    public void InvalidZone(string zone) => Error("calendar.invalid-time-zone", () => Interval(zone: zone));
    [Theory]
    [InlineData("2026-11-05", 8)]
    [InlineData("2026-07-05", 7)]
    public void RomeConversionUsesSeasonalOffset(string date, int utcHour) => Assert.Equal(utcHour, Interval("09:00", "10:00", date).StartUtc.Hour);
    [Fact] public void TenantZoneIsNotHardcoded() => Assert.Equal(9, Interval("09:00", "10:00", zone: "UTC").StartUtc.Hour);
    [Fact] public void UtcKindIsExplicit() => Assert.Equal(DateTimeKind.Utc, Interval().StartUtc.Kind);
    [Theory]
    [InlineData("05/11/2026")]
    [InlineData("2026-02-29")]
    [InlineData("45900")]
    public void TextDatesAreStrict(string date) => Error("calendar.invalid-date", () => InstitutionCalendarRules.ParseDate(date));
    [Theory]
    [InlineData("9:15")]
    [InlineData("09:15:00")]
    [InlineData("24:00")]
    [InlineData("0.5")]
    public void TextTimesAreStrict(string time) => Error("calendar.invalid-time", () => InstitutionCalendarRules.ParseTime(time));
    [Fact] public void TeacherConflictsAcrossClasses()
    {
        var a = Lesson(); var b = Lesson() with { ClassId = Guid.NewGuid() };
        Assert.Equal(CalendarConflictKind.TeacherOverlap, Assert.Single(InstitutionCalendarRules.Conflicts(Tenant, new[] { a, b })).Kind);
    }
    [Fact] public void ClassConflictsAcrossTeachers()
    {
        var a = Lesson(); var b = Lesson() with { OriginalTeacherId = Guid.NewGuid(), ScheduledTeacherId = Guid.NewGuid(), SubstitutionReason = "Cover" };
        Assert.Equal(CalendarConflictKind.ClassOverlap, Assert.Single(InstitutionCalendarRules.Conflicts(Tenant, new[] { a, b })).Kind);
    }
    [Fact] public void DuplicateIsSpecific() => Assert.Equal(CalendarConflictKind.Duplicate,
        Assert.Single(InstitutionCalendarRules.Conflicts(Tenant, new[] { Lesson(), Lesson() })).Kind);
    [Fact] public void CancelledDoesNotOccupyTime() => Assert.Empty(InstitutionCalendarRules.Conflicts(Tenant,
        new[] { Lesson(), InstitutionCalendarRules.Cancel(Lesson(), 0, "Cancelled") }));
    [Fact] public void CompletedStillOccupiesItsHistoricalInterval() => Assert.NotEmpty(InstitutionCalendarRules.Conflicts(Tenant,
        new[] { Lesson(), InstitutionCalendarRules.Complete(Lesson(), 0, 140) }));
    [Fact] public void DraftConflictIsReported() => Assert.NotEmpty(InstitutionCalendarRules.Conflicts(Tenant,
        new[] { Lesson(), Lesson(CalendarPublicationStatus.Draft) }));
    [Fact] public void TenantMixRejected() => Error("calendar.tenant-scope-required", () => InstitutionCalendarRules.Conflicts(Tenant,
        new[] { Lesson(), Lesson() with { InstitutionId = Guid.NewGuid() } }));
    [Fact] public void DuplicateProjectionRejected()
    {
        var lesson = Lesson();
        Error("calendar.duplicate-identity", () => InstitutionCalendarRules.Conflicts(Tenant, new[] { lesson, lesson }));
    }
    [Fact] public void PlannedScheduledDeliveredRemainDistinct()
    {
        var result = Assert.Single(InstitutionCalendarRules.Hours(Tenant, Class, Plan(), new[] { InstitutionCalendarRules.Complete(Lesson(), 0, 125), Lesson() with { Interval = Interval("13:00", "15:00") } }));
        Assert.Equal(4800, result.PlannedMinutes); Assert.Equal(270, result.ScheduledMinutes); Assert.Equal(125, result.DeliveredMinutes);
        Assert.Equal(4530, result.RemainingToScheduleMinutes); Assert.Equal(4675, result.RemainingToDeliverMinutes);
    }
    [Fact] public void DraftAndCancelledExcludedFromPublishedHours()
    {
        var result = Assert.Single(InstitutionCalendarRules.Hours(Tenant, Class, Plan(), new[] { Lesson(CalendarPublicationStatus.Draft), InstitutionCalendarRules.Cancel(Lesson(), 0, "Cancelled") }));
        Assert.Equal(0, result.ScheduledMinutes); Assert.Equal(0, result.DeliveredMinutes);
    }
    [Fact] public void RecoveryCountsOnlyActiveReplacement()
    {
        var cancelled = InstitutionCalendarRules.Cancel(Lesson(), 0, "Absent");
        var replacement = Lesson() with { RecoveryOfLessonId = cancelled.Id };
        Assert.Equal(150, Assert.Single(InstitutionCalendarRules.Hours(Tenant, Class, Plan(), new[] { cancelled, replacement })).ScheduledMinutes);
    }
    [Fact] public void ClassesDoNotShareBudget()
    {
        var otherClass = Guid.NewGuid(); var plan = Plan();
        Assert.Equal(4800, Assert.Single(InstitutionCalendarRules.Hours(Tenant, Class, plan, new[] { Lesson() })).PlannedMinutes);
        Assert.Equal(4800, Assert.Single(InstitutionCalendarRules.Hours(Tenant, otherClass, plan, new[] { Lesson() with { ClassId = otherClass } })).PlannedMinutes);
        Error("calendar.inconsistent-hours-scope", () => InstitutionCalendarRules.Hours(Tenant, Class, plan, new[] { Lesson() with { ClassId = otherClass } }));
    }
    [Fact] public void ExcessIsWarningAndNeverChangesPlan()
    {
        var plan = Plan(100); var before = plan.Units[0].Modules[0].PlannedMinutes;
        var hours = Assert.Single(InstitutionCalendarRules.Hours(Tenant, Class, plan, new[] { InstitutionCalendarRules.Complete(Lesson(), 0, 170) }));
        Assert.Equal(50, hours.ScheduledExcessMinutes); Assert.Equal(70, hours.DeliveredExcessMinutes);
        Assert.Contains("calendar.over-scheduled", hours.Warnings); Assert.Contains("calendar.over-delivered", hours.Warnings);
        Assert.Equal(before, plan.Units[0].Modules[0].PlannedMinutes);
    }
    [Fact] public void MissingHoursAreVisible()
    {
        var hours = Assert.Single(InstitutionCalendarRules.Hours(Tenant, Class, Plan(), Array.Empty<CalendarLessonState>()));
        Assert.Equal(4800, hours.RemainingToScheduleMinutes); Assert.Equal(4800, hours.RemainingToDeliverMinutes);
        Assert.Contains("calendar.under-scheduled", hours.Warnings); Assert.Contains("calendar.delivery-incomplete", hours.Warnings);
    }
    [Fact] public void HoursDoNotDeduplicateSilently()
    {
        var lesson = Lesson(); Error("calendar.duplicate-identity", () => InstitutionCalendarRules.Hours(Tenant, Class, Plan(), new[] { lesson, lesson }));
    }
    [Fact] public void ForeignPlanRejected() => Error("calendar.published-plan-required", () => InstitutionCalendarRules.Hours(Tenant, Class, Plan() with { InstitutionId = Guid.NewGuid() }, new[] { Lesson() }));
    [Fact] public void DraftPlanRejected() => Error("calendar.published-plan-required", () => InstitutionCalendarRules.Hours(Tenant, Class, Plan() with { Status = FormativeVersionStatus.Draft }, new[] { Lesson() }));
    [Fact] public void ForeignModuleRejected() => Error("calendar.inconsistent-hours-scope", () => InstitutionCalendarRules.Hours(Tenant, Class, Plan(), new[] { Lesson() with { ModuleId = Guid.NewGuid() } }));
    [Fact] public void ForeignVersionRejected() => Error("calendar.inconsistent-hours-scope", () => InstitutionCalendarRules.Hours(Tenant, Class, Plan(), new[] { Lesson() with { PlanVersionId = Guid.NewGuid() } }));
    [Fact] public void PublicationIsExplicit()
    {
        var draft = Lesson(CalendarPublicationStatus.Draft); var published = InstitutionCalendarRules.Publish(draft, 0);
        Assert.Equal(CalendarPublicationStatus.Draft, draft.Publication); Assert.Equal(CalendarPublicationStatus.Published, published.Publication); Assert.Equal(1, published.Revision);
    }
    [Fact] public void PublishedCannotBeRepublishedByPureTransition() => Error("calendar.draft-required", () => InstitutionCalendarRules.Publish(Lesson(), 0));
    [Fact] public void CancelPreservesOriginalState()
    {
        var before = Lesson(); var after = InstitutionCalendarRules.Cancel(before, 0, "Absent");
        Assert.Equal(before.Id, after.Id); Assert.Equal(CalendarLessonStatus.Scheduled, before.Status);
        Assert.Equal(CalendarLessonStatus.Cancelled, after.Status); Assert.Equal(1, after.Revision);
    }
    [Fact] public void ReschedulePreservesIdentityAndPreviousInterval()
    {
        var before = Lesson(); var after = InstitutionCalendarRules.Reschedule(before, 0, Interval("13:00", "15:00"), "Moved");
        Assert.Equal(before.Id, after.Id); Assert.Equal(new TimeOnly(9, 15), before.Interval.StartsAt); Assert.Equal(new TimeOnly(13, 0), after.Interval.StartsAt);
    }
    [Fact] public void SubstitutePreservesOriginalTeacher()
    {
        var before = Lesson(); var substitute = Guid.NewGuid(); var after = InstitutionCalendarRules.Substitute(before, 0, substitute, "Cover");
        Assert.Equal(Teacher, after.OriginalTeacherId); Assert.Equal(substitute, after.ScheduledTeacherId); Assert.Equal(Teacher, before.ScheduledTeacherId);
    }
    [Fact] public void CompletionRequiresAnExplicitAction()
    {
        var before = Lesson() with { Interval = Interval(date: "2020-01-01") };
        Assert.Null(before.DeliveredMinutes); Assert.Equal(CalendarLessonStatus.Scheduled, before.Status);
        var after = InstitutionCalendarRules.Complete(before, 0, 135); Assert.Equal(135, after.DeliveredMinutes); Assert.Equal(1, after.Revision);
    }
    [Fact] public void DraftCannotBeCompleted() => Error("calendar.published-scheduled-required", () => InstitutionCalendarRules.Complete(Lesson(CalendarPublicationStatus.Draft), 0, 150));
    [Fact] public void CancelledCannotBeCompleted() => Error("calendar.published-scheduled-required", () => InstitutionCalendarRules.Complete(InstitutionCalendarRules.Cancel(Lesson(), 0, "Cancelled"), 1, 150));
    [Fact] public void CompletedCannotBeRescheduled() => Error("calendar.scheduled-required", () => InstitutionCalendarRules.Reschedule(InstitutionCalendarRules.Complete(Lesson(), 0, 150), 1, Interval("13:00", "15:00"), "Moved"));
    [Fact] public void CompletedCannotBeCancelled() => Error("calendar.scheduled-required", () => InstitutionCalendarRules.Cancel(InstitutionCalendarRules.Complete(Lesson(), 0, 150), 1, "Cancelled"));
    [Fact] public void CompletedCannotBeSubstituted() => Error("calendar.invalid-substitution", () => InstitutionCalendarRules.Substitute(InstitutionCalendarRules.Complete(Lesson(), 0, 150), 1, Guid.NewGuid(), "Cover"));
    [Fact] public void CorrectionPreservesPreviousMinutes()
    {
        var before = InstitutionCalendarRules.Complete(Lesson(), 0, 150); var after = InstitutionCalendarRules.CorrectDelivered(before, 1, 125, "Correction");
        Assert.Equal(150, before.DeliveredMinutes); Assert.Equal(125, after.DeliveredMinutes); Assert.Equal(2, after.Revision);
    }
    [Theory] [InlineData(0)] [InlineData(-1)] [InlineData(1441)]
    public void DeliveredBounds(int minutes) => Error("calendar.invalid-delivered-minutes", () => InstitutionCalendarRules.Complete(Lesson(), 0, minutes));
    [Theory] [InlineData(-1)] [InlineData(1)] [InlineData(long.MaxValue)]
    public void StaleOrInvalidRevision(long revision) => Error("calendar.revision-conflict", () => InstitutionCalendarRules.Cancel(Lesson(), revision, "Cancelled"));
    [Fact] public void RevisionCannotOverflow() => Error("calendar.revision-conflict", () => InstitutionCalendarRules.Cancel(Lesson() with { Revision = long.MaxValue }, long.MaxValue, "Cancelled"));
    [Theory] [InlineData("")] [InlineData(" ")] [InlineData("Reason\0")]
    public void ReasonIsRequired(string reason) => Error("calendar.reason-required", () => InstitutionCalendarRules.Cancel(Lesson(), 0, reason));
    [Fact] public void ApprovalUsesConfiguredLeadAndTenantDate()
    {
        var approval = InstitutionCalendarRules.Approval(2026, 11, 5, "Europe/Rome", new DateTime(2026, 10, 27, 23, 30, 0, DateTimeKind.Utc), null);
        Assert.Equal(new DateOnly(2026, 10, 27), approval.DeadlineLocalDate); Assert.True(approval.IsOverdue); Assert.False(approval.IsPublished);
    }
    [Fact] public void DeadlineDayIsInclusive() => Assert.False(InstitutionCalendarRules.Approval(2026, 11, 3, "UTC", new DateTime(2026, 10, 29, 23, 59, 0, DateTimeKind.Utc), null).IsOverdue);
    [Fact] public void LatePublicationIsExplicit() => Assert.True(InstitutionCalendarRules.Approval(2026, 11, 3, "UTC", new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc)).IsLate);
    [Fact] public void ServerLocalClockIsRejected() => Error("calendar.utc-required", () => InstitutionCalendarRules.Approval(2026, 11, 3, "UTC", new DateTime(2026, 11, 1), null));
    [Theory] [InlineData(-1)] [InlineData(367)]
    public void LeadDaysValidated(int days) => Error("calendar.invalid-approval-lead-days", () => InstitutionCalendarRules.Approval(2026, 11, days, "UTC", DateTime.UtcNow, null));
}
