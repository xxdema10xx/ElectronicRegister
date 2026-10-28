using System.Text.Json;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Services;

public interface IOnboardingFaultObserver
{
    Task BeforeCommitAsync(string action, CancellationToken ct);
}
