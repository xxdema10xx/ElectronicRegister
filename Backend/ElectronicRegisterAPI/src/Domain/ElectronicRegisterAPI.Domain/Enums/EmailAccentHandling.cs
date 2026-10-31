using ElectronicRegisterAPI.Domain.Serialization;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Enums;

[JsonConverter(typeof(EmailCatalogConverter<EmailAccentHandling>))]
public enum EmailAccentHandling
{
    RejectNonAscii,
    LatinFoldV1
}
