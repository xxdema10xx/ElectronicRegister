using ElectronicRegisterAPI.Domain.DTOs.Calendar;
using ElectronicRegisterAPI.Domain.DTOs.Import;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Policies;
#nullable enable
using System;
using System.Linq;
using ElectronicRegisterAPI.Business.Services;
using ElectronicRegisterAPI.Domain.Models;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Unit;

public sealed class CalendarImportRulesTests
{
    private readonly CalendarImportRules rules = new();
    private static readonly string[] Headers = { "Data", "OraInizio", "OraFine", "Classe", "Modulo", "Docente", "Aula" };
    private static readonly string[] Cells = { "2026-11-05", "09:15", "11:45", "CODING-A", "DB", "DOC-001", "Lab 1" };
    private static ImportTable Table(params string[][] rows) => new("HASH", ";", Headers, rows.Select((c, i) => new ImportRawRow(i + 2, c)).ToArray());
    [Fact] public void ItalianHeadersMap() => Assert.Empty(rules.Map(Headers).Issues);
    [Fact] public void EnglishReorderedHeadersMap() => Assert.Empty(rules.Map(new[] { "Teacher", "Module", "Class", "End Time", "Date", "Start Time" }).Issues);
    [Fact] public void ValidRowsRetainReferencesWithoutGuessingIdentities()
    {
        var row = Assert.Single(rules.Validate(Table(Cells), rules.Map(Headers)));
        Assert.Empty(row.Issues); Assert.Equal("DOC-001", row.TeacherReference); Assert.Equal("DB", row.ModuleReference);
        Assert.Equal(new TimeOnly(9, 15), row.StartsAt);
    }
    [Fact] public void RequiredColumnMissing() => Assert.Contains(rules.Map(new[] { "Date", "Class" }).Issues, i => i.Code == "RequiredColumnMissing");
    [Fact] public void AliasedDuplicateColumnIsAmbiguous() => Assert.Contains(rules.Map(Headers.Append("Date").ToArray()).Issues, i => i.Code == "AmbiguousMapping");
    [Fact] public void UnknownColumnIsWarning() => Assert.Contains(rules.Map(Headers.Append("InstitutionId").ToArray()).Issues, i => i.Code == "IgnoredColumn" && i.Warning);
    [Theory] [InlineData(-1)] [InlineData(7)]
    public void InvalidColumnIndex(int index) => Assert.Throws<CalendarValidationException>(() => rules.Map(Headers, new[] { new CalendarColumnMapping(index, CalendarImportField.Date) }));
    [Fact] public void DuplicateColumnIndexRejected() => Assert.Throws<CalendarValidationException>(() => rules.Map(Headers,
        new[] { new CalendarColumnMapping(0, CalendarImportField.Date), new CalendarColumnMapping(0, CalendarImportField.StartsAt) }));
    [Fact] public void DuplicateDestinationRejected() => Assert.Throws<CalendarValidationException>(() => rules.Map(Headers,
        new[] { new CalendarColumnMapping(0, CalendarImportField.Date), new CalendarColumnMapping(1, CalendarImportField.Date) }));
    [Fact] public void UnknownEnumRejected() => Assert.Throws<CalendarValidationException>(() => rules.Map(Headers, new[] { new CalendarColumnMapping(0, (CalendarImportField)999) }));
    [Fact] public void BothDuplicateRowsAreInvalid()
    {
        var rows = rules.Validate(Table(Cells, Cells), rules.Map(Headers));
        Assert.All(rows, r => Assert.Contains(r.Issues, i => i.Code == "DuplicateWithinFile"));
    }
    [Fact] public void BadDateHasRowIssue()
    {
        var cells = Cells.ToArray(); cells[0] = "2026-02-30";
        Assert.Contains(Assert.Single(rules.Validate(Table(cells), rules.Map(Headers))).Issues, i => i.Code == "calendar.invalid-date");
    }
    [Fact] public void MissingTeacherHasRowIssue()
    {
        var cells = Cells.ToArray(); cells[5] = "";
        Assert.Contains(Assert.Single(rules.Validate(Table(cells), rules.Map(Headers))).Issues, i => i.Field == "Teacher" && i.Code == "RequiredFieldMissing");
    }
    [Fact] public void OvernightHasRowIssue()
    {
        var cells = Cells.ToArray(); cells[1] = "23:00"; cells[2] = "01:00";
        Assert.Contains(Assert.Single(rules.Validate(Table(cells), rules.Map(Headers))).Issues, i => i.Code == "calendar.same-day-positive-interval-required");
    }
    [Theory] [InlineData("=HYPERLINK(x)")] [InlineData("+formula")] [InlineData("@formula")]
    public void FormulaLikeMappedReferencesRejected(string value)
    {
        var cells = Cells.ToArray(); cells[5] = value;
        Assert.Contains(Assert.Single(rules.Validate(Table(cells), rules.Map(Headers))).Issues, i => i.Code == "InvalidValue");
    }
    [Theory]
    [InlineData("1", false, "1900-01-01")]
    [InlineData("59", false, "1900-02-28")]
    [InlineData("61", false, "1900-03-01")]
    [InlineData("0", true, "1904-01-01")]
    [InlineData("1", true, "1904-01-02")]
    [InlineData("2958465", false, "9999-12-31")]
    public void ExcelDateSystems(string raw, bool date1904, string expected) => Assert.Equal(expected, CalendarExcelValues.Date(raw, date1904));
    [Theory]
    [InlineData("60", false)] [InlineData("0", false)] [InlineData("-1", true)] [InlineData("12.5", false)]
    [InlineData("NaN", false)] [InlineData("1E3", false)] [InlineData("2957004", true)]
    public void InvalidExcelDates(string raw, bool date1904) => Assert.Throws<CalendarValidationException>(() => CalendarExcelValues.Date(raw, date1904));
    [Theory]
    [InlineData("0", "00:00")] [InlineData("0.5", "12:00")]
    [InlineData("0.385416666666667", "09:15")] [InlineData("0.489583333333333", "11:45")]
    public void ExcelTimeFractions(string raw, string expected) => Assert.Equal(expected, CalendarExcelValues.Time(raw));
    [Theory]
    [InlineData("1")] [InlineData("-0.5")] [InlineData("0.123456")]
    [InlineData("0.500011574074074")] [InlineData("NaN")] [InlineData("1E-3")]
    public void ArbitraryNumbersAndSecondsRejected(string raw) => Assert.Throws<CalendarValidationException>(() => CalendarExcelValues.Time(raw));
}
