using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Calendar;

public sealed record CalendarPeriod(
    Guid Id,
    Guid ClassId,
    DateOnly Month,
    string Status,
    long Revision,
    Guid? PublishedByUserId,
    Guid? PublishedByMembershipId,
    DateTime? PublishedAtUtc,
    CalendarApproval Approval
);
