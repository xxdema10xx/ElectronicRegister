using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Email;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmailHistory(
    Guid EventId,
    string Operation,
    Guid ActorUserId,
    Guid ActorMembershipId,
    string? BeforeJson,
    string AfterJson,
    string Reason,
    DateTime OccurredAtUtc
);
