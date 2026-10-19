using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Infrastructure.Mappers;

internal static class ExamStatusMapper
{
    public static string ToDbString(ExamStatus status) => status switch
    {
        ExamStatus.Draft => "draft",
        ExamStatus.Published => "published",
        ExamStatus.Closed => "closed",
        ExamStatus.Cancelled => "cancelled",
        _ => throw new ArgumentOutOfRangeException(
            nameof(status), $"Stato esame non valido: {status}")
    };

    public static ExamStatus FromDbString(string status) =>
        status.ToLowerInvariant() switch
        {
            "draft" => ExamStatus.Draft,
            "published" => ExamStatus.Published,
            "closed" => ExamStatus.Closed,
            "cancelled" => ExamStatus.Cancelled,
            _ => throw new ArgumentOutOfRangeException(
                nameof(status), $"Stato esame non valido: {status}")
        };
}