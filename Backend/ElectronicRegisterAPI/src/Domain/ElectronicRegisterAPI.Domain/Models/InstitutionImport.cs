using System.Text.Json;
using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.Models;

// File parsing and pure rules are separate from tenant-authorized persistent workflows.
public sealed class ImportCatalogConverter<T> : JsonConverter<T> where T : struct, Enum
{
    public override T Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        if(reader.TokenType!=JsonTokenType.String || reader.GetString() is not {} value ||
            !Enum.GetNames<T>().Contains(value,StringComparer.Ordinal))throw new JsonException("Catalogo importazione non valido.");
        return Enum.Parse<T>(value);
    }
    public override void Write(Utf8JsonWriter writer,T value,JsonSerializerOptions options)
    {if(!Enum.IsDefined(value))throw new JsonException();writer.WriteStringValue(value.ToString());}
}
[JsonConverter(typeof(ImportCatalogConverter<ImportCategory>))]
public enum ImportCategory { Student, Teacher }
[JsonConverter(typeof(ImportCatalogConverter<ImportEmailMode>))]
public enum ImportEmailMode { None, ReserveInstitutionalAddress }
[JsonConverter(typeof(ImportCatalogConverter<ImportField>))]
public enum ImportField { Ignore, FirstName, LastName, Class, ExternalReference }
[JsonConverter(typeof(ImportCatalogConverter<ImportRowStatus>))]
public enum ImportRowStatus { Valid, Warning, Invalid, AmbiguousReference }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ImportParserLimits(int MaxFileBytes=10*1024*1024,int MaxDataRows=2000,
    int MaxColumns=16,int MaxCellCharacters=512,int MaxRecordCharacters=8192,
    int MaxZipEntries=128,int MaxUncompressedBytes=40*1024*1024,int MaxXmlNodes=200000)
{
    public void Validate()
    {
        if(MaxFileBytes is <1 or >64*1024*1024 || MaxDataRows is <1 or >10000 || MaxColumns is <2 or >64 ||
            MaxCellCharacters is <1 or >4096 || MaxRecordCharacters<MaxCellCharacters || MaxRecordCharacters>1024*1024 ||
            MaxZipEntries is <4 or >256 || MaxUncompressedBytes is <1 or >128*1024*1024 || MaxXmlNodes is <1 or >1000000)
            throw new ArgumentException("Limiti importazione non validi.");
    }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ImportParsingOptions(string? Delimiter=null,string Encoding="utf-8",string? WorksheetName=null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ImportColumnMapping(int ColumnIndex,ImportField Field);
public sealed record ImportIssue(string Field,string Code,string Message,bool Warning=false);
public sealed record ImportRawRow(int RowNumber,IReadOnlyList<string> Cells);
public sealed record ImportTable(string Sha256,string Delimiter,IReadOnlyList<string> Headers,IReadOnlyList<ImportRawRow> Rows);
public sealed record ImportMappingResult(IReadOnlyList<ImportColumnMapping> Columns,IReadOnlyList<ImportIssue> Issues);
public sealed record ImportValidatedRow(int RowNumber,ImportRowStatus Status,string? FirstName,string? LastName,
    string? ClassReference,string? ExternalReference,string? EmailPreview,IReadOnlyList<ImportIssue> Issues);

public sealed class ImportInputException(string code,string message) : ArgumentException(message)
{public string Code {get;}=code;}

public interface IInstitutionImportFileReader
{
    Task<ImportTable> ReadAsync(Stream input,string fileName,ImportParsingOptions options,
        ImportParserLimits limits,CancellationToken ct=default);
}
public interface IInstitutionImportRules
{
    ImportMappingResult Map(IReadOnlyList<string> headers,ImportCategory category,IReadOnlyList<ImportColumnMapping>? explicitMapping=null);
    IReadOnlyList<ImportValidatedRow> Validate(ImportTable table,ImportCategory category,ImportMappingResult mapping,
        ImportEmailMode emailMode=ImportEmailMode.None,EmailPolicyVersion? policy=null,IReadOnlyDictionary<int,int>? emailCandidateOffsets=null);
}
