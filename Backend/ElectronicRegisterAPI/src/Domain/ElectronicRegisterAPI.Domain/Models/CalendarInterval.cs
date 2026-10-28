using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Policies;
using System.Globalization;

namespace ElectronicRegisterAPI.Domain.Models;

public sealed record CalendarInterval
{
    public DateOnly LocalDate { get; }
    public TimeOnly StartsAt { get; }
    public TimeOnly EndsAt { get; }
    public string TimeZoneId { get; }
    public DateTime StartUtc { get; }
    public DateTime EndUtc { get; }
    public int DurationMinutes { get; }

    private CalendarInterval(
        DateOnly date,
        TimeOnly start,
        TimeOnly end,
        string zone,
        DateTime startUtc,
        DateTime endUtc
    )
    {
        LocalDate = date;
        StartsAt = start;
        EndsAt = end;
        TimeZoneId = zone;
        StartUtc = startUtc;
        EndUtc = endUtc;
        DurationMinutes = checked((int)((endUtc.Ticks - startUtc.Ticks) / TimeSpan.TicksPerMinute));
    }

    public static CalendarInterval Create(
        DateOnly date,
        TimeOnly start,
        TimeOnly end,
        string? timeZoneId
    )
    {
        if (start.Ticks % TimeSpan.TicksPerMinute != 0 || end.Ticks % TimeSpan.TicksPerMinute != 0)
            throw new CalendarValidationException("calendar.minute-precision-required");
        if (end <= start)
            throw new CalendarValidationException("calendar.same-day-positive-interval-required");
        var zone = InstitutionCalendarRules.TimeZone(timeZoneId);
        var localStart = date.ToDateTime(start, DateTimeKind.Unspecified);
        var localEnd = date.ToDateTime(end, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(localStart) || zone.IsInvalidTime(localEnd))
            throw new CalendarValidationException("calendar.nonexistent-local-time");
        if (zone.IsAmbiguousTime(localStart) || zone.IsAmbiguousTime(localEnd))
            throw new CalendarValidationException("calendar.ambiguous-local-time");
        // Reject intervals crossing an ambiguous or nonexistent local minute as well.
        // This version deliberately has no offset-selection workflow.
        for (var minute = localStart; minute < localEnd; minute = minute.AddMinutes(1))
        {
            if (zone.IsInvalidTime(minute))
                throw new CalendarValidationException("calendar.nonexistent-local-time");
            if (zone.IsAmbiguousTime(minute))
                throw new CalendarValidationException("calendar.ambiguous-local-time");
        }

        var utcStart = TimeZoneInfo.ConvertTimeToUtc(localStart, zone);
        var utcEnd = TimeZoneInfo.ConvertTimeToUtc(localEnd, zone);
        if (utcEnd <= utcStart || (utcEnd.Ticks - utcStart.Ticks) % TimeSpan.TicksPerMinute != 0)
            throw new CalendarValidationException("calendar.invalid-utc-duration");
        return new(date, start, end, zone.Id, utcStart, utcEnd);
    }

    public bool Overlaps(CalendarInterval other) => StartUtc < other.EndUtc && other.StartUtc < EndUtc;
}
