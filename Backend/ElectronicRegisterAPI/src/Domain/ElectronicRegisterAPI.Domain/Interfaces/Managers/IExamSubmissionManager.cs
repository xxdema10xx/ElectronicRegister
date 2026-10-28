using System;
using System.Collections.Generic;
using System.Text;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Domain.Interfaces.Managers;

public interface IExamSubmissionManager
{
    Task<List<SubmissionSummaryDto>> GetByExamAsync(Guid examId, SubmissionStatus? status, ClaimsContext caller);
    Task<SubmissionReviewDto> GetByIdAsync(Guid id, ClaimsContext caller);
    Task ReviewAsync(Guid id, ReviewOpenAnswersDto dto, ClaimsContext caller);
    Task FinalizeAsync(Guid id, FinalizeSubmissionDto dto, ClaimsContext caller);
}
