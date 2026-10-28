using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.DTOs;

public sealed record CourseAdministrationEventDto(
    Guid EventId,
    Guid InstitutionId,
    Guid ActorUserId,
    Guid ActorMembershipId,
    Guid CourseEditionId,
    Guid? EnrollmentId,
    Guid? CampusId,
    Guid? ClassId,
    string Action,
    string? PreviousStatus,
    string? NewStatus,
    DateOnly? PreviousCompletionDate,
    DateOnly? NewCompletionDate,
    long? RevisionBefore,
    long? RevisionAfter,
    string? Reason,
    string? BeforeJson,
    string AfterJson,
    DateTime OccurredAtUtc
);
