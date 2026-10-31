using ElectronicRegisterAPI.Domain.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Enums;

[JsonConverter(typeof(ImportCatalogConverter<ImportEmailMode>))]
public enum ImportEmailMode
{
    None,
    ReserveInstitutionalAddress
}
