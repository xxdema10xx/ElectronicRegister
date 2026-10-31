using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.FormativePlans;

public sealed record FormativeReceipt(
    Guid EventId,
    Guid PlanId,
    Guid? VersionId,
    long Revision,
    Guid? CourseEditionId = null
);
