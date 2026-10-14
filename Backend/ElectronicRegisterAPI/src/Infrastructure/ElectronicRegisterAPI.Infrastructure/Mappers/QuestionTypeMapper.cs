
using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Infrastructure.Mappers;

internal static class QuestionTypeMapper
{
    public static string ToDbString(QuestionType type) => type switch
    {
        QuestionType.Single => "single",
        QuestionType.Multiple => "multiple",
        QuestionType.Open => "open",
        _ => throw new ArgumentOutOfRangeException(
            nameof(type), $"Tipo domanda non valido: {type}")
    };

    public static QuestionType FromDbString(string type) =>
        type.ToLowerInvariant() switch
        {
            "single" => QuestionType.Single,
            "multiple" => QuestionType.Multiple,
            "open" => QuestionType.Open,
            _ => throw new ArgumentOutOfRangeException(
                nameof(type), $"Tipo domanda non valido: {type}")
        };
}
