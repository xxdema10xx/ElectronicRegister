using ElectronicRegisterAPI.Domain.Enums;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Email;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmailAssignment(
    Guid Id,
    Guid? StudentId,
    Guid? TeacherId,
    Guid? MembershipId,
    EmailCategory Category,
    long PolicyRevision,
    string Email,
    string State,
    long Revision,
    string FirstNameSnapshot,
    string LastNameSnapshot,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    bool MailboxVerified = false,
    bool DomainVerified = false,
    bool LoginEnabled = false
);
