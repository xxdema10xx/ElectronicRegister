using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.DTOs.Calendar;
using ElectronicRegisterAPI.Domain.DTOs.Import;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Exceptions;
using System.Text.Json;
using ElectronicRegisterAPI.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal sealed partial class InstitutionCalendarRepository
{
    public async Task<CalendarHoursReport> HoursAsync(TenantContext t, Guid classId, CancellationToken ct)
    {
        await Access(t, ct);
        RequireOperator(t);
        await using var snapshot = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        var result = await Hours(t, classId, ct);
        await snapshot.CommitAsync(ct);
        return result;
    }

    private async Task<CalendarHoursReport> Hours(TenantContext t, Guid classId, CancellationToken ct)
    {
        if ((await Rows("SELECT id FROM class WHERE institution_id=@t AND id=@c", ct, ("@t", t.InstitutionId), ("@c", classId))).Count != 1)
            throw new KeyNotFoundException();
        var binding = (await Rows("SELECT c.course_edition_id,b.version_id FROM class c JOIN course_edition_formative_plan b ON b.institution_id=c.institution_id AND b.course_edition_id=c.course_edition_id JOIN formative_plan_version v ON v.institution_id=b.institution_id AND v.id=b.version_id AND v.status='Published' WHERE c.institution_id=@t AND c.id=@c", ct, ("@t", t.InstitutionId), ("@c", classId))).SingleOrDefault() ?? throw new CalendarConflictException("calendar.teaching-configuration-required");
        var modules = await Rows("SELECT m.id,m.unit_id,m.planned_minutes,COALESCE(SUM(IF(l.publication='Published' AND l.status<>'Cancelled',TIMESTAMPDIFF(MINUTE,l.start_utc,l.end_utc),0)),0) AS scheduled,COALESCE(SUM(IF(l.publication='Published' AND l.status='Completed',l.delivered_minutes,0)),0) AS delivered FROM formative_module m LEFT JOIN institution_lesson l ON l.institution_id=m.institution_id AND l.module_id=m.id AND l.class_id=@c WHERE m.institution_id=@t AND m.version_id=@v GROUP BY m.id,m.unit_id,m.planned_minutes ORDER BY m.unit_id,m.id", ct, ("@t", t.InstitutionId), ("@c", classId), ("@v", G(binding, "version_id")));
        var months = await Rows("SELECT YEAR(local_date) AS year,MONTH(local_date) AS month,SUM(IF(status<>'Cancelled',TIMESTAMPDIFF(MINUTE,start_utc,end_utc),0)) AS scheduled,COALESCE(SUM(IF(status='Completed',delivered_minutes,0)),0) AS delivered FROM institution_lesson WHERE institution_id=@t AND class_id=@c AND publication='Published' GROUP BY YEAR(local_date),MONTH(local_date) ORDER BY year,month", ct, ("@t", t.InstitutionId), ("@c", classId));
        return new(classId, G(binding, "course_edition_id"), G(binding, "version_id"), modules.Select(m => new CalendarModuleHours(classId, G(m, "id"), L(m, "planned_minutes"), L(m, "scheduled"), L(m, "delivered"))).ToArray(), modules.GroupBy(m => G(m, "unit_id")).Select(g => new CalendarUnitHours(g.Key, g.Sum(x => L(x, "planned_minutes")), g.Sum(x => L(x, "scheduled")), g.Sum(x => L(x, "delivered")))).ToArray(), months.Select(m => new CalendarMonthlyHours((int)L(m, "year"), (int)L(m, "month"), L(m, "scheduled"), L(m, "delivered"))).ToArray());
    }

    private static void ImportScope(CalendarImportSettings settings)
    {
        _ = new DateOnly(settings.Year, settings.Month, 1);
        if (!Enum.IsDefined(settings.Mode) || settings.ClassIds is null || settings.ClassIds.Count is < 1 or > 100 || settings.ClassIds.Any(x => x == Guid.Empty) || settings.ClassIds.Distinct().Count() != settings.ClassIds.Count)
            throw new ArgumentException("calendar.exact-scope-required");
    }

    private async Task<(CalendarImportPreview Preview, HashSet<Guid> Replaced)> BuildPreview(
        TenantContext t,
        Guid id,
        ImportTable table,
        CalendarImportSettings settings,
        DateTime expires,
        CancellationToken ct
    )
    {
        ImportScope(settings);
        var zone = await LoadSettings(t, ct);
        var month = new DateOnly(settings.Year, settings.Month, 1);
        var existing = new List<Dictionary<string, object>>();
        var periods = new List<object>();
        foreach (var classId in settings.ClassIds.Order())
        {
            if ((await Rows("SELECT id FROM class WHERE institution_id=@t AND id=@c FOR UPDATE", ct, ("@t", t.InstitutionId), ("@c", classId))).Count != 1)
                throw new KeyNotFoundException();
            existing.AddRange(await LessonRows(t, "AND l.class_id=@c AND l.local_date>=@month AND l.local_date<@next ORDER BY l.id FOR UPDATE", ct, ("@c", classId), ("@month", month), ("@next", month.AddMonths(1))));
            var p = (await Rows("SELECT id,revision FROM institution_calendar_period WHERE institution_id=@t AND class_id=@c AND month_date=@month", ct, ("@t", t.InstitutionId), ("@c", classId), ("@month", month))).SingleOrDefault();
            periods.Add(new { ClassId = classId, Id = p is null ? (Guid? )null : G(p, "id"), Revision = p is null ? -1 : L(p, "revision") });
        }

        var replaced = settings.Mode == CalendarImportMode.ReplaceDraftScope ? existing.Where(r => S(r, "publication") == "Draft" && S(r, "status") == "Scheduled").Select(r => G(r, "id")).ToHashSet() : new HashSet<Guid>();
        var mapping = importRules.Map(table.Headers, settings.Mapping);
        var mapped = importRules.Validate(table, mapping);
        var rows = new List<CalendarImportRow>();
        var resolvedState = new List<object>();
        var candidates = new List<CalendarLesson>();
        foreach (var row in mapped)
        {
            var issues = row.Issues.ToList();
            CalendarLessonInput? input = null;
            Dictionary<string, object>? resolved = null;
            CalendarInterval? interval = null;
            if (!issues.Any(i => !i.Warning))
            {
                try
                {
                    var classMatches = Guid.TryParse(row.ClassReference, out var classGuid) ? await Rows("SELECT id FROM class WHERE institution_id=@t AND id=@id", ct, ("@t", t.InstitutionId), ("@id", classGuid)) : await Rows("SELECT id FROM class WHERE institution_id=@t AND name=@name ORDER BY id LIMIT 2", ct, ("@t", t.InstitutionId), ("@name", row.ClassReference));
                    if (classMatches.Count != 1)
                        throw new CalendarValidationException(classMatches.Count == 0 ? "calendar.class-unavailable" : "calendar.ambiguous-class");
                    var c = G(classMatches[0], "id");
                    if (!settings.ClassIds.Contains(c))
                        throw new CalendarValidationException("calendar.row-outside-scope");
                    var moduleMatches = Guid.TryParse(row.ModuleReference, out var moduleGuid) ? await Rows("SELECT m.id FROM formative_module m JOIN course_edition_formative_plan b ON b.institution_id=m.institution_id AND b.version_id=m.version_id JOIN class c ON c.institution_id=b.institution_id AND c.course_edition_id=b.course_edition_id WHERE c.institution_id=@t AND c.id=@c AND m.id=@id", ct, ("@t", t.InstitutionId), ("@c", c), ("@id", moduleGuid)) : await Rows("SELECT m.id FROM formative_module m JOIN course_edition_formative_plan b ON b.institution_id=m.institution_id AND b.version_id=m.version_id JOIN class c ON c.institution_id=b.institution_id AND c.course_edition_id=b.course_edition_id WHERE c.institution_id=@t AND c.id=@c AND (m.code=@name OR m.name=@name) ORDER BY m.id LIMIT 2", ct, ("@t", t.InstitutionId), ("@c", c), ("@name", row.ModuleReference));
                    if (moduleMatches.Count != 1)
                        throw new CalendarValidationException(moduleMatches.Count == 0 ? "calendar.module-unavailable" : "calendar.ambiguous-module");
                    var teacherMatches = Guid.TryParse(row.TeacherReference, out var teacherGuid) ? await Rows("SELECT id FROM teacher WHERE institution_id=@t AND id=@id", ct, ("@t", t.InstitutionId), ("@id", teacherGuid)) : await Rows("SELECT teacher_id AS id FROM institution_import_external_reference WHERE institution_id=@t AND category='Teacher' AND reference=@ref", ct, ("@t", t.InstitutionId), ("@ref", row.TeacherReference));
                    if (teacherMatches.Count != 1)
                        throw new CalendarValidationException("calendar.teacher-unavailable");
                    if (row.Date is null || Month(row.Date.Value) != month)
                        throw new CalendarValidationException("calendar.row-outside-month");
                    input = new(c, G(moduleMatches[0], "id"), G(teacherMatches[0], "id"), row.Date.Value, row.StartsAt!.Value, row.EndsAt!.Value, null, row.Room, row.Description, row.ExternalReference);
                    resolved = await Resolve(t, input, ct);
                    interval = CalendarInterval.Create(input.Date, input.StartsAt, input.EndsAt, zone.TimeZoneId);
                    resolvedState.Add(new { Row = row.RowNumber, Reference = resolved });
                }
                catch (CalendarValidationException e)
                {
                    issues.Add(new("Row", e.Code, "Riferimenti o intervallo non validi."));
                    input = null;
                }
                catch (CalendarConflictException e)
                {
                    issues.Add(new("Row", e.Code, "Configurazione didattica non disponibile."));
                    input = null;
                }
            }

            var conflicts = new List<CalendarConflict>();
            var previewId = new Guid(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(id.ToString("D") + ":" + row.RowNumber))[..16]);
            if (input is not null && resolved is not null && interval is not null)
            {
                var candidate = new CalendarLesson(previewId, Guid.Empty, input.ClassId, G(resolved, "course_edition_id"), G(resolved, "version_id"), input.ModuleId, G(resolved, "subject_id"), G(resolved, "class_subject_id"), G(resolved, "assigned_teacher_id"), input.TeacherId, input.Date, input.StartsAt, input.EndsAt, zone.TimeZoneId, interval.StartUtc, interval.EndUtc, "Draft", "Scheduled", 0, null, null, input.Room, input.Description, input.ExternalReference);
                try
                {
                    await CheckConflicts(t, candidate, ct, replaced);
                }
                catch (CalendarConflictException e)when (e.Details is CalendarConflict[] found)
                {
                    conflicts.AddRange(found);
                }

                foreach (var prior in candidates.Where(p => (p.ClassId == candidate.ClassId || p.TeacherId == candidate.TeacherId) && p.StartUtc < candidate.EndUtc && candidate.StartUtc < p.EndUtc))
                {
                    var kind = prior.ClassId == candidate.ClassId && prior.ModuleId == candidate.ModuleId && prior.TeacherId == candidate.TeacherId && prior.StartUtc == candidate.StartUtc && prior.EndUtc == candidate.EndUtc ? CalendarConflictKind.Duplicate : prior.ClassId == candidate.ClassId ? CalendarConflictKind.ClassOverlap : CalendarConflictKind.TeacherOverlap;
                    conflicts.Add(new(candidate.Id, prior.Id, kind));
                    var index = rows.FindIndex(r => r.PreviewLessonId == prior.Id);
                    if (index >= 0)
                        rows[index] = rows[index] with
                        {
                            Conflicts = rows[index].Conflicts.Append(new CalendarConflict(prior.Id, candidate.Id, kind)).ToArray()
                        };
                }

                candidates.Add(candidate);
            }

            rows.Add(new(row.RowNumber, input, resolved is null ? null : S(resolved, "class_name"), resolved is null ? null : S(resolved, "name"), resolved is null ? null : S(resolved, "teacher_name"), interval?.DurationMinutes, issues, conflicts, previewId, row.ClassReference, row.ModuleReference, row.TeacherReference));
        }

        var hours = new List<CalendarImportImpact>();
        foreach (var c in settings.ClassIds.Order())
        {
            CalendarHoursReport summary;
            try
            {
                summary = await Hours(t, c, ct);
            }
            catch (CalendarConflictException)
            {
                continue;
            }

            var draft = await Rows("SELECT module_id,SUM(TIMESTAMPDIFF(MINUTE,start_utc,end_utc)) AS minutes FROM institution_lesson WHERE institution_id=@t AND class_id=@c AND publication='Draft' AND status='Scheduled' GROUP BY module_id", ct, ("@t", t.InstitutionId), ("@c", c));
            foreach (var module in summary.Modules)
            {
                var incoming = candidates.Where(l => l.ClassId == c && l.ModuleId == module.ModuleId).Sum(l => (long)l.DurationMinutes);
                var removed = existing.Where(r => replaced.Contains(G(r, "id")) && G(r, "class_id") == c && G(r, "module_id") == module.ModuleId).Sum(r => (long)Lesson(r).DurationMinutes);
                var draftMinutes = draft.Where(r => G(r, "module_id") == module.ModuleId).Sum(r => L(r, "minutes")) - removed + incoming;
                hours.Add(new(c, module.ModuleId, module.PlannedMinutes, module.ScheduledMinutes, draftMinutes, incoming));
            }
        }

        var preview = new CalendarImportPreview(id, 0, "", table.Sha256, expires, "Preview", settings, rows.Count, replaced.Count, existing.Count - replaced.Count, rows, hours, Digest(new { Settings = zone, Periods = periods, References = resolvedState }), table.Headers.ToArray(), mapping.Columns);
        return (preview with { Digest = Digest(preview) }, replaced);
    }

    public Task<CalendarImportPreview> PreviewAsync(
        TenantContext t,
        ImportTable table,
        CalendarImportSettings settings,
        CancellationToken ct
    ) => Write(t, settings.EventId, settings.Reason, new { Op = "preview", Table = table, Settings = settings }, async hash =>
    {
        if (table.Rows.Count is < 1 or > 2000 || Json(table).Length > 2 * 1024 * 1024)
            throw new ArgumentException("calendar.staging-limit");
        var pending = (await Rows("SELECT COUNT(*) AS n FROM institution_calendar_import WHERE institution_id=@t AND actor_membership_id=@m AND status='Preview' AND expires_at_utc>UTC_TIMESTAMP(6)", ct, ("@t", t.InstitutionId), ("@m", t.MembershipId))).Single();
        if (L(pending, "n") >= 20)
            throw new CalendarConflictException("calendar.preview-limit");
        var id = Guid.NewGuid();
        var expires = clock.GetUtcNow().UtcDateTime.AddMinutes(30);
        var result = (await BuildPreview(t, id, table, settings, expires, ct)).Preview;
        await Event(t, settings.EventId, "Import", id, "ImportRequested", null, 0, null, result, result, hash, settings.Reason, ct);
        await Execute("INSERT INTO institution_calendar_import(id,institution_id,actor_user_id,actor_membership_id,revision,status,file_sha256,preview_digest,raw_json,settings_json,preview_json,created_at_utc,expires_at_utc,event_id) VALUES(@id,@t,@u,@m,0,'Preview',@file,@digest,@raw,@settings,@preview,UTC_TIMESTAMP(6),@expires,@ev)", ct, ("@id", id), ("@t", t.InstitutionId), ("@u", t.UserId), ("@m", t.MembershipId), ("@file", table.Sha256), ("@digest", result.Digest), ("@raw", Json(table)), ("@settings", Json(settings)), ("@preview", Json(result)), ("@expires", expires), ("@ev", settings.EventId));
        return result;
    }, ct);
    private async Task<Dictionary<string, object>> ImportRow(TenantContext t, Guid id, CancellationToken ct) => (await Rows("SELECT * FROM institution_calendar_import WHERE institution_id=@t AND id=@id AND actor_user_id=@u AND actor_membership_id=@m", ct, ("@t", t.InstitutionId), ("@id", id), ("@u", t.UserId), ("@m", t.MembershipId))).SingleOrDefault() ?? throw new KeyNotFoundException();
    public async Task<CalendarImportPreview> ImportAsync(TenantContext t, Guid id, CancellationToken ct)
    {
        await Access(t, ct);
        RequireOperator(t);
        var row = await ImportRow(t, id, ct);
        var result = JsonSerializer.Deserialize<CalendarImportPreview>(S(row, "preview_json"))!;
        return result with
        {
            Revision = L(row, "revision"),
            Status = S(row, "status")
        };
    }

    public Task<CalendarReceipt> ConfirmAsync(
        TenantContext t,
        Guid id,
        CalendarConfirmImport r,
        CancellationToken ct
    ) => Write(t, r.EventId, r.Reason, new { Op = "confirm", Id = id, Request = r }, async hash =>
    {
        var row = await ImportRow(t, id, ct);
        Revision(L(row, "revision"), r.ExpectedRevision);
        if (S(row, "status") != "Preview" || U(row, "expires_at_utc") <= clock.GetUtcNow().UtcDateTime)
            throw new CalendarConflictException("calendar.preview-expired-or-consumed");
        if (S(row, "preview_digest") != r.Digest)
            throw new CalendarConflictException("calendar.preview-digest-conflict");
        var stored = JsonSerializer.Deserialize<CalendarImportPreview>(S(row, "preview_json"))!;
        var table = JsonSerializer.Deserialize<ImportTable>(S(row, "raw_json"))!;
        var settings = JsonSerializer.Deserialize<CalendarImportSettings>(S(row, "settings_json"))!;
        var built = await BuildPreview(t, id, table, settings, stored.ExpiresAtUtc, ct);
        var preview = built.Preview;
        if (!preview.CanConfirm || preview.Digest != r.Digest)
            throw new CalendarConflictException("calendar.preview-stale-or-invalid", preview);
        if (!r.AcknowledgeWarnings && (preview.Rows.Any(x => x.Issues.Any(i => i.Warning)) || preview.Hours.Any(h => h.PublishedMinutes + h.DraftMinutesAfterImport != h.PlannedMinutes)))
            throw new CalendarConflictException("calendar.warnings-not-acknowledged");
        var ids = preview.Rows.Select(_ => Guid.NewGuid()).ToArray();
        var receipt = new CalendarReceipt(r.EventId, id, L(row, "revision") + 1, ids);
        await Event(t, r.EventId, "Import", id, "ImportConfirmed", L(row, "revision"), receipt.Revision, stored, new { Revision = receipt.Revision, Status = "Confirmed", LessonIds = ids }, receipt, hash, r.Reason, ct);
        foreach (var replaced in built.Replaced.Order())
        {
            var old = await LoadLesson(t, replaced, ct);
            var lesson = Lesson(old);
            var cancelled = lesson with
            {
                Status = "Cancelled",
                Revision = checked(lesson.Revision + 1)
            };
            var ev = Guid.NewGuid();
            await StoreLesson(t, cancelled, old, ev, "DraftReplaced", new CalendarReceipt(ev, replaced, cancelled.Revision), hash, r.Reason, NS(old, "substitution_reason"), NG(old, "import_id"), ct);
        }

        for (var index = 0; index < preview.Rows.Count; index++)
        {
            var input = preview.Rows[index].Lesson!;
            var lesson = await BuildLesson(t, ids[index], input, null, ct);
            await CheckConflicts(t, lesson, ct);
            var ev = Guid.NewGuid();
            await StoreLesson(t, lesson, null, ev, "LessonCreated", new CalendarReceipt(ev, lesson.Id, 0), hash, r.Reason, input.SubstitutionReason, id, ct);
        }

        foreach (var classId in settings.ClassIds.Order())
            await BumpPeriod(t, await PeriodRow(t, classId, new(settings.Year, settings.Month, 1), ct, true), false, r.Reason, ct);
        await Execute("UPDATE institution_calendar_import SET status='Confirmed',revision=@rev,event_id=@ev WHERE institution_id=@t AND id=@id", ct, ("@rev", receipt.Revision), ("@ev", r.EventId), ("@t", t.InstitutionId), ("@id", id));
        return receipt;
    }, ct);
}
