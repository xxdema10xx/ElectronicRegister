using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace ElectronicRegisterAPI.Domain.DTOs;
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TransitionEnrollmentDto(Guid EventId, [Required] string TargetStatus,
    [Required] DateOnly? CompletionDate, [Required, StringLength(1000)] string Reason, [Required] long? ExpectedRevision);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CorrectEnrollmentCompletionDto(Guid EventId, [Required] DateOnly? CompletionDate,
    [Required, StringLength(1000)] string Reason, [Required] long? ExpectedRevision);
public sealed record EnrollmentMutationResult(Guid EventId, long Revision, string Status, DateOnly? CompletionDate);
public sealed record CourseAdministrationEventDto(Guid EventId, Guid InstitutionId, Guid ActorUserId,
    Guid ActorMembershipId, Guid CourseEditionId, Guid? EnrollmentId, Guid? CampusId, Guid? ClassId,
    string Action, string? PreviousStatus, string? NewStatus, DateOnly? PreviousCompletionDate,
    DateOnly? NewCompletionDate, long? RevisionBefore, long? RevisionAfter, string? Reason,
    string? BeforeJson, string AfterJson, DateTime OccurredAtUtc);
