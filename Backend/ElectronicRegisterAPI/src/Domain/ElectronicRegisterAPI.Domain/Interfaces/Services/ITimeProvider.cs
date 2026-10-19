using System;

namespace ElectronicRegisterAPI.Domain.Interfaces.Services
{
    public interface ITimeProvider
    {
        DateTime UtcNow { get; }
    }
}
