using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Services;

public interface ICalendarFaultObserver
{
    Task CheckpointAsync(string name, CancellationToken ct);
}
