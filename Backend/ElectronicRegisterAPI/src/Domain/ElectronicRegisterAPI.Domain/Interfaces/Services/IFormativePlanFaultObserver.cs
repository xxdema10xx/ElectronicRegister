using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Services;

public interface IFormativePlanFaultObserver
{
    Task CheckpointAsync(string checkpoint, CancellationToken ct);
}
