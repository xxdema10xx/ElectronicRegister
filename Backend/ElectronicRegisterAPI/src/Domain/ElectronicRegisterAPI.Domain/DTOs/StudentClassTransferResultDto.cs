using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.DTOs;

public sealed record StudentClassTransferResultDto(
    Guid EventId,
    Guid StudentId,
    Guid FromClassId,
    Guid ToClassId,
    Guid FromCourseEditionId,
    Guid ToCourseEditionId,
    DateOnly EffectiveDate,
    int AppliedLimitMonths,
    long Revision,
    DateTime OccurredAtUtc
);
