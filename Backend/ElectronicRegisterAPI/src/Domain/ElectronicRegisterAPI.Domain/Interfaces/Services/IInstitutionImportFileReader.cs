using ElectronicRegisterAPI.Domain.Configuration;
using ElectronicRegisterAPI.Domain.DTOs.Import;
using System.Text.Json;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Services;

public interface IInstitutionImportFileReader
{
    Task<ImportTable> ReadAsync(
        Stream input,
        string fileName,
        ImportParsingOptions options,
        ImportParserLimits limits,
        CancellationToken ct = default
    );
}
