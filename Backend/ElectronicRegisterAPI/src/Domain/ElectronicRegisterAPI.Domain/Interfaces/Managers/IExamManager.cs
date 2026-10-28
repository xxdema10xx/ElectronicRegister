using System;
using System.Collections.Generic;
using System.Text;
using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Domain.Interfaces.Managers;

public interface IExamManager
{
    Task<List<ExamSummaryDto>> GetAllAsync(ClaimsContext caller);
    Task<ExamDetailDto?> GetByIdAsync(Guid id, ClaimsContext caller);
    Task<List<AssignedExamDto>> GetAssignedAsync(ClaimsContext caller);
    Task<Guid> AddAsync(CreateExamDto dto, ClaimsContext caller);
    Task UpdateAsync(Guid id, UpdateExamDto dto, ClaimsContext caller);
    Task UpdateStudentsAsync(Guid id, UpdateExamStudentsDto dto, ClaimsContext caller);
    Task PublishAsync(Guid id, ClaimsContext caller);
    Task CloseAsync(Guid id, ClaimsContext caller);
    Task CancelAsync(Guid id, ClaimsContext caller);
    Task DeleteAsync(Guid id, ClaimsContext caller);
    Task<StartExamDto> StartAsync(Guid id, ClaimsContext caller);
    Task<SaveAnswersResponseDto> SaveAnswersAsync(Guid id, SaveExamAnswersDto dto, ClaimsContext caller);
    Task<SubmissionResultDto> SubmitAsync(Guid id, SubmitExamDto dto, ClaimsContext caller);
    Task<SubmissionResultDto> GetResultAsync(Guid id, ClaimsContext caller);
}
