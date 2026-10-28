using ElectronicRegisterAPI.Domain.Authorization;
using ElectronicRegisterAPI.Domain.Configuration;
using ElectronicRegisterAPI.Domain.DTOs.Calendar;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Policies;
using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MySqlConnector;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal sealed partial class InstitutionCalendarRepository(
    ElectronicRegisterContext db,
    IMembershipAuthorizationRepository memberships,
    IInstitutionWriteAuthorization writes,
    CalendarFeature feature,
    ICalendarImportRules importRules,
    ICalendarFaultObserver faults,
    TimeProvider clock
) : IInstitutionCalendarRepository
{
    private static bool Operator(TenantContext t) => CalendarPermissions.Can(t.Roles, CalendarCapability.Manage);
    private static void RequireOperator(TenantContext t)
    {
        if (!Operator(t))
            throw new UnauthorizedAccessException();
    }

    private async Task Access(TenantContext t, CancellationToken ct)
    {
        if (!feature.Enabled)
            throw new UnauthorizedAccessException();
        var m = await memberships.GetAsync(t.UserId, t.InstitutionId, t.MembershipId, ct);
        if (m is null || m.Status != MembershipStatus.Active || m.InstitutionStatus != InstitutionStatus.Active || m.SecurityGeneration is null or < 0 || m.SecurityGeneration != t.SecurityGeneration || !t.Roles.SetEquals(m.Roles) || m.Roles.Any(r => !Enum.IsDefined(r)))
            throw new UnauthorizedAccessException();
    }

    private async Task<DbCommand> Command(string sql, CancellationToken ct, params (string, object? )[] args)
    {
        if (db.Database.GetDbConnection().State != ConnectionState.Open)
            await db.Database.OpenConnectionAsync(ct);
        var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = sql;
        cmd.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        foreach (var(k, v)in args)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = k;
            p.Value = v switch
            {
                Guid g => g.ToString("D"),
                DateOnly d => d.ToDateTime(TimeOnly.MinValue),
                TimeOnly time => time.ToTimeSpan(),
                null => DBNull.Value,
                _ => v
            };
            cmd.Parameters.Add(p);
        }

        return cmd;
    }

    private async Task<List<Dictionary<string, object>>> Rows(string sql, CancellationToken ct, params (string, object? )[] args)
    {
        await using var cmd = await Command(sql, ct, args);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var result = new List<Dictionary<string, object>>();
        while (await r.ReadAsync(ct))
        {
            var row = new Dictionary<string, object>();
            for (var i = 0; i < r.FieldCount; i++)
                row[r.GetName(i)] = r.GetValue(i);
            result.Add(row);
        }

        return result;
    }

    private async Task Execute(string sql, CancellationToken ct, params (string, object? )[] args)
    {
        await using var cmd = await Command(sql, ct, args);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static string S(Dictionary<string, object> r, string k) => Convert.ToString(r[k])!;
    private static string? NS(Dictionary<string, object> r, string k) => r[k] is DBNull ? null : S(r, k);
    private static Guid G(Dictionary<string, object> r, string k) => Guid.Parse(S(r, k));
    private static Guid? NG(Dictionary<string, object> r, string k) => r[k] is DBNull ? null : G(r, k);
    private static long L(Dictionary<string, object> r, string k) => Convert.ToInt64(r[k]);
    private static DateTime U(Dictionary<string, object> r, string k) => DateTime.SpecifyKind((DateTime)r[k], DateTimeKind.Utc);
    private static string Json(object value) => JsonSerializer.Serialize(value);
    private static string Digest(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Json(value))));
    private static DateOnly Month(DateOnly d) => new(d.Year, d.Month, 1);
    private static void Revision(long actual, long expected)
    {
        if (expected < 0 || actual != expected || actual == long.MaxValue)
            throw new CalendarConflictException("calendar.revision-conflict");
    }

    private static void Page(int page, int size)
    {
        if (page < 1 || size is < 1 or > 100 || (long)(page - 1) * size > int.MaxValue)
            throw new ArgumentException("calendar.invalid-page");
    }

    private static string? Text(string? value, int max)
    {
        if (value is null)
            return null;
        value = value.Trim();
        if (value.Length > max || value.Any(char.IsControl))
            throw new ArgumentException("calendar.invalid-text");
        return value.Length == 0 ? null : value;
    }

    private async Task<T> Write<T>(
        TenantContext t,
        Guid eventId,
        string reason,
        object intent,
        Func<string, Task<T>> action,
        CancellationToken ct,
        bool allowTeacher = false
    )
    {
        if (!Operator(t) && !(allowTeacher && CalendarPermissions.Can(t.Roles, CalendarCapability.ConfirmOwnDelivery)))
            throw new UnauthorizedAccessException();
        if (eventId == Guid.Empty)
            throw new ArgumentException("calendar.event-required");
        InstitutionCalendarRules.Reason(reason);
        await Access(t, ct);
        if (db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Standalone calendar transaction required.");
        var hash = Digest(new { t.InstitutionId, t.UserId, t.MembershipId, Intent = intent });
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            await Execute("SELECT id FROM institution WHERE id=@t FOR UPDATE", ct, ("@t", t.InstitutionId));
            await writes.LockAsync(t, ct);
            await Access(t, ct);
            await faults.CheckpointAsync("after-iam-lock", ct);
            var prior = (await Rows("SELECT request_hash,receipt_json FROM institution_calendar_event WHERE institution_id=@t AND id=@id", ct, ("@t", t.InstitutionId), ("@id", eventId))).SingleOrDefault();
            T result;
            if (prior is not null)
            {
                if (S(prior, "request_hash") != hash)
                    throw new CalendarConflictException("calendar.event-payload-conflict");
                result = JsonSerializer.Deserialize<T>(S(prior, "receipt_json"))!;
            }
            else
                result = await action(hash);
            await faults.CheckpointAsync("before-commit", ct);
            await Access(t, ct);
            await tx.CommitAsync(ct);
            await faults.CheckpointAsync("after-commit", ct);
            return result;
        }
        catch (MySqlException e)when (e.Number is 1062 or 1451 or 1452 or 1644 or 1213 or 1205)
        {
            throw new CalendarConflictException("calendar.database-conflict");
        }
    }

    private async Task Event(
        TenantContext t,
        Guid id,
        string kind,
        Guid resource,
        string operation,
        long? beforeRevision,
        long revision,
        object? before,
        object after,
        object receipt,
        string hash,
        string reason,
        CancellationToken ct
    )
    {
        await Execute("INSERT INTO institution_calendar_event(id,institution_id,actor_user_id,actor_membership_id,resource_kind,resource_id,operation,revision_before,revision_after,request_hash,before_json,state_json,receipt_json,reason,occurred_at_utc) VALUES(@id,@t,@u,@m,@kind,@r,@op,@rb,@ra,@hash,@before,@after,@receipt,@reason,UTC_TIMESTAMP(6))", ct, ("@id", id), ("@t", t.InstitutionId), ("@u", t.UserId), ("@m", t.MembershipId), ("@kind", kind), ("@r", resource), ("@op", operation), ("@rb", beforeRevision), ("@ra", revision), ("@hash", hash), ("@before", before is null ? null : Json(before)), ("@after", Json(after)), ("@receipt", Json(receipt)), ("@reason", reason));
        await faults.CheckpointAsync("after-audit", ct);
    }

    private async Task<CalendarSettings> LoadSettings(TenantContext t, CancellationToken ct)
    {
        var r = (await Rows("SELECT * FROM institution_calendar_settings WHERE institution_id=@t", ct, ("@t", t.InstitutionId))).SingleOrDefault() ?? throw new CalendarConflictException("calendar.settings-required");
        return new(S(r, "time_zone_id"), (int)L(r, "approval_lead_days"), L(r, "revision"));
    }

    public async Task<CalendarSettings> SettingsAsync(TenantContext t, CancellationToken ct)
    {
        await Access(t, ct);
        RequireOperator(t);
        return await LoadSettings(t, ct);
    }

    public Task<CalendarReceipt> ConfigureAsync(TenantContext t, CalendarSettingsCommand r, CancellationToken ct) => Write(t, r.EventId, r.Reason, new { Op = "configure", Request = r }, async hash =>
    {
        _ = InstitutionCalendarRules.TimeZone(r.TimeZoneId);
        if (r.ApprovalLeadDays is < 0 or > 366)
            throw new ArgumentException("calendar.invalid-lead-days");
        var row = (await Rows("SELECT * FROM institution_calendar_settings WHERE institution_id=@t", ct, ("@t", t.InstitutionId))).SingleOrDefault();
        CalendarSettings? before = row is null ? null : await LoadSettings(t, ct);
        if (before is null)
        {
            if (r.ExpectedRevision != -1)
                throw new CalendarConflictException("calendar.revision-conflict");
        }
        else
            Revision(before.Revision, r.ExpectedRevision);
        var revision = before is null ? 0 : before.Revision + 1;
        var after = new CalendarSettings(r.TimeZoneId, r.ApprovalLeadDays, revision);
        var receipt = new CalendarReceipt(r.EventId, t.InstitutionId, revision);
        await Event(t, r.EventId, "Settings", t.InstitutionId, "SettingsConfigured", before?.Revision, revision, before, after, receipt, hash, r.Reason, ct);
        await Execute(before is null ? "INSERT INTO institution_calendar_settings(institution_id,time_zone_id,approval_lead_days,revision,event_id) VALUES(@t,@zone,@lead,@rev,@ev)" : "UPDATE institution_calendar_settings SET time_zone_id=@zone,approval_lead_days=@lead,revision=@rev,event_id=@ev WHERE institution_id=@t", ct, ("@t", t.InstitutionId), ("@zone", r.TimeZoneId), ("@lead", r.ApprovalLeadDays), ("@rev", revision), ("@ev", r.EventId));
        return receipt;
    }, ct);
    private async Task<Dictionary<string, object>> PeriodRow(
        TenantContext t,
        Guid classId,
        DateOnly month,
        CancellationToken ct,
        bool create = false
    )
    {
        var r = (await Rows("SELECT * FROM institution_calendar_period WHERE institution_id=@t AND class_id=@c AND month_date=@month", ct, ("@t", t.InstitutionId), ("@c", classId), ("@month", month))).SingleOrDefault();
        if (r is not null)
            return r;
        if (!create)
            throw new KeyNotFoundException();
        var id = Guid.NewGuid();
        var ev = Guid.NewGuid();
        var receipt = new CalendarReceipt(ev, id, 0);
        await Event(t, ev, "Period", id, "DraftCreated", null, 0, null, new { Id = id, ClassId = classId, Month = month, Status = "Draft", Revision = 0 }, receipt, Digest(receipt), "Initial class/month draft", ct);
        await Execute("INSERT INTO institution_calendar_period(id,institution_id,class_id,month_date,status,revision,event_id) VALUES(@id,@t,@c,@month,'Draft',0,@ev)", ct, ("@id", id), ("@t", t.InstitutionId), ("@c", classId), ("@month", month), ("@ev", ev));
        return await PeriodRow(t, classId, month, ct);
    }

    private async Task BumpPeriod(
        TenantContext t,
        Dictionary<string, object> before,
        bool publish,
        string reason,
        CancellationToken ct
    )
    {
        var id = G(before, "id");
        var rev = checked(L(before, "revision") + 1);
        var ev = Guid.NewGuid();
        var status = publish ? "Published" : S(before, "status");
        await Event(t, ev, "Period", id, publish ? "CalendarPublished" : "DraftUpdated", rev - 1, rev, before, new { Id = id, Status = status, Revision = rev }, new CalendarReceipt(ev, id, rev), Digest(new { id, rev, publish }), reason, ct);
        await Execute("UPDATE institution_calendar_period SET revision=@rev,status=@status,event_id=@ev,published_by_user_id=IF(@pub,@u,published_by_user_id),published_by_membership_id=IF(@pub,@m,published_by_membership_id),published_at_utc=IF(@pub,UTC_TIMESTAMP(6),published_at_utc) WHERE institution_id=@t AND id=@id", ct, ("@rev", rev), ("@status", status), ("@ev", ev), ("@pub", publish), ("@u", t.UserId), ("@m", t.MembershipId), ("@t", t.InstitutionId), ("@id", id));
    }

    public async Task<CalendarPeriod> PeriodAsync(
        TenantContext t,
        Guid classId,
        int year,
        int month,
        CancellationToken ct
    )
    {
        await Access(t, ct);
        RequireOperator(t);
        var settings = await LoadSettings(t, ct);
        var r = await PeriodRow(t, classId, new(year, month, 1), ct);
        var published = r["published_at_utc"] is DBNull ? (DateTime? )null : U(r, "published_at_utc");
        return new(G(r, "id"), classId, new(year, month, 1), S(r, "status"), L(r, "revision"), NG(r, "published_by_user_id"), NG(r, "published_by_membership_id"), published, InstitutionCalendarRules.Approval(year, month, settings.ApprovalLeadDays, settings.TimeZoneId, clock.GetUtcNow().UtcDateTime, published));
    }

    private static CalendarLesson Lesson(Dictionary<string, object> r) => new(G(r, "id"), G(r, "period_id"), G(r, "class_id"), G(r, "course_edition_id"), G(r, "version_id"), G(r, "module_id"), G(r, "subject_id"), G(r, "class_subject_id"), G(r, "original_teacher_id"), G(r, "teacher_id"), DateOnly.FromDateTime((DateTime)r["local_date"]), TimeOnly.FromTimeSpan((TimeSpan)r["starts_at"]), TimeOnly.FromTimeSpan((TimeSpan)r["ends_at"]), S(r, "time_zone_id"), U(r, "start_utc"), U(r, "end_utc"), S(r, "publication"), S(r, "status"), L(r, "revision"), r["delivered_minutes"] is DBNull ? null : (int)L(r, "delivered_minutes"), NG(r, "recovery_of_id"), NS(r, "room"), NS(r, "description"), NS(r, "external_reference"), S(r, "class_name"), S(r, "module_name"), S(r, "teacher_name"));
    private Task<List<Dictionary<string, object>>> LessonRows(
        TenantContext t,
        string filter,
        CancellationToken ct,
        params (string, object? )[] args
    ) => Rows("SELECT l.*,c.name AS class_name,m.name AS module_name,CONCAT(teacher.first_name,' ',teacher.last_name) AS teacher_name FROM institution_lesson l JOIN class c ON c.institution_id=l.institution_id AND c.id=l.class_id JOIN formative_module m ON m.institution_id=l.institution_id AND m.version_id=l.version_id AND m.id=l.module_id JOIN teacher ON teacher.institution_id=l.institution_id AND teacher.id=l.teacher_id WHERE l.institution_id=@t " + filter, ct, new[] { ("@t", (object? )t.InstitutionId) }.Concat(args).ToArray());
    private async Task<Dictionary<string, object>> LoadLesson(TenantContext t, Guid id, CancellationToken ct) => (await LessonRows(t, "AND l.id=@id", ct, ("@id", id))).SingleOrDefault() ?? throw new KeyNotFoundException();
    private static string PersonalScope(TenantContext t, string view)
    {
        var teacher = t.Roles.Contains(InstitutionRole.Teacher) ? "EXISTS(SELECT 1 FROM institution_teacher_profile p WHERE p.institution_id=l.institution_id AND p.membership_id=@member AND p.teacher_id=l.teacher_id)" : "FALSE";
        var student = t.Roles.Contains(InstitutionRole.Student) ? "EXISTS(SELECT 1 FROM institution_student_profile p JOIN student s ON s.institution_id=p.institution_id AND s.id=p.student_id WHERE p.institution_id=l.institution_id AND p.membership_id=@member AND s.class_id=l.class_id)" : "FALSE";
        return " AND l.publication='Published' AND (" + (view == "teacher" ? teacher : view == "student" ? student : teacher + " OR " + student) + ")";
    }

    public async Task<IReadOnlyList<CalendarLesson>> ListAsync(
        TenantContext t,
        CalendarQuery q,
        string view,
        CancellationToken ct
    )
    {
        await Access(t, ct);
        Page(q.Page, q.Size);
        if (q.To < q.From || q.To.DayNumber - q.From.DayNumber > 366)
            throw new ArgumentException("calendar.range-limit");
        if (view == "admin")
            RequireOperator(t);
        else if (view is not ("teacher" or "student"))
            throw new ArgumentException();
        var filter = " AND l.local_date>=@from AND l.local_date<=@to" + (view == "admin" ? "" : PersonalScope(t, view));
        if (!q.IncludeCancelled)
            filter += " AND l.status<>'Cancelled'";
        foreach (var(field, value)in new[]
        {
            ("class_id", q.ClassId),
            ("teacher_id", q.TeacherId),
            ("module_id", q.ModuleId),
            ("course_edition_id", q.CourseEditionId)
        }

        )
            if (value.HasValue)
                filter += $" AND l.{field}=@{field}";
        if (q.Publication is not null)
        {
            if (q.Publication is not ("Draft" or "Published"))
                throw new ArgumentException();
            filter += " AND l.publication=@publication";
        }

        if (q.Status is not null)
        {
            if (q.Status is not ("Scheduled" or "Cancelled" or "Completed"))
                throw new ArgumentException();
            filter += " AND l.status=@status";
        }

        return (await LessonRows(t, filter + " ORDER BY l.start_utc,l.id LIMIT @size OFFSET @offset", ct, ("@member", t.MembershipId), ("@from", q.From), ("@to", q.To), ("@class_id", q.ClassId), ("@teacher_id", q.TeacherId), ("@module_id", q.ModuleId), ("@course_edition_id", q.CourseEditionId), ("@publication", q.Publication), ("@status", q.Status), ("@size", q.Size), ("@offset", (q.Page - 1) * q.Size))).Select(Lesson).ToArray();
    }

    public async Task<CalendarLesson> GetAsync(TenantContext t, Guid id, CancellationToken ct)
    {
        await Access(t, ct);
        var r = (await LessonRows(t, "AND l.id=@id" + (Operator(t) ? "" : PersonalScope(t, "both")), ct, ("@id", id), ("@member", t.MembershipId))).SingleOrDefault() ?? throw new KeyNotFoundException();
        return Lesson(r);
    }

    public async Task<FormativeModuleSnapshot> ModuleAsync(TenantContext t, Guid lessonId, CancellationToken ct)
    {
        var lesson = await GetAsync(t, lessonId, ct);
        var m = (await Rows("SELECT * FROM formative_module WHERE institution_id=@t AND version_id=@v AND id=@id", ct, ("@t", t.InstitutionId), ("@v", lesson.VersionId), ("@id", lesson.ModuleId))).Single();
        async Task<FormativeTopic[]> Topics(string table) => (await Rows($"SELECT id,description,display_order FROM {table} WHERE institution_id=@t AND version_id=@v AND module_id=@id ORDER BY display_order,id", ct, ("@t", t.InstitutionId), ("@v", lesson.VersionId), ("@id", lesson.ModuleId))).Select(r => new FormativeTopic(G(r, "id"), S(r, "description"), (int)L(r, "display_order"))).ToArray();
        return new(lesson.ModuleId, NS(m, "code"), S(m, "name"), NS(m, "description"), (int)L(m, "planned_minutes"), (int)L(m, "display_order"), lesson.SubjectId, await Topics("formative_module_competency"), await Topics("formative_module_content"));
    }

    public async Task<IReadOnlyList<CalendarHistory>> HistoryAsync(
        TenantContext t,
        Guid id,
        int page,
        int size,
        CancellationToken ct
    )
    {
        await Access(t, ct);
        RequireOperator(t);
        Page(page, size);
        await LoadLesson(t, id, ct);
        return (await Rows("SELECT * FROM institution_calendar_event WHERE institution_id=@t AND resource_id=@id AND resource_kind='Lesson' ORDER BY occurred_at_utc,id LIMIT @size OFFSET @offset", ct, ("@t", t.InstitutionId), ("@id", id), ("@size", size), ("@offset", (page - 1) * size))).Select(r => new CalendarHistory(G(r, "id"), S(r, "operation"), G(r, "actor_user_id"), G(r, "actor_membership_id"), r["revision_before"] is DBNull ? null : L(r, "revision_before"), L(r, "revision_after"), NS(r, "before_json"), S(r, "state_json"), S(r, "reason"), U(r, "occurred_at_utc"))).ToArray();
    }
}
