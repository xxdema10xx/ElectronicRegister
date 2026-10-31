using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.FormativePlans;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateFormativeVersion(
    [property: JsonRequired] Guid EventId,
    string Name,
    [property: JsonRequired] int TheoryPlannedMinutes,
    [property: JsonRequired] int InternshipPlannedMinutes,
    string Reason
);
