using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Import;

public sealed record ImportPreparedRow(
    ImportValidatedRow Data,
    Guid? ClassId,
    string? ClassFingerprint,
    IReadOnlyList<Guid>? ClassCandidates = null
);
