using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Business.Services;

public sealed class CalendarImportRules : ICalendarImportRules
{
    private static readonly CalendarImportField[] Required = [CalendarImportField.Date, CalendarImportField.StartsAt, CalendarImportField.EndsAt,
        CalendarImportField.Class, CalendarImportField.Module, CalendarImportField.Teacher];

    public CalendarMappingResult Map(IReadOnlyList<string> headers, IReadOnlyList<CalendarColumnMapping>? mapping = null)
    {
        if (headers.Count is < 1 or > 64) throw new CalendarValidationException("calendar.invalid-mapping");
        var columns = mapping?.ToArray() ?? headers.Select((h, i) => new CalendarColumnMapping(i,
            CalendarImportColumns.Resolve(h))).ToArray();
        if (columns.Any(c => c.ColumnIndex < 0 || c.ColumnIndex >= headers.Count || !Enum.IsDefined(c.Field)) ||
            columns.Select(c => c.ColumnIndex).Distinct().Count() != columns.Length ||
            mapping is not null && columns.Where(c => c.Field != CalendarImportField.Ignore).GroupBy(c => c.Field).Any(g => g.Count() > 1))
            throw new CalendarValidationException("calendar.invalid-mapping");
        var issues = new List<ImportIssue>();
        foreach (var field in Required)
            if (!columns.Any(c => c.Field == field)) issues.Add(new(field.ToString(), "RequiredColumnMissing", "Colonna obbligatoria mancante."));
        foreach (var group in columns.Where(c => c.Field != CalendarImportField.Ignore).GroupBy(c => c.Field).Where(g => g.Count() > 1))
            issues.Add(new(group.Key.ToString(), "AmbiguousMapping", "PiÃ¹ colonne corrispondono allo stesso campo."));
        foreach (var column in columns.Where(c => c.Field == CalendarImportField.Ignore))
            issues.Add(new(column.ColumnIndex.ToString(), "IgnoredColumn", "Colonna ignorata.", true));
        return new(columns, issues);
    }

    public IReadOnlyList<CalendarMappedRow> Validate(ImportTable table, CalendarMappingResult mapping)
    {
        if (table.Rows.Count > 10000 || mapping.Columns.Any(c => c.ColumnIndex < 0 || c.ColumnIndex >= table.Headers.Count))
            throw new CalendarValidationException("calendar.invalid-mapping");
        var result = new List<CalendarMappedRow>();
        foreach (var row in table.Rows)
        {
            if (row.Cells.Count != table.Headers.Count) throw new CalendarValidationException("calendar.invalid-row-width");
            var issues = new List<ImportIssue>(mapping.Issues);
            string? Value(CalendarImportField field, int max)
            {
                var matches = mapping.Columns.Where(c => c.Field == field).ToArray();
                if (matches.Length != 1) return null;
                var value = row.Cells[matches[0].ColumnIndex].Trim();
                if (value.Length == 0)
                {
                    if (Required.Contains(field)) issues.Add(new(field.ToString(), "RequiredFieldMissing", "Valore obbligatorio mancante."));
                    return null;
                }
                if (value.Length > max || value.Any(c => char.IsControl(c) || char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format) ||
                    value.Any(char.IsSurrogate) || value[0] is '=' or '+' or '-' or '@')
                { issues.Add(new(field.ToString(), "InvalidValue", "Valore non valido.")); return null; }
                return value;
            }
            DateOnly? date = null; TimeOnly? start = null, end = null;
            var dateValue = Value(CalendarImportField.Date, 10);
            var startValue = Value(CalendarImportField.StartsAt, 5); var endValue = Value(CalendarImportField.EndsAt, 5);
            try { if (dateValue is not null) date = InstitutionCalendarRules.ParseDate(dateValue); }
            catch (CalendarValidationException e) { issues.Add(new("Date", e.Code, "Data non valida; usare yyyy-MM-dd.")); }
            try { if (startValue is not null) start = InstitutionCalendarRules.ParseTime(startValue); }
            catch (CalendarValidationException e) { issues.Add(new("StartsAt", e.Code, "Ora non valida; usare HH:mm.")); }
            try { if (endValue is not null) end = InstitutionCalendarRules.ParseTime(endValue); }
            catch (CalendarValidationException e) { issues.Add(new("EndsAt", e.Code, "Ora non valida; usare HH:mm.")); }
            if (start.HasValue && end.HasValue && end <= start)
                issues.Add(new("EndsAt", "calendar.same-day-positive-interval-required", "La fine deve seguire l'inizio nello stesso giorno locale."));
            result.Add(new(row.RowNumber, date, start, end, Value(CalendarImportField.Class, 128), Value(CalendarImportField.Module, 200),
                Value(CalendarImportField.Teacher, 128), Value(CalendarImportField.Room, 200), Value(CalendarImportField.Description, 2000),
                Value(CalendarImportField.ExternalReference, 100), issues));
        }
        // Reference resolution may identify further duplicates (GUID vs stable code). This detects exact file duplicates only.
        var duplicateRows = result.Where(r => !r.Issues.Any(i => !i.Warning)).GroupBy(r =>
            (r.Date, r.StartsAt, r.EndsAt, r.ClassReference, r.ModuleReference, r.TeacherReference)).Where(g => g.Count() > 1).SelectMany(g => g).Select(r => r.RowNumber).ToHashSet();
        return result.Select(r => duplicateRows.Contains(r.RowNumber)
            ? r with { Issues = r.Issues.Append(new ImportIssue("Row", "DuplicateWithinFile", "Lezione duplicata nel file.")).ToArray() } : r).ToArray();
    }
}
