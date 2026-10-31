using ElectronicRegisterAPI.Domain.Enums;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Calendar;

public sealed record CalendarImportPreview(
    Guid Id,
    long Revision,
    string Digest,
    string FileSha256,
    DateTime ExpiresAtUtc,
    string Status,
    CalendarImportSettings Scope,
    int Added,
    int Replaced,
    int Preserved,
    IReadOnlyList<CalendarImportRow> Rows,
    IReadOnlyList<CalendarImportImpact> Hours,
    string ReferenceDigest = "",
    IReadOnlyList<string>? Columns = null,
    IReadOnlyList<CalendarColumnMapping>? ResolvedMapping = null
)
{
    public int TotalRows => Rows.Count;
    public int ValidRows => Rows.Count(r => r.Lesson is not null && !r.Issues.Any(i => !i.Warning) && r.Conflicts.Count == 0);
    public int InvalidRows => TotalRows - ValidRows;
    public int WarningRows => Rows.Count(r => r.Issues.Any(i => i.Warning));
    public int DuplicateRows => Rows.Count(r => r.Issues.Any(i => i.Code == "DuplicateWithinFile") || r.Conflicts.Any(c => c.Kind == CalendarConflictKind.Duplicate));
    public bool CanConfirm => Rows.Count > 0 && InvalidRows == 0;
}
