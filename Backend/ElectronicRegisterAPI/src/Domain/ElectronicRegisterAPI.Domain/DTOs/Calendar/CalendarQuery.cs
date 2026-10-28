using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Calendar;

public sealed record CalendarQuery(
    DateOnly From,
    DateOnly To,
    Guid? ClassId = null,
    Guid? TeacherId = null,
    Guid? ModuleId = null,
    Guid? CourseEditionId = null,
    string? Publication = null,
    string? Status = null,
    bool IncludeCancelled = false,
    int Page = 1,
    int Size = 100
);
