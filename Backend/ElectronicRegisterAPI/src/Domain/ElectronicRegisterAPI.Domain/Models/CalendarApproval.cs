using System.Globalization;

namespace ElectronicRegisterAPI.Domain.Models;

public sealed record CalendarApproval(
    DateOnly DeadlineLocalDate,
    bool IsPublished,
    bool IsLate,
    bool IsOverdue
);
