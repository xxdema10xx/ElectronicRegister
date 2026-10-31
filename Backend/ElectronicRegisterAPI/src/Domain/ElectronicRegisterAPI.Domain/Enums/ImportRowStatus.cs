using ElectronicRegisterAPI.Domain.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Enums;

[JsonConverter(typeof(ImportCatalogConverter<ImportRowStatus>))]
public enum ImportRowStatus
{
    Valid,
    Warning,
    Invalid,
    AmbiguousReference
}
