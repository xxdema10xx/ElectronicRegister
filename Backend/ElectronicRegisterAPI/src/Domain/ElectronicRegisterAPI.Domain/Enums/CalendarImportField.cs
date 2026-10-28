using ElectronicRegisterAPI.Domain.Serialization;
using System.Globalization;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Enums;

[JsonConverter(typeof(ImportCatalogConverter<CalendarImportField>))]
public enum CalendarImportField
{
    Ignore,
    Date,
    StartsAt,
    EndsAt,
    Class,
    Module,
    Teacher,
    Room,
    Description,
    ExternalReference
}
