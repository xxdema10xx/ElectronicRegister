using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Services;

public interface IExamSubmissionProcessor
{
    Task<SubmissionResultDto> ProcessSubmitAsync(Guid submissionId, SubmitExamDto? dto, SubmitReason reason, ClaimsContext? caller);
}
