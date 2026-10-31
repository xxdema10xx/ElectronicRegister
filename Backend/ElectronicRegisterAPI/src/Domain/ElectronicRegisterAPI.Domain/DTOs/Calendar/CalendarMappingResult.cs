using ElectronicRegisterAPI.Domain.DTOs.Import;
using System.Globalization;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Calendar;

public sealed record CalendarMappingResult(IReadOnlyList<CalendarColumnMapping> Columns, IReadOnlyList<ImportIssue> Issues);
