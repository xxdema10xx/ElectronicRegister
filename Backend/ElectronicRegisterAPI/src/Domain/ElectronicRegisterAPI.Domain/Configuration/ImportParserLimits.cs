using System.Text.Json;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Configuration;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ImportParserLimits(
    int MaxFileBytes = 10 * 1024 * 1024,
    int MaxDataRows = 2000,
    int MaxColumns = 16,
    int MaxCellCharacters = 512,
    int MaxRecordCharacters = 8192,
    int MaxZipEntries = 128,
    int MaxUncompressedBytes = 40 * 1024 * 1024,
    int MaxXmlNodes = 200000
)
{
    public void Validate()
    {
        if (MaxFileBytes is < 1 or > 64 * 1024 * 1024 || MaxDataRows is < 1 or > 10000 || MaxColumns is < 2 or > 64 || MaxCellCharacters is < 1 or > 4096 || MaxRecordCharacters < MaxCellCharacters || MaxRecordCharacters > 1024 * 1024 || MaxZipEntries is < 4 or > 256 || MaxUncompressedBytes is < 1 or > 128 * 1024 * 1024 || MaxXmlNodes is < 1 or > 1000000)
            throw new ArgumentException("Limiti importazione non validi.");
    }
}
