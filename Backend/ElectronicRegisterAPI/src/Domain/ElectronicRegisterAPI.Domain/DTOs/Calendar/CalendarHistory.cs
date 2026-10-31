using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Calendar;

public sealed record CalendarHistory(
    Guid EventId,
    string Operation,
    Guid ActorUserId,
    Guid ActorMembershipId,
    long? RevisionBefore,
    long RevisionAfter,
    string? BeforeJson,
    string StateJson,
    string Reason,
    DateTime OccurredAtUtc
);
