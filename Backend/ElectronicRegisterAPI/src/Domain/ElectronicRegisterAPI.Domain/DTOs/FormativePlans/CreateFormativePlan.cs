using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.FormativePlans;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateFormativePlan(
    [property: JsonRequired] Guid EventId,
    [property: JsonRequired] Guid StudyPathId,
    string Code,
    string Name,
    string? Description,
    string Reason
);
