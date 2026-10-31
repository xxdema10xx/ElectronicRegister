using System.Text;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Policies;

public static class InstitutionAcademicRules
{
    public static string Name(string? value)
    {
        var s = value?.Trim();
        if (string.IsNullOrEmpty(s) || s.Length > 100)
            throw new ArgumentException("Nome richiesto, massimo 100 caratteri.");
        return s;
    }

    public static string? Description(string? value)
    {
        if (value is not null && Encoding.UTF8.GetByteCount(value) > 65535)
            throw new ArgumentException("Descrizione troppo lunga.");
        return value;
    }

    public static string RequiredDescription(string? value) => Description(value) ?? throw new ArgumentException("Descrizione richiesta.");
    public static Guid Id(Guid value) => value == Guid.Empty ? throw new ArgumentException("Identificativo non valido.") : value;
    public static void Dates(DateOnly start, DateOnly end)
    {
        if (start.Year < 1000 || end.Year < 1000 || end < start)
            throw new ArgumentException("Intervallo date non valido.");
    }
}
