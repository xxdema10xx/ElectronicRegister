using System.Globalization;

namespace ElectronicRegisterAPI.Domain.Policies;

// E2: names of a linked profile match ignoring letter case, diacritics and outer whitespace.
public static class PersonNameComparison
{
    public static bool SameName(string? left, string? right) =>
        string.Compare((left ?? "").Trim(), (right ?? "").Trim(), CultureInfo.InvariantCulture, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) == 0;
}
