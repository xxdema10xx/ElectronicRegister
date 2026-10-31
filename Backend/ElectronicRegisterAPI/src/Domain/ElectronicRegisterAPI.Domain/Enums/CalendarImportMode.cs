using ElectronicRegisterAPI.Domain.Serialization;
using System.Globalization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Enums;

[System.Text.Json.Serialization.JsonConverter(typeof(ImportCatalogConverter<CalendarImportMode>))]
public enum CalendarImportMode
{
    AppendDraft,
    ReplaceDraftScope
}
