using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.Models;

public sealed record CalendarFeature(Func<bool> Availability) { public bool Enabled => Availability(); }
public sealed class CalendarConflictException(string code, object? details = null) : Exception(code)
{ public string Code { get; } = code; public object? Details { get; } = details; }
public interface ICalendarFaultObserver { Task CheckpointAsync(string name, CancellationToken ct); }
public sealed class NoCalendarFault : ICalendarFaultObserver { public Task CheckpointAsync(string name, CancellationToken ct) => Task.CompletedTask; }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CalendarCommand([property: JsonRequired] Guid EventId, [property: JsonRequired] long ExpectedRevision, string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CalendarSettingsCommand([property: JsonRequired] Guid EventId, [property: JsonRequired] long ExpectedRevision,
    string TimeZoneId, [property: JsonRequired] int ApprovalLeadDays, string Reason);
public sealed record CalendarSettings(string TimeZoneId, int ApprovalLeadDays, long Revision);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CalendarLessonInput([property: JsonRequired] Guid ClassId, [property: JsonRequired] Guid ModuleId,
    [property: JsonRequired] Guid TeacherId, [property: JsonRequired] DateOnly Date,
    [property: JsonRequired] TimeOnly StartsAt, [property: JsonRequired] TimeOnly EndsAt,
    string? SubstitutionReason = null, string? Room = null, string? Description = null, string? ExternalReference = null, Guid? RecoveryOfLessonId = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CalendarSaveLesson([property: JsonRequired] Guid EventId, [property: JsonRequired] long ExpectedRevision,
    CalendarLessonInput Lesson, string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CalendarDeliveredCommand([property: JsonRequired] Guid EventId, [property: JsonRequired] long ExpectedRevision,
    [property: JsonRequired] int DeliveredMinutes, string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CalendarRescheduleCommand([property: JsonRequired] Guid EventId, [property: JsonRequired] long ExpectedRevision,
    [property: JsonRequired] DateOnly Date, [property: JsonRequired] TimeOnly StartsAt, [property: JsonRequired] TimeOnly EndsAt, string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CalendarSubstituteCommand([property: JsonRequired] Guid EventId, [property: JsonRequired] long ExpectedRevision,
    [property: JsonRequired] Guid TeacherId, string Reason);
public sealed record CalendarReceipt(Guid EventId, Guid ResourceId, long Revision, IReadOnlyList<Guid>? LessonIds = null);

// Public lesson projection deliberately excludes actor IDs and administrative reasons.
public sealed record CalendarLesson(Guid Id, Guid PeriodId, Guid ClassId, Guid CourseEditionId, Guid VersionId,
    Guid ModuleId, Guid SubjectId, Guid ClassSubjectId, Guid OriginalTeacherId, Guid TeacherId,
    DateOnly Date, TimeOnly StartsAt, TimeOnly EndsAt, string TimeZoneId, DateTime StartUtc, DateTime EndUtc,
    string Publication, string Status, long Revision, int? DeliveredMinutes, Guid? RecoveryOfLessonId,
    string? Room, string? Description, string? ExternalReference, string? ClassName = null, string? ModuleName = null, string? TeacherName = null)
{ public int DurationMinutes => checked((int)(EndUtc-StartUtc).TotalMinutes); }
public sealed record CalendarPeriod(Guid Id, Guid ClassId, DateOnly Month, string Status, long Revision,
    Guid? PublishedByUserId, Guid? PublishedByMembershipId, DateTime? PublishedAtUtc, CalendarApproval Approval);
public sealed record CalendarHistory(Guid EventId, string Operation, Guid ActorUserId, Guid ActorMembershipId,
    long? RevisionBefore, long RevisionAfter, string? BeforeJson, string StateJson, string Reason, DateTime OccurredAtUtc);
public sealed record CalendarMonthlyHours(int Year, int Month, long ScheduledMinutes, long DeliveredMinutes);
public sealed record CalendarUnitHours(Guid UnitId, long PlannedMinutes, long ScheduledMinutes, long DeliveredMinutes);
public sealed record CalendarHoursReport(Guid ClassId, Guid CourseEditionId, Guid VersionId,
    IReadOnlyList<CalendarModuleHours> Modules, IReadOnlyList<CalendarUnitHours> Units, IReadOnlyList<CalendarMonthlyHours> Months)
{
    public long PlannedMinutes => Modules.Sum(m=>m.PlannedMinutes);
    public long ScheduledMinutes => Modules.Sum(m=>m.ScheduledMinutes);
    public long DeliveredMinutes => Modules.Sum(m=>m.DeliveredMinutes);
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CalendarImportSettings([property: JsonRequired] Guid EventId,
    [property: JsonRequired] int Year, [property: JsonRequired] int Month,
    [property: JsonRequired] IReadOnlyList<Guid> ClassIds, [property: JsonRequired] CalendarImportMode Mode,
    IReadOnlyList<CalendarColumnMapping>? Mapping, string Reason);
public sealed record CalendarImportRow(int RowNumber, CalendarLessonInput? Lesson, string? ClassName, string? ModuleName,
    string? TeacherName, int? DurationMinutes, IReadOnlyList<ImportIssue> Issues, IReadOnlyList<CalendarConflict> Conflicts, Guid PreviewLessonId,
    string? ClassReference = null, string? ModuleReference = null, string? TeacherReference = null);
public sealed record CalendarImportImpact(Guid ClassId, Guid ModuleId, long PlannedMinutes, long PublishedMinutes,
    long DraftMinutesAfterImport, long IncomingMinutes)
{
    public long ProjectedScheduledMinutes => PublishedMinutes+DraftMinutesAfterImport;
    public string? Warning => ProjectedScheduledMinutes<PlannedMinutes?"calendar.under-scheduled":ProjectedScheduledMinutes>PlannedMinutes?"calendar.over-scheduled":null;
}
public sealed record CalendarImportPreview(Guid Id, long Revision, string Digest, string FileSha256,
    DateTime ExpiresAtUtc, string Status, CalendarImportSettings Scope, int Added, int Replaced, int Preserved,
    IReadOnlyList<CalendarImportRow> Rows, IReadOnlyList<CalendarImportImpact> Hours, string ReferenceDigest = "",
    IReadOnlyList<string>? Columns = null, IReadOnlyList<CalendarColumnMapping>? ResolvedMapping = null)
{
    public int TotalRows => Rows.Count;
    public int ValidRows => Rows.Count(r=>r.Lesson is not null&&!r.Issues.Any(i=>!i.Warning)&&r.Conflicts.Count==0);
    public int InvalidRows => TotalRows-ValidRows;
    public int WarningRows => Rows.Count(r=>r.Issues.Any(i=>i.Warning));
    public int DuplicateRows => Rows.Count(r=>r.Issues.Any(i=>i.Code=="DuplicateWithinFile")||r.Conflicts.Any(c=>c.Kind==CalendarConflictKind.Duplicate));
    public bool CanConfirm => Rows.Count>0 && InvalidRows==0;
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CalendarConfirmImport([property: JsonRequired] Guid EventId, [property: JsonRequired] long ExpectedRevision,
    string Digest, [property: JsonRequired] bool AcknowledgeWarnings, string Reason);

public sealed record CalendarQuery(DateOnly From, DateOnly To, Guid? ClassId = null, Guid? TeacherId = null,
    Guid? ModuleId = null, Guid? CourseEditionId = null, string? Publication = null, string? Status = null,
    bool IncludeCancelled = false, int Page = 1, int Size = 100);

public interface IInstitutionCalendarRepository
{
    Task<CalendarSettings> SettingsAsync(TenantContext actor, CancellationToken ct);
    Task<CalendarReceipt> ConfigureAsync(TenantContext actor, CalendarSettingsCommand command, CancellationToken ct);
    Task<IReadOnlyList<CalendarLesson>> ListAsync(TenantContext actor, CalendarQuery query, string view, CancellationToken ct);
    Task<CalendarLesson> GetAsync(TenantContext actor, Guid id, CancellationToken ct);
    Task<FormativeModuleSnapshot> ModuleAsync(TenantContext actor, Guid lessonId, CancellationToken ct);
    Task<CalendarPeriod> PeriodAsync(TenantContext actor, Guid classId, int year, int month, CancellationToken ct);
    Task<CalendarReceipt> SaveAsync(TenantContext actor, Guid? id, CalendarSaveLesson command, CancellationToken ct);
    Task<CalendarReceipt> PublishAsync(TenantContext actor, Guid classId, int year, int month, CalendarCommand command, CancellationToken ct);
    Task<CalendarReceipt> TransitionAsync(TenantContext actor, Guid id, string operation, CalendarCommand command,
        CalendarDeliveredCommand? delivered, CalendarRescheduleCommand? reschedule, CalendarSubstituteCommand? substitute, CancellationToken ct);
    Task<IReadOnlyList<CalendarHistory>> HistoryAsync(TenantContext actor, Guid id, int page, int size, CancellationToken ct);
    Task<CalendarHoursReport> HoursAsync(TenantContext actor, Guid classId, CancellationToken ct);
    Task<CalendarImportPreview> PreviewAsync(TenantContext actor, ImportTable table, CalendarImportSettings settings, CancellationToken ct);
    Task<CalendarImportPreview> ImportAsync(TenantContext actor, Guid id, CancellationToken ct);
    Task<CalendarReceipt> ConfirmAsync(TenantContext actor, Guid id, CalendarConfirmImport command, CancellationToken ct);
}

public interface IInstitutionCalendarManager
{
    Task<CalendarSettings> SettingsAsync(CancellationToken ct);
    Task<CalendarReceipt> ConfigureAsync(CalendarSettingsCommand command, CancellationToken ct);
    Task<IReadOnlyList<CalendarLesson>> ListAsync(CalendarQuery query, string view, CancellationToken ct);
    Task<CalendarLesson> GetAsync(Guid id, CancellationToken ct);
    Task<FormativeModuleSnapshot> ModuleAsync(Guid lessonId, CancellationToken ct);
    Task<CalendarPeriod> PeriodAsync(Guid classId, int year, int month, CancellationToken ct);
    Task<CalendarReceipt> SaveAsync(Guid? id, CalendarSaveLesson command, CancellationToken ct);
    Task<CalendarReceipt> PublishAsync(Guid classId, int year, int month, CalendarCommand command, CancellationToken ct);
    Task<CalendarReceipt> TransitionAsync(Guid id, string operation, CalendarCommand command, CalendarDeliveredCommand? delivered,
        CalendarRescheduleCommand? reschedule, CalendarSubstituteCommand? substitute, CancellationToken ct);
    Task<IReadOnlyList<CalendarHistory>> HistoryAsync(Guid id, int page, int size, CancellationToken ct);
    Task<CalendarHoursReport> HoursAsync(Guid classId, CancellationToken ct);
    Task<CalendarImportPreview> PreviewAsync(Stream file, string fileName, ImportParsingOptions parsing, CalendarImportSettings settings, CancellationToken ct);
    Task<CalendarImportPreview> ImportAsync(Guid id, CancellationToken ct);
    Task<CalendarReceipt> ConfirmAsync(Guid id, CalendarConfirmImport command, CancellationToken ct);
}
