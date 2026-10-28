using ElectronicRegisterAPI.Domain.Exceptions;
using System.Globalization;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Policies;

// Conversion is deliberately opt-in and only for styled numeric cells in calendar fields.
public static class CalendarExcelValues
{
    public static string Date(string raw, bool date1904)
    {
        if (!decimal.TryParse(raw, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var serial) || serial < 0 || serial != decimal.Truncate(serial) || serial > (date1904 ? 2_957_003 : 2_958_465) || (!date1904 && serial < 1) || (!date1904 && serial == 60))
            throw new CalendarValidationException("calendar.invalid-excel-date");
        // Excel's fictitious 1900-02-29 is never mapped to a real day.
        try
        {
            var origin = date1904 ? new DateOnly(1904, 1, 1) : new DateOnly(1899, 12, 31);
            var days = checked((int)serial) - (!date1904 && serial > 60 ? 1 : 0);
            return origin.AddDays(days).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new CalendarValidationException("calendar.invalid-excel-date");
        }
    }

    public static string Time(string raw)
    {
        if (!decimal.TryParse(raw, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var serial) || serial < 0 || serial >= 1)
            throw new CalendarValidationException("calendar.invalid-excel-time");
        var minutes = serial * 1440;
        var rounded = decimal.Round(minutes, 0, MidpointRounding.ToEven);
        // Excel serials are IEEE754 approximations. Permit only sub-microsecond representation noise,
        // never rounding a genuine second or an arbitrary fractional minute into an appointment.
        if (decimal.Abs(minutes - rounded) > 0.00000001m || rounded >= 1440)
            throw new CalendarValidationException("calendar.excel-minute-precision-required");
        return new TimeOnly((int)rounded / 60, (int)rounded % 60).ToString("HH:mm", CultureInfo.InvariantCulture);
    }
}
