using ElectronicRegisterAPI.Domain.DTOs.Calendar;
using ElectronicRegisterAPI.Domain.DTOs.Import;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Exceptions;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Services;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

// Real CSV/Open XML component tests; no HTTP or MySQL certification is implied.
public sealed class InstitutionCalendarFileReaderTests
{
    private static Task<ImportTable> Read(byte[] bytes, string name = "calendar.xlsx", ImportParsingOptions? options = null,
        CalendarColumnMapping[]? mapping = null) => new InstitutionImportFileReader().ReadCalendarAsync(new MemoryStream(bytes), name, options ?? new(), new(), mapping);

    private static byte[] Workbook(bool date1904 = false, string date = "46331", uint dateFormat = 14, string time = "0.385416666666667",
        uint timeFormat = 20, bool formula = false, bool numericReference = false, bool customHeaders = false)
    {
        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true))
        {
            var book = document.AddWorkbookPart();
            book.Workbook = new Workbook(new WorkbookProperties { Date1904 = date1904 });
            var styles = book.AddNewPart<WorkbookStylesPart>();
            styles.Stylesheet = new Stylesheet(new CellFormats(new CellFormat { NumberFormatId = 0 },
                new CellFormat { NumberFormatId = dateFormat }, new CellFormat { NumberFormatId = timeFormat }));
            styles.Stylesheet.Save();
            var part = book.AddNewPart<WorksheetPart>(); var data = new SheetData(); part.Worksheet = new Worksheet(data);
            var sheets = book.Workbook.AppendChild(new Sheets()); sheets.Append(new Sheet { Id = book.GetIdOfPart(part), SheetId = 1, Name = "Calendar" });
            var header = new Row { RowIndex = 1 };
            var headers = customHeaders ? new[] { "D", "S", "E", "C", "M", "T" } : new[] { "Data", "OraInizio", "OraFine", "Classe", "Modulo", "Docente" };
            for (var i = 0; i < headers.Length; i++) header.Append(new Cell { CellReference = $"{(char)('A' + i)}1", DataType = CellValues.InlineString, InlineString = new InlineString(new Text(headers[i])) });
            data.Append(header);
            var row = new Row { RowIndex = 2 };
            row.Append(new Cell { CellReference = "A2", StyleIndex = 1, DataType = CellValues.Number, CellValue = new CellValue(date) });
            row.Append(new Cell { CellReference = "B2", StyleIndex = 2, DataType = CellValues.Number, CellValue = new CellValue(time) });
            row.Append(new Cell { CellReference = "C2", StyleIndex = 2, DataType = CellValues.Number, CellValue = new CellValue("0.489583333333333") });
            foreach (var (column, text) in new[] { ("D2", "CODING-A"), ("E2", "DB"), ("F2", "DOC-001") })
                row.Append(new Cell { CellReference = column, DataType = CellValues.InlineString, InlineString = new InlineString(new Text(text)) });
            if (formula) row.Elements<Cell>().First().CellFormula = new CellFormula("TODAY()");
            if (numericReference) { var cell = row.Elements<Cell>().Last(); cell.InlineString = null; cell.DataType = CellValues.Number; cell.CellValue = new CellValue("123"); }
            data.Append(row); part.Worksheet.Save(); book.Workbook.Save();
        }
        return stream.ToArray();
    }

    [Theory] [InlineData(";", false)] [InlineData(",", false)] [InlineData(";", true)] [InlineData(",", true)]
    public async Task CsvReusesUtf8Reader(string delimiter, bool bom)
    {
        var text = string.Join(delimiter, new[] { "Docente", "Data", "OraFine", "Classe", "Modulo", "OraInizio" }) + "\n" +
            string.Join(delimiter, new[] { "DOC-001", "2026-11-05", "11:45", "CODING-A", "DB", "09:15" });
        var bytes = Encoding.UTF8.GetBytes((bom ? "\uFEFF" : "") + text);
        var table = await Read(bytes, "calendar.csv"); Assert.Single(table.Rows); Assert.Equal("DOC-001", table.Rows[0].Cells[0]);
    }
    [Fact] public async Task StyledExcelDateAndTimeBecomeExplicitIso()
    {
        var table = await Read(Workbook()); Assert.Equal("2026-11-05", table.Rows[0].Cells[0]);
        Assert.Equal("09:15", table.Rows[0].Cells[1]); Assert.Equal("11:45", table.Rows[0].Cells[2]);
    }
    [Fact] public async Task Date1904IsHonored() => Assert.Equal("1904-01-01", (await Read(Workbook(date1904: true, date: "0"))).Rows[0].Cells[0]);
    [Fact] public async Task LegacyAnagraphicReaderStillRejectsNumericDates() => Assert.Equal("UnsupportedCellType",
        (await Assert.ThrowsAsync<ImportInputException>(() => new InstitutionImportFileReader().ReadAsync(new MemoryStream(Workbook()), "people.xlsx", new(), new()))).Code);
    [Theory] [InlineData(0)] [InlineData(22)] [InlineData(164)]
    public async Task UnstyledCombinedOrCustomDateIsRejected(int format) => Assert.Equal("AmbiguousCalendarCell",
        (await Assert.ThrowsAsync<ImportInputException>(() => Read(Workbook(dateFormat: (uint)format)))).Code);
    [Theory] [InlineData(0)] [InlineData(18)] [InlineData(46)] [InlineData(164)]
    public async Task AmbiguousTimeFormatRejected(int format) => Assert.Equal("AmbiguousCalendarCell",
        (await Assert.ThrowsAsync<ImportInputException>(() => Read(Workbook(timeFormat: (uint)format)))).Code);
    [Fact] public async Task FictitiousLeapDayRejected() => Assert.Equal("calendar.invalid-excel-date",
        (await Assert.ThrowsAsync<ImportInputException>(() => Read(Workbook(date: "60")))).Code);
    [Fact] public async Task SecondsAreNotRounded() => Assert.Equal("calendar.excel-minute-precision-required",
        (await Assert.ThrowsAsync<ImportInputException>(() => Read(Workbook(time: "0.500011574074074")))).Code);
    [Fact] public async Task FormulasRejectedWithCachedValues() => Assert.Equal("FormulaNotAllowed",
        (await Assert.ThrowsAsync<ImportInputException>(() => Read(Workbook(formula: true)))).Code);
    [Fact] public async Task NumericTeacherReferenceIsNotCoerced() => Assert.Equal("AmbiguousCalendarCell",
        (await Assert.ThrowsAsync<ImportInputException>(() => Read(Workbook(numericReference: true)))).Code);
    [Fact] public async Task ManualMappingAppliesToNumericCells()
    {
        var fields = new[] { CalendarImportField.Date, CalendarImportField.StartsAt, CalendarImportField.EndsAt, CalendarImportField.Class, CalendarImportField.Module, CalendarImportField.Teacher };
        var table = await Read(Workbook(customHeaders: true), mapping: fields.Select((field, i) => new CalendarColumnMapping(i, field)).ToArray());
        Assert.Equal("2026-11-05", table.Rows[0].Cells[0]);
    }
    [Fact] public async Task ExistingPreflightRejectsDtdInCalendarMode()
    {
        using var stream = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, true))
        { using var writer = new StreamWriter(zip.CreateEntry("xl/workbook.xml").Open()); writer.Write("<!DOCTYPE x [<!ENTITY a SYSTEM 'file:///invalid'>]><x>&a;</x>"); }
        Assert.Equal("MalformedWorkbook", (await Assert.ThrowsAsync<ImportInputException>(() => Read(stream.ToArray()))).Code);
    }
}
