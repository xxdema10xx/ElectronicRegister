using ElectronicRegisterAPI.Domain.DTOs.Email;
using ElectronicRegisterAPI.Domain.DTOs.Import;
using ElectronicRegisterAPI.Domain.Enums;
using System.Text.Json;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Services;

public interface IInstitutionImportRules
{
    ImportMappingResult Map(IReadOnlyList<string> headers, ImportCategory category, IReadOnlyList<ImportColumnMapping>? explicitMapping = null);
    IReadOnlyList<ImportValidatedRow> Validate(
        ImportTable table,
        ImportCategory category,
        ImportMappingResult mapping,
        ImportEmailMode emailMode = ImportEmailMode.None,
        EmailPolicyVersion? policy = null,
        IReadOnlyDictionary<int, int>? emailCandidateOffsets = null
    );
}
