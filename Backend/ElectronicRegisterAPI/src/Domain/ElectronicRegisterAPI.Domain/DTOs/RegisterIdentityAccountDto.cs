using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.DTOs;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RegisterIdentityAccountDto([Required, EmailAddress, StringLength(255)] string Email, [Required, StringLength(72, MinimumLength = 8)] string Password);
