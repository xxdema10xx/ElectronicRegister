using ElectronicRegisterAPI.Domain.Models;
using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.DTOs.Email;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record GeneratedInstitutionEmail(
    string Email,
    string NormalizedEmail,
    string LocalPart,
    string Domain,
    string[] Warnings
);
