using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.FormativePlans;

public sealed record FormativeBinding(Guid CourseEditionId, Guid VersionId, DateTime CreatedAtUtc);
