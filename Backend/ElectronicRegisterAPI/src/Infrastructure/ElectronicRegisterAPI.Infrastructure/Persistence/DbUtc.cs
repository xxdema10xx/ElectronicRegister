namespace ElectronicRegisterAPI.Infrastructure.Persistence;

// MySQL DATETIME contains UTC by application contract but has no timezone marker.
internal static class DbUtc
{
    internal static DateTime Read(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    internal static DateTime? Read(DateTime? value) => value.HasValue ? Read(value.Value) : null;
}
