using ElectronicRegisterAPI.Domain.Enums;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Import;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ImportPreviewSettings(
    [property: JsonRequired] ImportCategory Category,
    ImportEmailMode EmailMode = ImportEmailMode.None,
    IReadOnlyList<ImportColumnMapping>? Mapping = null,
    IReadOnlyList<ImportClassChoice>? Classes = null,
    IReadOnlyList<ImportEmailCandidateChoice>? EmailCandidates = null
);
