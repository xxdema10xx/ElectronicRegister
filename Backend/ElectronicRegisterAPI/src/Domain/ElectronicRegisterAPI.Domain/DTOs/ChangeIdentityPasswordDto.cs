using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.DTOs;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChangeIdentityPasswordDto([Required, StringLength(1024)] string CurrentPassword, [Required, StringLength(72, MinimumLength = 8)] string NewPassword);
