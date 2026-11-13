namespace ElectronicRegisterAPI.Domain.Policies;

// Italian personal fiscal code (16 characters), including omocodia substitutions and the
// official odd/even check-character tables. Temporary 11-digit codes are not accepted.
public static class FiscalCodeRules
{
    private const string Omocodia = "LMNPQRSTUV";
    private const string Months = "ABCDEHLMPRST";
    private static readonly int[] OddDigits = [1, 0, 5, 7, 9, 13, 15, 17, 19, 21];
    private static readonly int[] OddLetters = [1, 0, 5, 7, 9, 13, 15, 17, 19, 21, 2, 4, 18, 20, 11, 3, 6, 8, 12, 14, 16, 10, 22, 25, 24, 23];
    private static readonly int[] NumericPositions = [6, 7, 9, 10, 12, 13, 14];

    public static string Normalize(string? value) => (value ?? "").Trim().ToUpperInvariant();

    public static bool IsValid(string? normalized)
    {
        if (normalized is not { Length: 16 })
            return false;
        for (var i = 0; i < 16; i++)
        {
            var c = normalized[i];
            var valid = NumericPositions.Contains(i) ? char.IsAsciiDigit(c) || Omocodia.Contains(c) : char.IsAsciiLetterUpper(c);
            if (!valid)
                return false;
        }
        if (!Months.Contains(normalized[8]))
            return false;
        var day = Digit(normalized[9]) * 10 + Digit(normalized[10]);
        if (day is not (>= 1 and <= 31 or >= 41 and <= 71))
            return false;
        return Check(normalized.AsSpan(0, 15)) == normalized[15];
    }

    public static char Check(ReadOnlySpan<char> first15)
    {
        if (first15.Length != 15)
            throw new ArgumentException("Servono 15 caratteri.");
        var sum = 0;
        for (var i = 0; i < 15; i++)
        {
            var c = first15[i];
            if (!char.IsAsciiDigit(c) && !char.IsAsciiLetterUpper(c))
                throw new ArgumentException("Carattere non ammesso.");
            var odd = i % 2 == 0;
            if (char.IsAsciiDigit(c))
                sum += odd ? OddDigits[c - '0'] : c - '0';
            else
                sum += odd ? OddLetters[c - 'A'] : c - 'A';
        }
        return (char)('A' + sum % 26);
    }

    private static int Digit(char c) => char.IsAsciiDigit(c) ? c - '0' : Omocodia.IndexOf(c);
}
