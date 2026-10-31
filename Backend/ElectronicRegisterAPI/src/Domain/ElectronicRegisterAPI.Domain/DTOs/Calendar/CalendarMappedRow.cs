using ElectronicRegisterAPI.Domain.DTOs.Import;
using System.Globalization;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Calendar;

public sealed record CalendarMappedRow(
    int RowNumber,
    DateOnly? Date,
    TimeOnly? StartsAt,
    TimeOnly? EndsAt,
    string? ClassReference,
    string? ModuleReference,
    string? TeacherReference,
    string? Room,
    string? Description,
    string? ExternalReference,
    IReadOnlyList<ImportIssue> Issues
);
