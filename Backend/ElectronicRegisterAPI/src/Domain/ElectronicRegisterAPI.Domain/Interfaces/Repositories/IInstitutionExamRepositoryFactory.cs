using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface IInstitutionExamRepositoryFactory
{
    IInstitutionExamRepositories Create(ExamPrincipal principal);
    IInstitutionExamRepositories Create(ExamPrincipal principal, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Create(principal);
    }
}
