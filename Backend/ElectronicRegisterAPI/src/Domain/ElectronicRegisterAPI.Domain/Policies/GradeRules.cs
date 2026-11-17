namespace ElectronicRegisterAPI.Domain.Policies;

/// <summary>Rules for the official grade scale (whole numbers, 0-30, pass at 18), independent of exam score percentages.</summary>
public static class GradeRules
{
    public const decimal Minimum = 0m;
    public const decimal Maximum = 30m;
    public const decimal Passing = 18m;
    /// <summary>Highest whole grade that is still insufficient.</summary>
    public const decimal HighestFailing = Passing - 1m;
    public static bool IsPassing(decimal value) => value >= Passing;
    public static void EnsureValidValue(decimal value)
    {
        if (value < Minimum || value > Maximum)
            throw new ArgumentOutOfRangeException(nameof(value), $"Il valore del voto deve essere compreso tra {Minimum:0} e {Maximum:0}.");
        if (value != decimal.Truncate(value))
            throw new ArgumentOutOfRangeException(nameof(value), "Il voto deve essere un numero intero.");
    }
}
