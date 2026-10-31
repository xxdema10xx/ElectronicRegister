namespace ElectronicRegisterAPI.Domain.Interfaces.Services;

// A live availability port. Implementations must evaluate current rollout state on every read.
public interface IFeatureAvailability
{
    bool Enabled { get; }
}
