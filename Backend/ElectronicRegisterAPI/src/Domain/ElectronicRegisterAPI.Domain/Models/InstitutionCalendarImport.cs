using System.Globalization;
using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.Models;

[JsonConverter(typeof(ImportCatalogConverter<CalendarImportField>))]
public enum CalendarImportField { Ignore, Date, StartsAt, EndsAt, Class, Module, Teacher, Room, Description, ExternalReference }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CalendarColumnMapping([property: JsonRequired] int ColumnIndex, [property: JsonRequired] CalendarImportField Field);
public sealed record CalendarMappedRow(int RowNumber, DateOnly? Date, TimeOnly? StartsAt, TimeOnly? EndsAt,
    string? ClassReference, string? ModuleReference, string? TeacherReference, string? Room, string? Description,
    string? ExternalReference, IReadOnlyList<ImportIssue> Issues);
public sealed record CalendarMappingResult(IReadOnlyList<CalendarColumnMapping> Columns, IReadOnlyList<ImportIssue> Issues);

public interface ICalendarImportRules
{
    CalendarMappingResult Map(IReadOnlyList<string> headers, IReadOnlyList<CalendarColumnMapping>? mapping = null);
    IReadOnlyList<CalendarMappedRow> Validate(ImportTable table, CalendarMappingResult mapping);
}

public interface ICalendarImportFileReader
{
    Task<ImportTable> ReadCalendarAsync(Stream input, string fileName, ImportParsingOptions options,
        ImportParserLimits limits, IReadOnlyList<CalendarColumnMapping>? mapping = null, CancellationToken ct = default);
}

// Conversion is deliberately opt-in and only for styled numeric cells in calendar fields.
public static class CalendarExcelValues
{
    public static string Date(string raw, bool date1904)
    {
        if (!decimal.TryParse(raw, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out var serial) || serial < 0 || serial != decimal.Truncate(serial) ||
            serial > (date1904 ? 2_957_003 : 2_958_465) || (!date1904 && serial < 1) || (!date1904 && serial == 60))
            throw new CalendarValidationException("calendar.invalid-excel-date");
        // Excel's fictitious 1900-02-29 is never mapped to a real day.
        try
        {
            var origin = date1904 ? new DateOnly(1904, 1, 1) : new DateOnly(1899, 12, 31);
            var days = checked((int)serial) - (!date1904 && serial > 60 ? 1 : 0);
            return origin.AddDays(days).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        catch (ArgumentOutOfRangeException) { throw new CalendarValidationException("calendar.invalid-excel-date"); }
    }

    public static string Time(string raw)
    {
        if (!decimal.TryParse(raw, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var serial) || serial < 0 || serial >= 1)
            throw new CalendarValidationException("calendar.invalid-excel-time");
        var minutes = serial * 1440;
        var rounded = decimal.Round(minutes, 0, MidpointRounding.ToEven);
        // Excel serials are IEEE754 approximations. Permit only sub-microsecond representation noise,
        // never rounding a genuine second or an arbitrary fractional minute into an appointment.
        if (decimal.Abs(minutes - rounded) > 0.00000001m || rounded >= 1440)
            throw new CalendarValidationException("calendar.excel-minute-precision-required");
        return new TimeOnly((int)rounded / 60, (int)rounded % 60).ToString("HH:mm", CultureInfo.InvariantCulture);
    }
}

public static class CalendarImportColumns
{
    private static readonly IReadOnlyDictionary<string, CalendarImportField> Aliases = new Dictionary<string, CalendarImportField>(StringComparer.OrdinalIgnoreCase)
    {
        ["Data"] = CalendarImportField.Date, ["Date"] = CalendarImportField.Date,
        ["OraInizio"] = CalendarImportField.StartsAt, ["Ora inizio"] = CalendarImportField.StartsAt, ["StartsAt"] = CalendarImportField.StartsAt, ["Start Time"] = CalendarImportField.StartsAt,
        ["OraFine"] = CalendarImportField.EndsAt, ["Ora fine"] = CalendarImportField.EndsAt, ["EndsAt"] = CalendarImportField.EndsAt, ["End Time"] = CalendarImportField.EndsAt,
        ["Classe"] = CalendarImportField.Class, ["Class"] = CalendarImportField.Class,
        ["Modulo"] = CalendarImportField.Module, ["Module"] = CalendarImportField.Module,
        ["Docente"] = CalendarImportField.Teacher, ["Teacher"] = CalendarImportField.Teacher,
        ["Aula"] = CalendarImportField.Room, ["Room"] = CalendarImportField.Room,
        ["Descrizione"] = CalendarImportField.Description, ["Description"] = CalendarImportField.Description, ["Argomento"] = CalendarImportField.Description,
        ["RiferimentoEsterno"] = CalendarImportField.ExternalReference, ["ExternalReference"] = CalendarImportField.ExternalReference
    };
    public static CalendarImportField Resolve(string header) => Aliases.TryGetValue(header.Trim(), out var field) ? field : CalendarImportField.Ignore;
}
