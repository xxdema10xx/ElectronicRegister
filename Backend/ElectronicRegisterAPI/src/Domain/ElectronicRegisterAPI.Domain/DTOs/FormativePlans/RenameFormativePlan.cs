using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.FormativePlans;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RenameFormativePlan(
    [property: JsonRequired] Guid EventId,
    [property: JsonRequired] long ExpectedRevision,
    string Name,
    string? Description,
    string Reason
);
