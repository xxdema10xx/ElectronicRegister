using System.Text.Json.Serialization;
namespace ElectronicRegisterAPI.Domain.Models;

public sealed record InstitutionImportFeature(bool Enabled,bool SchoolEnabled,ImportParserLimits Limits,int TtlMinutes=30,int MaxStageBytes=2*1024*1024,int MaxPendingPreviews=20)
{
    [JsonIgnore] public Func<bool>? CurrentAvailability {get;init;}
    public bool IsEnabled=>CurrentAvailability?.Invoke()??(Enabled&&SchoolEnabled);
    public void Validate(){Limits.Validate();if(TtlMinutes is <1 or >120||MaxStageBytes is <1024 or >3*1024*1024||MaxPendingPreviews is <1 or >100)throw new ArgumentException("Configurazione staging non valida.");}
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ImportClassChoice(int RowNumber,Guid ClassId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ImportEmailCandidateChoice(int RowNumber,int CandidateOffset);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ImportPreviewSettings([property:JsonRequired] ImportCategory Category,
    ImportEmailMode EmailMode=ImportEmailMode.None,IReadOnlyList<ImportColumnMapping>? Mapping=null,IReadOnlyList<ImportClassChoice>? Classes=null,
    IReadOnlyList<ImportEmailCandidateChoice>? EmailCandidates=null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ImportRemap(Guid EventId,long ExpectedRevision,ImportPreviewSettings Settings);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ImportConfirmation(Guid EventId,long ExpectedRevision,string PreviewDigest,bool AcknowledgeWarnings=false,
    bool AllowRepeatedFile=false,string? Reason=null);
public sealed record ImportPreparedRow(ImportValidatedRow Data,Guid? ClassId,string? ClassFingerprint,IReadOnlyList<Guid>? ClassCandidates=null);
public sealed record ImportPayload(ImportTable Table,ImportPreviewSettings Settings,ImportMappingResult Mapping,
    IReadOnlyList<ImportPreparedRow> Rows,Guid? PolicyId,long? PolicyRevision,IReadOnlyList<ImportIssue> Warnings);
public sealed record ImportBatch(Guid Id,ImportCategory Category,string State,long Revision,string FileSha256,string PreviewDigest,
    int TotalRows,int ValidRows,int InvalidRows,DateTime CreatedAtUtc,DateTime ExpiresAtUtc,long? PolicyRevision,
    IReadOnlyList<ImportIssue> Warnings,IReadOnlyList<string>? Columns=null,IReadOnlyList<ImportColumnMapping>? Mapping=null);
public sealed record ImportStoredBatch(ImportBatch Batch,ImportPayload? Payload,ImportResult? Result=null);
public sealed record ImportCreatedRow(int RowNumber,Guid? StudentId,Guid? TeacherId,Guid? EmailAssignmentId);
public sealed record ImportResult(Guid ImportId,long Revision,int CreatedCount,IReadOnlyList<ImportCreatedRow> Rows);
public sealed record ImportRowsPage(int PageNumber,int PageSize,int TotalCount,IReadOnlyList<ImportPreparedRow> Items);
public sealed record ImportBatchPage(int PageNumber,int PageSize,long TotalCount,IReadOnlyList<ImportBatch> Items);
public sealed record ImportClassMatch(Guid Id,string Fingerprint);
public sealed record ImportReplay(string RequestHash,string ResultJson);
public sealed class ImportConflictException(string code) : Exception("Il contesto dell'importazione è cambiato; verificare o rigenerare la preview.")
{public string Code {get;}=code;}

public interface IInstitutionImportSession
{
    Task<ImportStoredBatch> GetAsync(Guid id);
    Task<ImportBatchPage> ListAsync(int page,int size);
    Task<IReadOnlyList<ImportClassMatch>> ClassesAsync(string reference,Guid? choice);
    Task<bool> ExternalExistsAsync(ImportCategory category,string reference);
    Task<bool> HomonymExistsAsync(ImportCategory category,string first,string last);
    Task<bool> PreviouslyImportedAsync(string hash,ImportCategory category,Guid except);
    Task<ImportReplay?> ReplayAsync(Guid eventId);
    Task CreateAsync(ImportBatch batch,ImportPayload payload,Guid eventId,string requestHash);
    Task RemapAsync(ImportBatch batch,ImportPayload payload,Guid eventId,string requestHash);
    Task AddCreatedAsync(Guid batch,ImportCategory category,ImportCreatedRow row,string? externalReference);
    Task CompleteAsync(Guid batch,long revision,Guid eventId,string requestHash,ImportResult result,string reason);
}
public interface IInstitutionImportRepository
{
    Task<T> ExecuteAsync<T>(TenantContext actor,Func<IInstitutionImportSession,Task<T>> action,CancellationToken ct);
}
public interface IInstitutionImportManager
{
    Task<ImportBatch> PreviewAsync(Stream file,string fileName,ImportParsingOptions parsing,ImportPreviewSettings settings,CancellationToken ct);
    Task<ImportBatch> GetAsync(Guid id,CancellationToken ct);
    Task<ImportRowsPage> RowsAsync(Guid id,int page,int size,CancellationToken ct);
    Task<ImportBatchPage> ListAsync(int page,int size,CancellationToken ct);
    Task<ImportBatch> RemapAsync(Guid id,ImportRemap request,CancellationToken ct);
    Task<ImportResult> ConfirmAsync(Guid id,ImportConfirmation request,CancellationToken ct);
    Task<ImportResult> ResultAsync(Guid id,CancellationToken ct);
    Task<byte[]> TemplateAsync(ImportCategory category,CancellationToken ct);
}
public interface IInstitutionImportRetention {Task<int> PurgeExpiredAsync(CancellationToken ct);}
