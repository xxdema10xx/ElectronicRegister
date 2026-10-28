#nullable enable
using System;
using System.Collections.Generic;
using ElectronicRegisterAPI.Domain.Models;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Unit;

public sealed class CalendarTeachingRulesTests
{
    private readonly Guid tenant = Guid.NewGuid(), classroom = Guid.NewGuid(), edition = Guid.NewGuid(), version = Guid.NewGuid(),
        module = Guid.NewGuid(), subject = Guid.NewGuid(), teacher = Guid.NewGuid(), substitute = Guid.NewGuid();
    private CalendarClassBinding Class => new(tenant, classroom, edition);
    private CalendarEditionBinding Binding => new(tenant, edition, version);
    private CalendarSubjectAssignment Assignment => new(tenant, classroom, subject, teacher);
    private FormativePlanSnapshot Plan => new(version, tenant, Guid.NewGuid(), Guid.NewGuid(), 1, "Plan", FormativeVersionStatus.Published, 1, 4800, 0,
        new[] { new FormativeUnitSnapshot(Guid.NewGuid(), null, "UF", null, 0, new[] {
            new FormativeModuleSnapshot(module, null, "DB", null, 4800, 0, subject, Array.Empty<FormativeTopic>(), Array.Empty<FormativeTopic>()) }) });
    private CalendarTeachingBinding Resolve(CalendarClassBinding? c = null, CalendarEditionBinding? b = null, FormativePlanSnapshot? p = null,
        Guid? m = null, Guid? t = null, CalendarSubjectAssignment[]? assignments = null, string? reason = null) =>
        CalendarTeachingRules.Resolve(tenant, c ?? Class, b ?? Binding, p ?? Plan, m ?? module, t ?? teacher,
            assignments ?? new[] { Assignment }, new HashSet<Guid> { teacher, substitute }, reason);
    [Fact] public void ResolvesRealModuleSubjectAndAssignment()
    {
        var result = Resolve(); Assert.Equal(module, result.ModuleId); Assert.Equal(subject, result.SubjectId);
        Assert.Equal(teacher, result.OriginalTeacherId); Assert.Equal(teacher, result.ScheduledTeacherId);
    }
    [Fact] public void LegacyClassNeedsExplicitConfiguration() => Assert.Equal("calendar.teaching-configuration-required",
        Assert.Throws<CalendarValidationException>(() => Resolve(c: Class with { CourseEditionId = null })).Code);
    [Fact] public void MissingBindingIsNotInvented() => Assert.Throws<CalendarValidationException>(() => CalendarTeachingRules.Resolve(
        tenant, Class, null, Plan, module, teacher, new[] { Assignment }, new HashSet<Guid> { teacher }));
    [Fact] public void ForeignClassRejected() => Assert.Throws<CalendarValidationException>(() => Resolve(c: Class with { InstitutionId = Guid.NewGuid() }));
    [Fact] public void ForeignPlanRejected() => Assert.Throws<CalendarValidationException>(() => Resolve(p: Plan with { InstitutionId = Guid.NewGuid() }));
    [Fact] public void ForeignBindingRejected() => Assert.Throws<CalendarValidationException>(() => Resolve(b: Binding with { InstitutionId = Guid.NewGuid() }));
    [Fact] public void WrongEditionRejected() => Assert.Throws<CalendarValidationException>(() => Resolve(b: Binding with { CourseEditionId = Guid.NewGuid() }));
    [Fact] public void WrongVersionRejected() => Assert.Throws<CalendarValidationException>(() => Resolve(b: Binding with { VersionId = Guid.NewGuid() }));
    [Fact] public void DraftPlanRejected() => Assert.Throws<CalendarValidationException>(() => Resolve(p: Plan with { Status = FormativeVersionStatus.Draft }));
    [Fact] public void InventedModuleRejected() => Assert.Throws<CalendarValidationException>(() => Resolve(m: Guid.NewGuid()));
    [Fact] public void ExternalTeacherRejected() => Assert.Throws<CalendarValidationException>(() => Resolve(t: Guid.NewGuid()));
    [Fact] public void NoClassSubjectCreated() => Assert.Equal("calendar.class-subject-required",
        Assert.Throws<CalendarValidationException>(() => Resolve(assignments: Array.Empty<CalendarSubjectAssignment>())).Code);
    [Fact] public void IncompatibleSubjectRejected() => Assert.Throws<CalendarValidationException>(() => Resolve(assignments: new[] { Assignment with { SubjectId = Guid.NewGuid() } }));
    [Fact] public void ForeignAssignmentRejected() => Assert.Throws<CalendarValidationException>(() => Resolve(assignments: new[] { Assignment with { InstitutionId = Guid.NewGuid() } }));
    [Fact] public void DifferentTeacherRequiresExplicitSubstitution() => Assert.Throws<CalendarValidationException>(() => Resolve(t: substitute));
    [Fact] public void SubstitutionPreservesAssignment()
    {
        var assignment = Assignment; var result = Resolve(t: substitute, assignments: new[] { assignment }, reason: "Cover");
        Assert.Equal(teacher, assignment.TeacherId); Assert.Equal(teacher, result.OriginalTeacherId); Assert.Equal(substitute, result.ScheduledTeacherId);
    }
    private CalendarLessonState Lesson() => new(Guid.NewGuid(), tenant, edition, classroom, version, module, subject, teacher, teacher,
        CalendarInterval.Create(new(2026, 11, 5), new(9, 0), new(11, 0), "Europe/Rome"), CalendarPublicationStatus.Published, CalendarLessonStatus.Scheduled, 0);
    [Fact] public void RecoveryLinksNewIdentity()
    {
        var cancelled = InstitutionCalendarRules.Cancel(Lesson(), 0, "Absent"); var replacement = Lesson();
        var result = CalendarTeachingRules.Recovery(cancelled, replacement);
        Assert.Equal(cancelled.Id, result.RecoveryOfLessonId); Assert.NotEqual(cancelled.Id, result.Id); Assert.Null(replacement.RecoveryOfLessonId);
    }
    [Fact] public void RecoveryRequiresCancelledSource() => Assert.Throws<CalendarValidationException>(() => CalendarTeachingRules.Recovery(Lesson(), Lesson()));
    [Fact] public void RecoveryCannotCrossClasses() => Assert.Throws<CalendarValidationException>(() => CalendarTeachingRules.Recovery(
        InstitutionCalendarRules.Cancel(Lesson(), 0, "Absent"), Lesson() with { ClassId = Guid.NewGuid() }));
    [Fact] public void RecoveryCannotCrossModules() => Assert.Throws<CalendarValidationException>(() => CalendarTeachingRules.Recovery(
        InstitutionCalendarRules.Cancel(Lesson(), 0, "Absent"), Lesson() with { ModuleId = Guid.NewGuid() }));
}
