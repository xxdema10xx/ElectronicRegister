using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.DTOs;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class TransferStudentClassDto
{
    [Required]
    public required Guid EventId { get; init; }

    [Required]
    public required Guid ToClassId { get; init; }

    [Required]
    public required DateOnly EffectiveDate { get; init; }

    [Required]
    public required string Reason { get; init; }

    [Required]
    public required long ExpectedRevision { get; init; }
}
