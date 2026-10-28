using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Import;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ImportConfirmation(
    Guid EventId,
    long ExpectedRevision,
    string PreviewDigest,
    bool AcknowledgeWarnings = false,
    bool AllowRepeatedFile = false,
    string? Reason = null
);
