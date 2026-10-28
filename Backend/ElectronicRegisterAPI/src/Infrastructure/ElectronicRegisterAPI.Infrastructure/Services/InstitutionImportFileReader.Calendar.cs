using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Infrastructure.Services;

public sealed partial class InstitutionImportFileReader
{
    public Task<ImportTable> ReadCalendarAsync(Stream input, string fileName, ImportParsingOptions options,
        ImportParserLimits limits, IReadOnlyList<CalendarColumnMapping>? mapping = null, CancellationToken ct = default)
    {
        // Snapshot caller-owned mappings before awaiting file reads.
        var columns = mapping?.ToArray();
        if (columns is not null && (columns.Any(c => c.ColumnIndex < 0 || c.ColumnIndex >= limits.MaxColumns || !Enum.IsDefined(c.Field)) ||
            columns.Select(c => c.ColumnIndex).Distinct().Count() != columns.Length ||
            columns.Where(c => c.Field != CalendarImportField.Ignore).GroupBy(c => c.Field).Any(g => g.Count() > 1)))
            throw new CalendarValidationException("calendar.invalid-mapping");

        return ReadCoreAsync(input, fileName, options, limits, (cell, workbook, headers, column) =>
        {
            var field = columns is not null ? columns.SingleOrDefault(c => c.ColumnIndex == column)?.Field ?? CalendarImportField.Ignore
                : CalendarImportColumns.Resolve(column < headers.Count ? headers[column] : "");
            var formats = workbook.WorkbookStylesPart?.Stylesheet?.CellFormats?.Elements<CellFormat>().ToArray();
            var styleIndex = cell.StyleIndex?.Value ?? 0;
            if (formats is null || styleIndex >= formats.Length) throw BadWorkbook("AmbiguousCalendarCell", "La cella numerica richiede un formato data/ora esplicito.");
            var formatId = formats[styleIndex].NumberFormatId?.Value ?? 0;
            // Deliberately narrow, culture-independent subset. Custom formats must use ISO text.
            var dateFormat = formatId is 14 or 15 or 16 or 17;
            var timeFormat = formatId is 20 or 21;
            if (field == CalendarImportField.Date && dateFormat)
                return CalendarExcelValues.Date(cell.CellValue?.Text ?? "", workbook.Workbook?.WorkbookProperties?.Date1904?.Value ?? false);
            if ((field is CalendarImportField.StartsAt or CalendarImportField.EndsAt) && timeFormat)
                return CalendarExcelValues.Time(cell.CellValue?.Text ?? "");
            throw BadWorkbook("AmbiguousCalendarCell", "Numero non compatibile con il campo calendario e il formato dichiarato; usare testo ISO.");
        }, ct);
    }
}
