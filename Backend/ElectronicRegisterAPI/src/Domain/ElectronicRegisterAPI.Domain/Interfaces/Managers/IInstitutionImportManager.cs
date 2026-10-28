using ElectronicRegisterAPI.Domain.DTOs.Import;
using ElectronicRegisterAPI.Domain.Enums;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Managers;

public interface IInstitutionImportManager
{
    Task<ImportBatch> PreviewAsync(
        Stream file,
        string fileName,
        ImportParsingOptions parsing,
        ImportPreviewSettings settings,
        CancellationToken ct
    );
    Task<ImportBatch> GetAsync(Guid id, CancellationToken ct);
    Task<ImportRowsPage> RowsAsync(
        Guid id,
        int page,
        int size,
        CancellationToken ct
    );
    Task<ImportBatchPage> ListAsync(int page, int size, CancellationToken ct);
    Task<ImportBatch> RemapAsync(Guid id, ImportRemap request, CancellationToken ct);
    Task<ImportResult> ConfirmAsync(Guid id, ImportConfirmation request, CancellationToken ct);
    Task<ImportResult> ResultAsync(Guid id, CancellationToken ct);
    Task<byte[]> TemplateAsync(ImportCategory category, CancellationToken ct);
}
