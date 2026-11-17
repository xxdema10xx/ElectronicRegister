namespace ElectronicRegisterAPI.Domain.Policies;

/// <summary>Rules for the official grade scale (whole numbers, 0-30, pass at 18), independent of exam score percentages.</summary>
public static class GradeRules
{
    public const decimal Minimum = 0m;
    public const decimal Maximum = 30m;
    public const decimal Passing = 18m;
    /// <summary>Highest whole grade that is still insufficient.</summary>
    public const decimal HighestFailing = Passing - 1m;
    public const int DescriptionMaxLength = 200;
    public const int MaxBatchSize = 100;
    public static string InvalidValueMessage => $"Il voto deve essere un numero intero compreso tra {Minimum:0} e {Maximum:0}.";
    public static bool IsPassing(decimal value) => value >= Passing;
    public static bool IsValidValue(decimal value) => value >= Minimum && value <= Maximum && value == decimal.Truncate(value);
    public static void EnsureValidValue(decimal value)
    {
        if (value < Minimum || value > Maximum)
            throw new ArgumentOutOfRangeException(nameof(value), $"Il valore del voto deve essere compreso tra {Minimum:0} e {Maximum:0}.");
        if (value != decimal.Truncate(value))
            throw new ArgumentOutOfRangeException(nameof(value), "Il voto deve essere un numero intero.");
    }

    /// <summary>Trims an optional grade description; null or blank means no description.</summary>
    public static string? NormalizeDescription(string? description)
    {
        if (description is null)
            return null;
        var trimmed = description.Trim();
        if (trimmed.Length == 0)
            return null;
        if (trimmed.Length > DescriptionMaxLength)
            throw new ArgumentException($"La descrizione del voto non può superare {DescriptionMaxLength} caratteri.", nameof(description));
        if (trimmed.Any(char.IsControl))
            throw new ArgumentException("La descrizione del voto non può contenere caratteri di controllo.", nameof(description));
        return trimmed;
    }
}
