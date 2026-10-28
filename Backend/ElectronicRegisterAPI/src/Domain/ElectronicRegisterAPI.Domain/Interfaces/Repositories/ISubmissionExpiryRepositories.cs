using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface ISubmissionExpiryRepositories
{
    IExamSubmissionRepository Submissions { get; }

    IExamRepository Exams { get; }

    IGradeRepository Grades { get; }

    IAuditRepository Audit { get; }
}
