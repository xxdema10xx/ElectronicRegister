using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface ILegacySubmissionBoundary
{
    Task<bool> IsLegacySubmissionAsync(Guid id);
}
