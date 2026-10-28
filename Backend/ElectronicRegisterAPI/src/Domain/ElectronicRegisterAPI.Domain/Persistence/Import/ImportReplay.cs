using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Persistence.Import;

public sealed record ImportReplay(string RequestHash, string ResultJson);
