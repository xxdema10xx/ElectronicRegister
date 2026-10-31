using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Policies;

public static class InstitutionClassRules
{
    public static string Name(string? value)
    {
        var name = value?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 20)
            throw new ArgumentException("Il nome della classe deve contenere da 1 a 20 caratteri.");
        return name;
    }

    public static void Page(int page, int size)
    {
        if (page < 1 || size < 1 || size > 100 || (long)(page - 1) * size > int.MaxValue)
            throw new ArgumentException("Paginazione non valida.");
    }
}
