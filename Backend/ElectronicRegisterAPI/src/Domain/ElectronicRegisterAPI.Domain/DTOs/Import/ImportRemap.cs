using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Import;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ImportRemap(Guid EventId, long ExpectedRevision, ImportPreviewSettings Settings);
