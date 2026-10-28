using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.DTOs;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CorrectEnrollmentCompletionDto(
    Guid EventId,
    [Required] DateOnly? CompletionDate,
    [Required, StringLength(1000)] string Reason,
    [Required] long? ExpectedRevision
);
