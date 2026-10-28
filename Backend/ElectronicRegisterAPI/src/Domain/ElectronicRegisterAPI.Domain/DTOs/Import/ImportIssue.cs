using System.Text.Json;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Import;

public sealed record ImportIssue(
    string Field,
    string Code,
    string Message,
    bool Warning = false
);
