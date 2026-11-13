using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Managers;

public interface IPlatformLifecycleManager
{
    Task<LifecyclePassResult> RunPassAsync(CancellationToken ct);
}
