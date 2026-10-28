using ElectronicRegisterAPI.Domain.Enums;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Email;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmailPreviewRequest(
    EmailCategory Category,
    long? PolicyRevision,
    string? FirstName,
    string? LastName,
    EmailTemplate? Draft = null
);
