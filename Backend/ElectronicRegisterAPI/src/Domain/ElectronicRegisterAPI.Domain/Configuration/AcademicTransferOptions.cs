using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Configuration;

public sealed class AcademicTransferOptions
{
    public const string Section = "AcademicTransfers";
    public int MaxMonthsFromCourseStart { get; set; } = 3;
    public string TimeZoneId { get; set; } = "Europe/Rome";

    public static bool Valid(AcademicTransferOptions o)
    {
        if (o.MaxMonthsFromCourseStart is < 1 or > 120)
            return false;
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(o.TimeZoneId);
            return true;
        }
        catch (Exception e)when (e is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        {
            return false;
        }
    }
}
