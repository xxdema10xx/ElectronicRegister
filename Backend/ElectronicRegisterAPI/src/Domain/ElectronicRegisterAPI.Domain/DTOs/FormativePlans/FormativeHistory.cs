using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.FormativePlans;

public sealed record FormativeHistory(
    Guid EventId,
    Guid? VersionId,
    Guid? CourseEditionId,
    string Operation,
    Guid ActorUserId,
    Guid ActorMembershipId,
    long? RevisionBefore,
    long RevisionAfter,
    string? BeforeJson,
    string AfterJson,
    string Reason,
    DateTime OccurredAtUtc
);
