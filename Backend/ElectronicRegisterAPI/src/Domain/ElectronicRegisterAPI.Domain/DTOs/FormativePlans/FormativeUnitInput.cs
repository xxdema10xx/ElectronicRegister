using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.FormativePlans;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FormativeUnitInput(
    Guid? Id,
    string? Code,
    string Name,
    string? Description,
    [property: JsonRequired] int DisplayOrder,
    IReadOnlyList<FormativeModuleInput> Modules
);
