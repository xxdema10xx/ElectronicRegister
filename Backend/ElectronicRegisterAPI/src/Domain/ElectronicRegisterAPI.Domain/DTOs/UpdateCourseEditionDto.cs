using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.DTOs;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateCourseEditionDto(
    [Required, StringLength(64)] string Code,
    [Required, StringLength(200)] string Name,
    DateOnly? StartDate,
    DateOnly? EndDate,
    [Required, StringLength(64, MinimumLength = 64)] string Version
);
