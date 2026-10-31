using ElectronicRegisterAPI.Domain.DTOs.Import;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Persistence.Import;

public sealed record ImportStoredBatch(ImportBatch Batch, ImportPayload? Payload, ImportResult? Result = null);
