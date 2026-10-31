using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.DTOs;

public sealed record EnrollmentMutationResult(
    Guid EventId,
    long Revision,
    string Status,
    DateOnly? CompletionDate
);
