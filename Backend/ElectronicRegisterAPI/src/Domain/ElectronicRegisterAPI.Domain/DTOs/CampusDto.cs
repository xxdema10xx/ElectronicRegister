using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.DTOs;

public sealed record CampusDto(
    Guid CampusId,
    string Name,
    string? Address,
    string? City,
    string? Province,
    bool? IsActive
);
