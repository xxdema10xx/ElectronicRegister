using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.FormativePlans;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FormativeModuleInput(
    Guid? Id,
    string? Code,
    string Name,
    string? Description,
    [property: JsonRequired] int PlannedMinutes,
    [property: JsonRequired] int DisplayOrder,
    Guid? SubjectId,
    IReadOnlyList<FormativeTopicInput> Competencies,
    IReadOnlyList<FormativeTopicInput> Contents
);
