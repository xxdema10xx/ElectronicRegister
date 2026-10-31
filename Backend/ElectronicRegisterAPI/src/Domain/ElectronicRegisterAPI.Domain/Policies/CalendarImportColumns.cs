using ElectronicRegisterAPI.Domain.Enums;
using System.Globalization;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Policies;

public static class CalendarImportColumns
{
    private static readonly IReadOnlyDictionary<string, CalendarImportField> Aliases = new Dictionary<string, CalendarImportField>(StringComparer.OrdinalIgnoreCase)
    {
        ["Data"] = CalendarImportField.Date,
        ["Date"] = CalendarImportField.Date,
        ["OraInizio"] = CalendarImportField.StartsAt,
        ["Ora inizio"] = CalendarImportField.StartsAt,
        ["StartsAt"] = CalendarImportField.StartsAt,
        ["Start Time"] = CalendarImportField.StartsAt,
        ["OraFine"] = CalendarImportField.EndsAt,
        ["Ora fine"] = CalendarImportField.EndsAt,
        ["EndsAt"] = CalendarImportField.EndsAt,
        ["End Time"] = CalendarImportField.EndsAt,
        ["Classe"] = CalendarImportField.Class,
        ["Class"] = CalendarImportField.Class,
        ["Modulo"] = CalendarImportField.Module,
        ["Module"] = CalendarImportField.Module,
        ["Docente"] = CalendarImportField.Teacher,
        ["Teacher"] = CalendarImportField.Teacher,
        ["Aula"] = CalendarImportField.Room,
        ["Room"] = CalendarImportField.Room,
        ["Descrizione"] = CalendarImportField.Description,
        ["Description"] = CalendarImportField.Description,
        ["Argomento"] = CalendarImportField.Description,
        ["RiferimentoEsterno"] = CalendarImportField.ExternalReference,
        ["ExternalReference"] = CalendarImportField.ExternalReference
    };
    public static CalendarImportField Resolve(string header) => Aliases.TryGetValue(header.Trim(), out var field) ? field : CalendarImportField.Ignore;
}
