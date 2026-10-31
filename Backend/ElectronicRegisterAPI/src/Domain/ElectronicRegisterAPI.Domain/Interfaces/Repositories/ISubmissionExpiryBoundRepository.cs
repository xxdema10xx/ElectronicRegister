using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface ISubmissionExpiryBoundRepository
{
    bool Matches(SubmissionExpiryWork work);
}
