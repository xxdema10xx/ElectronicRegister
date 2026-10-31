using ElectronicRegisterAPI.Domain.Interfaces.Services;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Infrastructure.Services;

public sealed class NoCalendarFault : ICalendarFaultObserver
{
    public Task CheckpointAsync(string name, CancellationToken ct) => Task.CompletedTask;
}
