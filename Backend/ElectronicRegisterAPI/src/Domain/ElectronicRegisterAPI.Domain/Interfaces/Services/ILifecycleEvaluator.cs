using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Services;

public interface ILifecycleEvaluator
{
    LifecycleDecision Evaluate(LifecycleSnapshot snapshot);
    DateTime StudentStartedBeforeUtc();
}
