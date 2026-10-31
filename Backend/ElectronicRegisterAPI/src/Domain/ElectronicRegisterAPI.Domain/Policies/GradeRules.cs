namespace ElectronicRegisterAPI.Domain.Policies;

/// <summary>Rules for the official grade scale, independent of exam score percentages.</summary>
public static class GradeRules
{
    public const decimal Minimum = 1m;
    public const decimal Maximum = 10m;
    public const decimal Passing = 6m;
    public static bool IsPassing(decimal value) => value >= Passing;
    public static void EnsureValidValue(decimal value)
    {
        if (value < Minimum || value > Maximum)
            throw new ArgumentOutOfRangeException(nameof(value), $"Il valore del voto deve essere compreso tra {Minimum} e {Maximum}.");
    }
}
