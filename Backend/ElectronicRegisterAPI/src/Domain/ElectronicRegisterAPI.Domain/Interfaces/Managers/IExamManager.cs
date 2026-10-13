using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace ElectronicRegisterAPI.Domain.Interfaces.Managers
{
    public interface IExamManager
    {
        Task<int> CountAsync(ClaimsContext caller);
        Task<List<ExamDto>> GetAllAsync(ClaimsContext caller);
        Task<ExamDto?> GetByIdAsync(Guid id, ClaimsContext caller);
        Task<List<ExamDto>> GetByDateAsync(DateOnly date, ClaimsContext caller);
        Task<Guid?> AddAsync(CreateExamDto dto, ClaimsContext caller);
        Task<bool> UpdateAsync(Guid id, UpdateExamDto dto, ClaimsContext caller);
        Task<bool> DeleteAsync(Guid id);
    }
}
