using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Policies;

public static class CalendarTeachingRules
{
    public static CalendarTeachingBinding Resolve(
        Guid institutionId,
        CalendarClassBinding classroom,
        CalendarEditionBinding? binding,
        FormativePlanSnapshot? plan,
        Guid moduleId,
        Guid teacherId,
        IReadOnlyList<CalendarSubjectAssignment> assignments,
        IReadOnlySet<Guid> institutionTeachers,
        string? substitutionReason = null
    )
    {
        if (institutionId == Guid.Empty || classroom.InstitutionId != institutionId || classroom.ClassId == Guid.Empty)
            throw new CalendarValidationException("calendar.class-unavailable");
        if (classroom.CourseEditionId is null || binding is null || plan is null)
            throw new CalendarValidationException("calendar.teaching-configuration-required");
        if (binding.InstitutionId != institutionId || binding.CourseEditionId != classroom.CourseEditionId || plan.InstitutionId != institutionId || binding.VersionId != plan.Id || plan.Status != FormativeVersionStatus.Published)
            throw new CalendarValidationException("calendar.inconsistent-plan-binding");
        var modules = plan.Units.SelectMany(u => u.Modules).Where(m => m.Id == moduleId).ToArray();
        if (moduleId == Guid.Empty || modules.Length != 1 || modules[0].SubjectId is not { } subjectId || subjectId == Guid.Empty)
            throw new CalendarValidationException("calendar.module-unavailable");
        if (assignments.Any(a => a.InstitutionId != institutionId || a.ClassId != classroom.ClassId))
            throw new CalendarValidationException("calendar.inconsistent-assignment-scope");
        var matches = assignments.Where(a => a.SubjectId == subjectId).ToArray();
        if (matches.Length != 1 || !institutionTeachers.Contains(matches[0].TeacherId) || matches[0].TeacherId == Guid.Empty)
            throw new CalendarValidationException("calendar.class-subject-required");
        if (teacherId == Guid.Empty || !institutionTeachers.Contains(teacherId))
            throw new CalendarValidationException("calendar.teacher-unavailable");
        var original = matches[0].TeacherId;
        var reason = teacherId == original ? null : InstitutionCalendarRules.Reason(substitutionReason);
        return new(institutionId, binding.CourseEditionId, classroom.ClassId, plan.Id, moduleId, subjectId, original, teacherId, reason);
    }

    public static CalendarLessonState Recovery(CalendarLessonState cancelled, CalendarLessonState replacement)
    {
        InstitutionCalendarRules.ValidateState(cancelled);
        InstitutionCalendarRules.ValidateState(replacement);
        if (cancelled.Status != CalendarLessonStatus.Cancelled || replacement.Status != CalendarLessonStatus.Scheduled || cancelled.Id == replacement.Id || cancelled.InstitutionId != replacement.InstitutionId || cancelled.ClassId != replacement.ClassId || cancelled.CourseEditionId != replacement.CourseEditionId || cancelled.PlanVersionId != replacement.PlanVersionId || cancelled.ModuleId != replacement.ModuleId || cancelled.SubjectId != replacement.SubjectId || replacement.RecoveryOfLessonId.HasValue)
            throw new CalendarValidationException("calendar.invalid-recovery");
        return replacement with
        {
            RecoveryOfLessonId = cancelled.Id
        };
    }
}
