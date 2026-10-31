using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Policies;

public static class InstitutionCampusRules
{
    public static string Name(string? value)
    {
        var s = value?.Trim();
        if (string.IsNullOrEmpty(s) || s.Length > 200)
            throw new ArgumentException("Nome sede richiesto, massimo 200 caratteri.");
        return s;
    }

    public static string? Optional(string? value, int maximum)
    {
        var s = value?.Trim();
        if (s?.Length > maximum)
            throw new ArgumentException("Campo sede troppo lungo.");
        return string.IsNullOrEmpty(s) ? null : s;
    }
}
