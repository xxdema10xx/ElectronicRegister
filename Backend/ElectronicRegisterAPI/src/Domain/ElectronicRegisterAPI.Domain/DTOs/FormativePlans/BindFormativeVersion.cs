using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.FormativePlans;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BindFormativeVersion([property: JsonRequired] Guid EventId, [property: JsonRequired] Guid VersionId, string Reason);
