using ElectronicRegisterAPI.Domain.Enums;
using System.Globalization;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Calendar;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CalendarColumnMapping([property: JsonRequired] int ColumnIndex, [property: JsonRequired] CalendarImportField Field);
