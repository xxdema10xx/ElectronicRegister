using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Email;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmailRecipient(
    Guid? StudentId,
    Guid? TeacherId,
    Guid? MembershipId,
    string? FirstName = null,
    string? LastName = null
);
