using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;

namespace ElectronicRegisterAPI.Application.Managers
{
    internal class ExamManager : IExamManager
    {
        private readonly IExamRepository _examRepository;
        private readonly IExamService _examService;

        public ExamManager(IExamRepository examRepository, IExamService examService)
        {
            _examRepository = examRepository;
            _examService = examService;
        }


        public async Task<List<ExamSummaryDto>> GetAllAsync(ClaimsContext caller)
        {
            if (caller.Role != UserRole.Admin && caller.Role != UserRole.Teacher)
                throw new UnauthorizedAccessException("Non sei autorizzato a visualizzare gli esami.");

            Guid? teacherId = null;

            if (caller.Role == UserRole.Teacher)
            {
                teacherId = caller.TeacherId
                    ?? throw new UnauthorizedAccessException("Identificativo docente non presente.");
            }

            return await _examRepository.GetSummariesAsync(teacherId);
        }


        public async Task<ExamDetailDto?> GetByIdAsync(Guid id, ClaimsContext caller)
        {
            throw new NotImplementedException();
        }

        public async Task<List<AssignedExamDto>> GetAssignedAsync(ClaimsContext caller)
        {
            throw new NotImplementedException();
        }

        public async Task<Guid> AddAsync(CreateExamDto dto, ClaimsContext caller)
        {
            throw new NotImplementedException();
        }

        public async Task UpdateAsync(Guid id, UpdateExamDto dto, ClaimsContext caller)
        {
            throw new NotImplementedException();
        }

        public async Task UpdateStudentsAsync(Guid id, UpdateExamStudentsDto dto, ClaimsContext caller)
        {
            throw new NotImplementedException();
        }

        public async Task PublishAsync(Guid id, ClaimsContext caller)
        {
            throw new NotImplementedException();
        }

        public async Task CloseAsync(Guid id, ClaimsContext caller)
        {
            throw new NotImplementedException();
        }

        public async Task CancelAsync(Guid id, ClaimsContext caller)
        {
            throw new NotImplementedException();
        }

        public async Task DeleteAsync(Guid id, ClaimsContext caller)
        {
            throw new NotImplementedException();
        }

        public async Task<StartExamDto> StartAsync(Guid id, ClaimsContext caller)
        {
            throw new NotImplementedException();
        }

        public async Task<SaveAnswersResponseDto> SaveAnswersAsync(Guid id, SaveExamAnswersDto dto, ClaimsContext caller)
        {
            throw new NotImplementedException();
        }

        public async Task<SubmissionResultDto> SubmitAsync(Guid id, SubmitExamDto dto, ClaimsContext caller)
        {
            throw new NotImplementedException();
        }

        public async Task<SubmissionResultDto> GetResultAsync(Guid id, ClaimsContext caller)
        {
            throw new NotImplementedException();
        }
    }
}
