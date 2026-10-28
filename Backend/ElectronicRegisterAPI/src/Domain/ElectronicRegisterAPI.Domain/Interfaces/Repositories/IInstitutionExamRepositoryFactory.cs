using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface IInstitutionExamRepositoryFactory
{
    IInstitutionExamRepositories Create(ExamPrincipal principal);
}
