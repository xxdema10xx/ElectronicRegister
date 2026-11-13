using ElectronicRegisterAPI.Domain.DTOs.Import;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Persistence.Import;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Persistence;

public interface IInstitutionImportSession
{
    Task<ImportStoredBatch> GetAsync(Guid id);
    Task<ImportBatchPage> ListAsync(int page, int size);
    Task<IReadOnlyList<ImportClassMatch>> ClassesAsync(string reference, Guid? choice);
    Task<bool> ExternalExistsAsync(ImportCategory category, string reference);
    Task<bool> HomonymExistsAsync(ImportCategory category, string first, string last);
    Task<bool> PreviouslyImportedAsync(string hash, ImportCategory category, Guid except);
    Task<ImportReplay?> ReplayAsync(Guid eventId);
    Task CreateAsync(
        ImportBatch batch,
        ImportPayload payload,
        Guid eventId,
        string requestHash
    );
    Task RemapAsync(
        ImportBatch batch,
        ImportPayload payload,
        Guid eventId,
        string requestHash
    );
    Task AddCreatedAsync(
        Guid batch,
        ImportCategory category,
        ImportCreatedRow row,
        string? externalReference
    );
    // Platform identity (E4): fiscal-code HMACs that already belong to a member of this
    // institution (tenant-own data only), the accounts of a validated batch, and the current
    // platform addresses of accounts created or linked by this institution's batches.
    Task<IReadOnlySet<string>> FiscalCodesInInstitutionAsync(IReadOnlyCollection<string> hmacs);
    Task<IReadOnlyList<ImportCreatedAccount>> CreatePlatformAccountsAsync(IReadOnlyList<ImportAccountRequest> people);
    Task<IReadOnlyDictionary<Guid, string>> EmailsAsync(IReadOnlyCollection<Guid> users);
    Task AddCreatedManyAsync(
        Guid batch,
        ImportCategory category,
        IReadOnlyList<ImportCreatedRow> rows,
        IReadOnlyList<string?> externalReferences
    );
    Task CompleteAsync(
        Guid batch,
        long revision,
        Guid eventId,
        string requestHash,
        ImportResult result,
        string reason
    );
}
