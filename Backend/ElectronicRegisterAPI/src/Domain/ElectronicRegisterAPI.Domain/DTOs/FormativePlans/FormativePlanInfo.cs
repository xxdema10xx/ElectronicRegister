using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.FormativePlans;

public sealed record FormativePlanInfo(
    Guid Id,
    Guid StudyPathId,
    string Code,
    string Name,
    string? Description,
    long Revision,
    DateTime CreatedAtUtc
);
