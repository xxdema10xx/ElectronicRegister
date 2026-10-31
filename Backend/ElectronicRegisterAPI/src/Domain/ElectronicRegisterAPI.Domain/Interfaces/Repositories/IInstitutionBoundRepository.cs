using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface IInstitutionBoundRepository
{
    Guid InstitutionId { get; }

    bool Matches(ExamPrincipal principal);
}
