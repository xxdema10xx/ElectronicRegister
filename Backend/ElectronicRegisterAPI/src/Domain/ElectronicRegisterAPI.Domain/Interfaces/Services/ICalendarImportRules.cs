using ElectronicRegisterAPI.Domain.DTOs.Calendar;
using ElectronicRegisterAPI.Domain.DTOs.Import;
using System.Globalization;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Services;

public interface ICalendarImportRules
{
    CalendarMappingResult Map(IReadOnlyList<string> headers, IReadOnlyList<CalendarColumnMapping>? mapping = null);
    IReadOnlyList<CalendarMappedRow> Validate(ImportTable table, CalendarMappingResult mapping);
}
