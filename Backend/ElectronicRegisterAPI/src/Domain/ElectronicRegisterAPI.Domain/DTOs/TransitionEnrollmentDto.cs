using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.DTOs;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TransitionEnrollmentDto(
    Guid EventId,
    [Required] string TargetStatus,
    [Required] DateOnly? CompletionDate,
    [Required, StringLength(1000)] string Reason,
    [Required] long? ExpectedRevision
);
