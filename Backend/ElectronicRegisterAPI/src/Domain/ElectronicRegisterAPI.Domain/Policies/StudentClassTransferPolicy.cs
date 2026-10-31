using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Policies;

public sealed class StudentClassTransferPolicy
{
    public static string Reason(string? reason)
    {
        var value = reason?.Trim();
        if (string.IsNullOrWhiteSpace(value) || value.Length > 1000)
            throw new ArgumentException("Motivazione obbligatoria, massimo 1000 caratteri.");
        return value;
    }

    public static DateOnly OperationalDate(TimeProvider clock, string zone) => OperationalDate(clock.GetUtcNow(), zone);
    public static DateOnly OperationalDate(DateTimeOffset instant, string zone) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, TimeZoneInfo.FindSystemTimeZoneById(zone)).DateTime);
    public static DateOnly Deadline(DateOnly? start, int months)
    {
        if (start is null)
            throw new BusinessRuleException("La data ufficiale di inizio corso non è configurata.");
        try
        {
            return start.Value.AddMonths(months);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new BusinessRuleException("Intervallo del corso non compatibile con il trasferimento.");
        }
    }

    public static void EnsureAllowed(DateOnly? start, DateOnly today, int months)
    {
        if (today > Deadline(start, months))
            throw new BusinessRuleException("La finestra amministrativa per il trasferimento è scaduta.");
    }
}
