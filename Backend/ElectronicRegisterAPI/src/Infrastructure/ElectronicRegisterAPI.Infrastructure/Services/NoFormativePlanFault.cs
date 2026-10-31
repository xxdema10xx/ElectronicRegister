using ElectronicRegisterAPI.Domain.Interfaces.Services;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Infrastructure.Services;

public sealed class NoFormativePlanFault : IFormativePlanFaultObserver
{
    public Task CheckpointAsync(string checkpoint, CancellationToken ct) => Task.CompletedTask;
}
