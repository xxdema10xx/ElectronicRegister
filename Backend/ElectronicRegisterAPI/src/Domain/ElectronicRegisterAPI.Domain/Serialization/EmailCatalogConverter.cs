using System.Text.Json;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Serialization;

public sealed class EmailCatalogConverter<T> : JsonConverter<T> where T : struct, Enum
{
    public override T Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String || reader.GetString()is not { } value || !Enum.GetNames<T>().Contains(value, StringComparer.Ordinal))
            throw new JsonException("Valore catalogo email non valido.");
        return Enum.Parse<T>(value);
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());
}
