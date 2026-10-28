using ElectronicRegisterAPI.Domain.Enums;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Email;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmailCreatePerson(
    Guid EventId,
    EmailCategory Category,
    long PolicyRevision,
    string FirstName,
    string LastName,
    Guid? ClassId = null
);
