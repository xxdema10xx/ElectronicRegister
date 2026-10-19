using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface IInstitutionBoundRepository
{
    Guid InstitutionId { get; }
    bool Matches(ExamPrincipal principal);
}
public interface ILegacySubmissionBoundary { Task<bool> IsLegacySubmissionAsync(Guid id); }
public interface IInstitutionExamRepositories
{
    IExamRepository Exams { get; }
    IExamSubmissionRepository Submissions { get; }
    IGradeRepository Grades { get; }
    IStudentRepository Students { get; }
    ISubjectRepository Subjects { get; }
    IClassSubjectRepository ClassSubjects { get; }
    IAuditRepository Audit { get; }
    Task<List<ClassSubjectChoiceDto>> ClassSubjectChoicesAsync();
    Task<List<StudentDto>> StudentsForClassAsync(Guid classId);
    Task<SubmissionSessionDto> SessionAsync(Guid submissionId);
    Task<ViolationReceiptDto> ReportViolationAsync(Guid submissionId, InstitutionViolationDto dto);
    Task<List<ViolationEventDto>> ViolationsAsync(Guid submissionId);
}
public interface IInstitutionExamRepositoryFactory
{
    IInstitutionExamRepositories Create(ExamPrincipal principal);
}
