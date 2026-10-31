using ElectronicRegisterAPI.Domain.Configuration;
using ElectronicRegisterAPI.Domain.DTOs.Calendar;
using ElectronicRegisterAPI.Domain.DTOs.Import;
using System.Globalization;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Services;

public interface ICalendarImportFileReader
{
    Task<ImportTable> ReadCalendarAsync(
        Stream input,
        string fileName,
        ImportParsingOptions options,
        ImportParserLimits limits,
        IReadOnlyList<CalendarColumnMapping>? mapping = null,
        CancellationToken ct = default
    );
}
