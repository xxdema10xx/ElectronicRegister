using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Services;

public interface IExamSubmissionProcessor
{
    Task<SubmissionResultDto> ProcessSubmitAsync(
        Guid submissionId,
        SubmitExamDto? dto,
        SubmitReason reason,
        ClaimsContext? caller
    );
    Task<SubmissionResultDto> ProcessInstitutionAsync(
        Guid submissionId,
        SubmitExamDto? dto,
        SubmitReason reason,
        ExamPrincipal principal
    ) => throw new UnauthorizedAccessException("Processor istituzionale non configurato.");
}
