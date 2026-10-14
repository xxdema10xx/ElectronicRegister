
using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Infrastructure.Mappers;

internal static class SubmitReasonMapper
{
    public static string ToDbString(SubmitReason reason) => reason switch
    {
        SubmitReason.Manual => "manual",
        SubmitReason.TimeExpired => "time_expired",
        SubmitReason.Violations => "violations",
        _ => throw new ArgumentOutOfRangeException(
            nameof(reason), $"Motivo consegna non valido: {reason}")
    };

    public static SubmitReason FromDbString(string reason) =>
        reason.ToLowerInvariant() switch
        {
            "manual" => SubmitReason.Manual,
            "time_expired" => SubmitReason.TimeExpired,
            "violations" => SubmitReason.Violations,
            _ => throw new ArgumentOutOfRangeException(
                nameof(reason), $"Motivo consegna non valido: {reason}")
        };
}
