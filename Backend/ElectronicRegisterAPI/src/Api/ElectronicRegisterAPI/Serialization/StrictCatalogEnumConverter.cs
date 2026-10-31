using ElectronicRegisterAPI.Domain.Results;
using System.Text.Json;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Api.Contracts;

namespace ElectronicRegisterAPI.Api.Serialization;

// Local to this contract: no numeric, case-insensitive or composite enum aliases.
public sealed class StrictCatalogEnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
    public override T Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String || reader.GetString()is not { } text || !Enum.GetNames<T>().Contains(text, StringComparer.Ordinal))
            throw new JsonException("Valore del catalogo non valido.");
        return Enum.Parse<T>(text);
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());
}
