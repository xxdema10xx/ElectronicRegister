using System.Text.Json.Serialization;
namespace ElectronicRegisterAPI.Domain.Models;

public sealed record FormativePlanFeature(Func<bool> Availability) { public bool Enabled => Availability(); }
public sealed record FormativePlanInfo(Guid Id, Guid StudyPathId, string Code, string Name, string? Description, long Revision, DateTime CreatedAtUtc);
public sealed record FormativeBinding(Guid CourseEditionId, Guid VersionId, DateTime CreatedAtUtc);
public sealed record FormativeReceipt(Guid EventId, Guid PlanId, Guid? VersionId, long Revision, Guid? CourseEditionId = null);
public sealed record FormativeHistory(Guid EventId, Guid? VersionId, Guid? CourseEditionId, string Operation,
    Guid ActorUserId, Guid ActorMembershipId, long? RevisionBefore, long RevisionAfter,
    string? BeforeJson, string AfterJson, string Reason, DateTime OccurredAtUtc);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateFormativePlan([property: JsonRequired] Guid EventId, [property: JsonRequired] Guid StudyPathId,
    string Code, string Name, string? Description, string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RenameFormativePlan([property: JsonRequired] Guid EventId, [property: JsonRequired] long ExpectedRevision,
    string Name, string? Description, string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FormativeCommand([property: JsonRequired] Guid EventId, [property: JsonRequired] long ExpectedRevision, string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateFormativeVersion([property: JsonRequired] Guid EventId, string Name,
    [property: JsonRequired] int TheoryPlannedMinutes, [property: JsonRequired] int InternshipPlannedMinutes, string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FormativeTopicInput(Guid? Id, string Description, [property: JsonRequired] int DisplayOrder);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FormativeModuleInput(Guid? Id, string? Code, string Name, string? Description,
    [property: JsonRequired] int PlannedMinutes, [property: JsonRequired] int DisplayOrder, Guid? SubjectId,
    IReadOnlyList<FormativeTopicInput> Competencies, IReadOnlyList<FormativeTopicInput> Contents);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FormativeUnitInput(Guid? Id, string? Code, string Name, string? Description,
    [property: JsonRequired] int DisplayOrder, IReadOnlyList<FormativeModuleInput> Modules);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReplaceFormativeDraft([property: JsonRequired] Guid EventId, [property: JsonRequired] long ExpectedRevision,
    string Name, [property: JsonRequired] int TheoryPlannedMinutes, [property: JsonRequired] int InternshipPlannedMinutes,
    IReadOnlyList<FormativeUnitInput> Units, string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BindFormativeVersion([property: JsonRequired] Guid EventId, [property: JsonRequired] Guid VersionId, string Reason);
public interface IFormativeDraftBuilder { FormativePlanSnapshot Build(FormativePlanSnapshot prior, ReplaceFormativeDraft request); }
public interface IFormativePlanFaultObserver { Task CheckpointAsync(string checkpoint, CancellationToken ct); }
public sealed class NoFormativePlanFault : IFormativePlanFaultObserver
{ public Task CheckpointAsync(string checkpoint,CancellationToken ct)=>Task.CompletedTask; }

public interface IInstitutionFormativePlanRepository
{
    Task<IReadOnlyList<FormativePlanInfo>> ListAsync(TenantContext actor, int page, int size, CancellationToken ct);
    Task<FormativePlanInfo> GetAsync(TenantContext actor, Guid id, CancellationToken ct);
    Task<IReadOnlyList<FormativePlanSnapshot>> VersionsAsync(TenantContext actor, Guid planId, int page, int size, CancellationToken ct);
    Task<FormativePlanSnapshot> VersionAsync(TenantContext actor, Guid id, CancellationToken ct);
    Task<FormativeHourSummary> ValidateAsync(TenantContext actor, Guid id, CancellationToken ct);
    Task<FormativeReceipt> CreateAsync(TenantContext actor, CreateFormativePlan request, CancellationToken ct);
    Task<FormativeReceipt> RenameAsync(TenantContext actor, Guid id, RenameFormativePlan request, CancellationToken ct);
    Task<FormativeReceipt> CreateVersionAsync(TenantContext actor, Guid planId, CreateFormativeVersion request, CancellationToken ct);
    Task<FormativeReceipt> ReplaceAsync(TenantContext actor, Guid versionId, ReplaceFormativeDraft request, CancellationToken ct);
    Task<FormativeReceipt> PublishAsync(TenantContext actor, Guid versionId, FormativeCommand request, CancellationToken ct);
    Task<FormativeReceipt> CloneAsync(TenantContext actor, Guid versionId, FormativeCommand request, CancellationToken ct);
    Task<FormativeReceipt> BindAsync(TenantContext actor, Guid editionId, BindFormativeVersion request, CancellationToken ct);
    Task<FormativePlanSnapshot> EditionPlanAsync(TenantContext actor, Guid editionId, CancellationToken ct);
    Task<IReadOnlyList<FormativeHistory>> HistoryAsync(TenantContext actor, Guid planId, int page, int size, CancellationToken ct);
}
public interface IInstitutionFormativePlanManager
{
    Task<IReadOnlyList<FormativePlanInfo>> ListAsync(int page, int size, CancellationToken ct);
    Task<FormativePlanInfo> GetAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<FormativePlanSnapshot>> VersionsAsync(Guid planId, int page, int size, CancellationToken ct);
    Task<FormativePlanSnapshot> VersionAsync(Guid id, CancellationToken ct);
    Task<FormativeHourSummary> ValidateAsync(Guid id, CancellationToken ct);
    Task<FormativeReceipt> CreateAsync(CreateFormativePlan request, CancellationToken ct);
    Task<FormativeReceipt> RenameAsync(Guid id, RenameFormativePlan request, CancellationToken ct);
    Task<FormativeReceipt> CreateVersionAsync(Guid planId, CreateFormativeVersion request, CancellationToken ct);
    Task<FormativeReceipt> ReplaceAsync(Guid versionId, ReplaceFormativeDraft request, CancellationToken ct);
    Task<FormativeReceipt> PublishAsync(Guid versionId, FormativeCommand request, CancellationToken ct);
    Task<FormativeReceipt> CloneAsync(Guid versionId, FormativeCommand request, CancellationToken ct);
    Task<FormativeReceipt> BindAsync(Guid editionId, BindFormativeVersion request, CancellationToken ct);
    Task<FormativePlanSnapshot> EditionPlanAsync(Guid editionId, CancellationToken ct);
    Task<IReadOnlyList<FormativeHistory>> HistoryAsync(Guid planId, int page, int size, CancellationToken ct);
}
