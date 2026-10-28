using ElectronicRegisterAPI.Domain.DTOs.Calendar;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Policies;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal sealed partial class InstitutionCalendarRepository
{
    private static CalendarLessonState State(TenantContext t, CalendarLesson l, string? reason = null) => new(l.Id, t.InstitutionId, l.CourseEditionId, l.ClassId, l.VersionId, l.ModuleId, l.SubjectId, l.OriginalTeacherId, l.TeacherId, CalendarInterval.Create(l.Date, l.StartsAt, l.EndsAt, l.TimeZoneId), Enum.Parse<CalendarPublicationStatus>(l.Publication), Enum.Parse<CalendarLessonStatus>(l.Status), l.Revision, l.DeliveredMinutes, l.RecoveryOfLessonId, reason);
    private async Task<Dictionary<string, object>> Resolve(TenantContext t, CalendarLessonInput input, CancellationToken ct)
    {
        writes.RequireLocked(t);
        var classroom = (await Rows("SELECT id,course_edition_id FROM class WHERE institution_id=@t AND id=@id FOR UPDATE", ct, ("@t", t.InstitutionId), ("@id", input.ClassId))).SingleOrDefault() ?? throw new KeyNotFoundException();
        if (classroom["course_edition_id"] is DBNull)
            throw new CalendarConflictException("calendar.teaching-configuration-required");
        var binding = (await Rows("SELECT b.version_id,v.status FROM course_edition_formative_plan b JOIN formative_plan_version v ON v.institution_id=b.institution_id AND v.id=b.version_id WHERE b.institution_id=@t AND b.course_edition_id=@edition FOR UPDATE", ct, ("@t", t.InstitutionId), ("@edition", G(classroom, "course_edition_id")))).SingleOrDefault();
        if (binding is null || S(binding, "status") != "Published")
            throw new CalendarConflictException("calendar.teaching-configuration-required");
        var module = (await Rows("SELECT m.*,u.name AS unit_name,cs.id AS class_subject_id,cs.teacher_id AS assigned_teacher_id,c.name AS class_name FROM formative_module m JOIN formative_unit u ON u.institution_id=m.institution_id AND u.id=m.unit_id JOIN class c ON c.institution_id=m.institution_id AND c.id=@class JOIN class_subject cs ON cs.institution_id=c.institution_id AND cs.class_id=c.id AND cs.subject_id=m.subject_id WHERE m.institution_id=@t AND m.version_id=@version AND m.id=@module FOR UPDATE", ct, ("@class", input.ClassId), ("@t", t.InstitutionId), ("@version", G(binding, "version_id")), ("@module", input.ModuleId))).SingleOrDefault() ?? throw new CalendarConflictException("calendar.module-or-class-subject-unavailable");
        var teacher = (await Rows("SELECT id,first_name,last_name FROM teacher WHERE institution_id=@t AND id=@id FOR UPDATE", ct, ("@t", t.InstitutionId), ("@id", input.TeacherId))).SingleOrDefault() ?? throw new CalendarConflictException("calendar.teacher-unavailable");
        if (G(module, "assigned_teacher_id") != input.TeacherId)
            InstitutionCalendarRules.Reason(input.SubstitutionReason);
        module["course_edition_id"] = classroom["course_edition_id"];
        module["teacher_name"] = S(teacher, "first_name") + " " + S(teacher, "last_name");
        return module;
    }

    private async Task<CalendarLesson> BuildLesson(
        TenantContext t,
        Guid id,
        CalendarLessonInput input,
        CalendarLesson? before,
        CancellationToken ct
    )
    {
        if (input.ClassId == Guid.Empty || input.ModuleId == Guid.Empty || input.TeacherId == Guid.Empty)
            throw new ArgumentException("calendar.reference-required");
        var settings = await LoadSettings(t, ct);
        var interval = CalendarInterval.Create(input.Date, input.StartsAt, input.EndsAt, settings.TimeZoneId);
        var resolved = await Resolve(t, input, ct);
        var p = await PeriodRow(t, input.ClassId, Month(input.Date), ct, true);
        if (before is not null && (before.ClassId != input.ClassId || before.CourseEditionId != G(resolved, "course_edition_id") || before.VersionId != G(resolved, "version_id")))
            throw new CalendarConflictException("calendar.ownership-immutable");
        var original = before?.OriginalTeacherId ?? G(resolved, "assigned_teacher_id");
        if (original != input.TeacherId)
            InstitutionCalendarRules.Reason(input.SubstitutionReason);
        var result = new CalendarLesson(id, G(p, "id"), input.ClassId, G(resolved, "course_edition_id"), G(resolved, "version_id"), input.ModuleId, G(resolved, "subject_id"), G(resolved, "class_subject_id"), original, input.TeacherId, input.Date, input.StartsAt, input.EndsAt, settings.TimeZoneId, interval.StartUtc, interval.EndUtc, before?.Publication ?? "Draft", "Scheduled", before is null ? 0 : checked(before.Revision + 1), null, input.RecoveryOfLessonId, Text(input.Room, 200), Text(input.Description, 2000), Text(input.ExternalReference, 100));
        InstitutionCalendarRules.ValidateState(State(t, result, input.SubstitutionReason));
        if (result.RecoveryOfLessonId is { } recovery)
        {
            var cancelledRow = await LoadLesson(t, recovery, ct);
            var cancelled = Lesson(cancelledRow);
            _ = CalendarTeachingRules.Recovery(State(t, cancelled, NS(cancelledRow, "substitution_reason")), State(t, result with { RecoveryOfLessonId = null }, input.SubstitutionReason));
        }

        return result;
    }

    private async Task CheckConflicts(
        TenantContext t,
        CalendarLesson incoming,
        CancellationToken ct,
        IReadOnlySet<Guid>? ignored = null
    )
    {
        if (incoming.Status == "Cancelled")
            return;
        // The Institution exclusive lock serializes all calendar writers. Current locking read
        // additionally protects against SQL writes to existing bookings under other isolation levels.
        var rows = await LessonRows(t, "AND l.id<>@id AND l.status<>'Cancelled' AND (l.class_id=@class OR l.teacher_id=@teacher) AND l.start_utc<@end AND @start<l.end_utc FOR UPDATE", ct, ("@id", incoming.Id), ("@class", incoming.ClassId), ("@teacher", incoming.TeacherId), ("@end", incoming.EndUtc), ("@start", incoming.StartUtc));
        var conflicts = rows.Where(r => ignored is null || !ignored.Contains(G(r, "id"))).Select(r => new CalendarConflict(incoming.Id, G(r, "id"), G(r, "class_id") == incoming.ClassId && G(r, "module_id") == incoming.ModuleId && G(r, "teacher_id") == incoming.TeacherId && U(r, "start_utc") == incoming.StartUtc && U(r, "end_utc") == incoming.EndUtc ? CalendarConflictKind.Duplicate : G(r, "class_id") == incoming.ClassId ? CalendarConflictKind.ClassOverlap : CalendarConflictKind.TeacherOverlap)).ToArray();
        if (conflicts.Length > 0)
            throw new CalendarConflictException("calendar.overlap", conflicts);
    }

    private async Task StoreLesson(
        TenantContext t,
        CalendarLesson after,
        Dictionary<string, object>? before,
        Guid eventId,
        string op,
        object receipt,
        string hash,
        string reason,
        string? substitutionReason,
        Guid? importId,
        CancellationToken ct
    )
    {
        var confirming = op is "LessonCompleted" or "DeliveredMinutesCorrected";
        var confirmedUser = confirming ? t.UserId : before is null ? null : NG(before, "confirmed_by_user_id");
        var confirmedMember = confirming ? t.MembershipId : before is null ? null : NG(before, "confirmed_by_membership_id");
        var confirmedAt = confirming ? clock.GetUtcNow().UtcDateTime : before is not null && before["confirmed_at_utc"] is not DBNull ? U(before, "confirmed_at_utc") : (DateTime? )null;
        if (confirmedAt is { } instant)
            confirmedAt = new DateTime(instant.Ticks / 10 * 10, DateTimeKind.Utc);
        var audit = System.Text.Json.JsonSerializer.SerializeToNode(after)!.AsObject();
        audit["SubstitutionReason"] = substitutionReason;
        audit["ImportId"] = importId?.ToString("D");
        audit["ConfirmedByUserId"] = confirmedUser?.ToString("D");
        audit["ConfirmedByMembershipId"] = confirmedMember?.ToString("D");
        audit["ConfirmedAtUtc"] = confirmedAt.HasValue ? System.Text.Json.JsonSerializer.SerializeToNode(confirmedAt.Value) : null;
        await Event(t, eventId, "Lesson", after.Id, op, before is null ? null : L(before, "revision"), after.Revision, before, audit, receipt, hash, reason, ct);
        var values = new (string, object? )[]
        {
            ("@id", after.Id),
            ("@t", t.InstitutionId),
            ("@period", after.PeriodId),
            ("@class", after.ClassId),
            ("@edition", after.CourseEditionId),
            ("@version", after.VersionId),
            ("@module", after.ModuleId),
            ("@subject", after.SubjectId),
            ("@cs", after.ClassSubjectId),
            ("@original", after.OriginalTeacherId),
            ("@teacher", after.TeacherId),
            ("@date", after.Date),
            ("@start", after.StartsAt),
            ("@end", after.EndsAt),
            ("@zone", after.TimeZoneId),
            ("@utcstart", after.StartUtc),
            ("@utcend", after.EndUtc),
            ("@pub", after.Publication),
            ("@status", after.Status),
            ("@revision", after.Revision),
            ("@delivered", after.DeliveredMinutes),
            ("@cu", confirmedUser),
            ("@cm", confirmedMember),
            ("@ca", confirmedAt),
            ("@recovery", after.RecoveryOfLessonId),
            ("@import", importId),
            ("@sub", substitutionReason),
            ("@room", after.Room),
            ("@description", after.Description),
            ("@external", after.ExternalReference),
            ("@event", eventId)
        };
        if (before is null)
            await Execute("INSERT INTO institution_lesson(id,institution_id,period_id,class_id,course_edition_id,version_id,module_id,subject_id,class_subject_id,original_teacher_id,teacher_id,local_date,starts_at,ends_at,time_zone_id,start_utc,end_utc,publication,status,revision,delivered_minutes,confirmed_by_user_id,confirmed_by_membership_id,confirmed_at_utc,recovery_of_id,import_id,substitution_reason,room,description,external_reference,event_id) VALUES(@id,@t,@period,@class,@edition,@version,@module,@subject,@cs,@original,@teacher,@date,@start,@end,@zone,@utcstart,@utcend,@pub,@status,@revision,@delivered,@cu,@cm,@ca,@recovery,@import,@sub,@room,@description,@external,@event)", ct, values);
        else
            await Execute("UPDATE institution_lesson SET period_id=@period,module_id=@module,subject_id=@subject,class_subject_id=@cs,teacher_id=@teacher,local_date=@date,starts_at=@start,ends_at=@end,time_zone_id=@zone,start_utc=@utcstart,end_utc=@utcend,publication=@pub,status=@status,revision=@revision,delivered_minutes=@delivered,confirmed_by_user_id=@cu,confirmed_by_membership_id=@cm,confirmed_at_utc=@ca,recovery_of_id=@recovery,substitution_reason=@sub,room=@room,description=@description,external_reference=@external,event_id=@event WHERE institution_id=@t AND id=@id", ct, values);
        await faults.CheckpointAsync("after-lesson", ct);
    }

    public Task<CalendarReceipt> SaveAsync(
        TenantContext t,
        Guid? id,
        CalendarSaveLesson r,
        CancellationToken ct
    ) => Write(t, r.EventId, r.Reason, new { Op = "save", Id = id, Request = r }, async hash =>
    {
        var before = id.HasValue ? await LoadLesson(t, id.Value, ct) : null;
        if (before is null)
        {
            if (r.ExpectedRevision != -1)
                throw new CalendarConflictException("calendar.revision-conflict");
        }
        else
        {
            Revision(L(before, "revision"), r.ExpectedRevision);
            if (S(before, "status") != "Scheduled")
                throw new CalendarConflictException("calendar.scheduled-required");
        }

        var after = await BuildLesson(t, id ?? Guid.NewGuid(), r.Lesson, before is null ? null : Lesson(before), ct);
        await CheckConflicts(t, after, ct);
        var receipt = new CalendarReceipt(r.EventId, after.Id, after.Revision);
        await StoreLesson(t, after, before, r.EventId, before is null ? "LessonCreated" : "LessonUpdated", receipt, hash, r.Reason, r.Lesson.SubstitutionReason, before is null ? null : NG(before, "import_id"), ct);
        await TouchPeriods(t, before, after, r.Reason, ct);
        return receipt;
    }, ct);
    private async Task TouchPeriods(
        TenantContext t,
        Dictionary<string, object>? before,
        CalendarLesson after,
        string reason,
        CancellationToken ct
    )
    {
        if (before is not null && G(before, "period_id") != after.PeriodId)
            await BumpPeriod(t, await PeriodRow(t, after.ClassId, Month(Lesson(before).Date), ct), after.Publication == "Published", reason, ct);
        var period = await PeriodRow(t, after.ClassId, Month(after.Date), ct);
        await BumpPeriod(t, period, after.Publication == "Published", reason, ct);
    }

    public Task<CalendarReceipt> PublishAsync(
        TenantContext t,
        Guid classId,
        int year,
        int month,
        CalendarCommand r,
        CancellationToken ct
    ) => Write(t, r.EventId, r.Reason, new { Op = "publish", ClassId = classId, Year = year, Month = month, Request = r }, async hash =>
    {
        var p = await PeriodRow(t, classId, new(year, month, 1), ct);
        Revision(L(p, "revision"), r.ExpectedRevision);
        var rows = await LessonRows(t, "AND l.period_id=@p AND l.publication='Draft' AND l.status='Scheduled' ORDER BY l.id FOR UPDATE", ct, ("@p", G(p, "id")));
        if (rows.Count == 0)
            throw new CalendarConflictException("calendar.no-draft-lessons");
        var receipt = new CalendarReceipt(r.EventId, G(p, "id"), L(p, "revision") + 1, rows.Select(x => G(x, "id")).ToArray());
        await Event(t, r.EventId, "Period", G(p, "id"), "CalendarPublished", L(p, "revision"), receipt.Revision, p, new { Revision = receipt.Revision, Status = "Published" }, receipt, hash, r.Reason, ct);
        foreach (var row in rows)
        {
            var before = Lesson(row);
            var input = new CalendarLessonInput(before.ClassId, before.ModuleId, before.TeacherId, before.Date, before.StartsAt, before.EndsAt, NS(row, "substitution_reason"), before.Room, before.Description, before.ExternalReference, before.RecoveryOfLessonId);
            await Resolve(t, input, ct);
            await CheckConflicts(t, before, ct);
            var after = before with
            {
                Publication = "Published",
                Revision = checked(before.Revision + 1)
            };
            var ev = Guid.NewGuid();
            await StoreLesson(t, after, row, ev, "LessonPublished", new CalendarReceipt(ev, after.Id, after.Revision), hash, r.Reason, NS(row, "substitution_reason"), NG(row, "import_id"), ct);
        }

        await Execute("UPDATE institution_calendar_period SET status='Published',revision=@rev,event_id=@ev,published_by_user_id=@u,published_by_membership_id=@m,published_at_utc=UTC_TIMESTAMP(6) WHERE institution_id=@t AND id=@id", ct, ("@rev", receipt.Revision), ("@ev", r.EventId), ("@u", t.UserId), ("@m", t.MembershipId), ("@t", t.InstitutionId), ("@id", receipt.ResourceId));
        await faults.CheckpointAsync("after-publication", ct);
        return receipt;
    }, ct);
    public Task<CalendarReceipt> TransitionAsync(
        TenantContext t,
        Guid id,
        string operation,
        CalendarCommand r,
        CalendarDeliveredCommand? delivered,
        CalendarRescheduleCommand? reschedule,
        CalendarSubstituteCommand? substitute,
        CancellationToken ct
    ) => Write(t, r.EventId, r.Reason, new { Op = operation, Id = id, Request = r, Delivered = delivered, Reschedule = reschedule, Substitute = substitute }, async hash =>
    {
        var row = await LoadLesson(t, id, ct);
        var before = Lesson(row);
        Revision(before.Revision, r.ExpectedRevision);
        var subReason = NS(row, "substitution_reason");
        var state = State(t, before, subReason);
        CalendarLessonState next;
        if (operation == "LessonCompleted")
        {
            if (!Operator(t) && (await Rows("SELECT teacher_id FROM institution_teacher_profile WHERE institution_id=@t AND membership_id=@m AND teacher_id=@teacher FOR UPDATE", ct, ("@t", t.InstitutionId), ("@m", t.MembershipId), ("@teacher", before.TeacherId))).Count != 1)
                throw new UnauthorizedAccessException();
            next = InstitutionCalendarRules.Complete(state, r.ExpectedRevision, delivered?.DeliveredMinutes ?? throw new ArgumentException());
        }
        else
        {
            RequireOperator(t);
            next = operation switch
            {
                "LessonCancelled" => InstitutionCalendarRules.Cancel(state, r.ExpectedRevision, r.Reason),
                "DeliveredMinutesCorrected" => InstitutionCalendarRules.CorrectDelivered(state, r.ExpectedRevision, delivered?.DeliveredMinutes ?? throw new ArgumentException(), r.Reason),
                "LessonRescheduled" when reschedule is not null => InstitutionCalendarRules.Reschedule(state, r.ExpectedRevision, CalendarInterval.Create(reschedule.Date, reschedule.StartsAt, reschedule.EndsAt, before.TimeZoneId), r.Reason),
                "TeacherSubstituted" when substitute is not null => InstitutionCalendarRules.Substitute(state, r.ExpectedRevision, substitute.TeacherId, r.Reason),
                _ => throw new ArgumentException("calendar.unknown-operation")};
        }

        subReason = next.SubstitutionReason;
        var after = before with
        {
            Date = next.Interval.LocalDate,
            StartsAt = next.Interval.StartsAt,
            EndsAt = next.Interval.EndsAt,
            StartUtc = next.Interval.StartUtc,
            EndUtc = next.Interval.EndUtc,
            TeacherId = next.ScheduledTeacherId,
            Status = next.Status.ToString(),
            Revision = next.Revision,
            DeliveredMinutes = next.DeliveredMinutes
        };
        if (operation is "LessonRescheduled" or "TeacherSubstituted")
        {
            await Resolve(t, new(after.ClassId, after.ModuleId, after.TeacherId, after.Date, after.StartsAt, after.EndsAt, subReason), ct);
            var p = await PeriodRow(t, after.ClassId, Month(after.Date), ct, true);
            after = after with
            {
                PeriodId = G(p, "id")
            };
            await CheckConflicts(t, after, ct);
        }

        var receipt = new CalendarReceipt(r.EventId, id, after.Revision);
        await StoreLesson(t, after, row, r.EventId, operation, receipt, hash, r.Reason, subReason, NG(row, "import_id"), ct);
        if (operation is not ("LessonCompleted" or "DeliveredMinutesCorrected"))
            await TouchPeriods(t, row, after, r.Reason, ct);
        return receipt;
    }, ct, allowTeacher: operation == "LessonCompleted");
}
