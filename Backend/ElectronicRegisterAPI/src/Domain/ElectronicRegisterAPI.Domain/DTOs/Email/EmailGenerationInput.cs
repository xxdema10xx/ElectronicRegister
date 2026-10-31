using ElectronicRegisterAPI.Domain.Models;
using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.DTOs.Email;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmailGenerationInput(string? FirstName, string? LastName);
