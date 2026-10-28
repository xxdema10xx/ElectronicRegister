using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.FormativePlans;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReplaceFormativeDraft(
    [property: JsonRequired] Guid EventId,
    [property: JsonRequired] long ExpectedRevision,
    string Name,
    [property: JsonRequired] int TheoryPlannedMinutes,
    [property: JsonRequired] int InternshipPlannedMinutes,
    IReadOnlyList<FormativeUnitInput> Units,
    string Reason
);
