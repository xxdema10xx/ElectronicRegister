using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Persistence.Import;

public sealed record ImportClassMatch(Guid Id, string Fingerprint);
