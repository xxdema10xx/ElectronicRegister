using System.Text.Json;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Serialization;

// File parsing and pure rules are separate from tenant-authorized persistent workflows.
public sealed class ImportCatalogConverter<T> : JsonConverter<T> where T : struct, Enum
{
    public override T Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String || reader.GetString()is not { } value || !Enum.GetNames<T>().Contains(value, StringComparer.Ordinal))
            throw new JsonException("Catalogo importazione non valido.");
        return Enum.Parse<T>(value);
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        if (!Enum.IsDefined(value))
            throw new JsonException();
        writer.WriteStringValue(value.ToString());
    }
}
