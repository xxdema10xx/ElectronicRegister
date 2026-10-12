using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace ElectronicRegisterAPI.Domain.Interfaces.Managers
{
    public interface IExamManager
    {
        Task<int> CountAsync();
        Task<List<ExamDto>> GetAllAsync();
        Task<ExamDto?> GetByIdAsync(Guid id);
        Task<List<ExamDto>> GetByIdsAsync(IEnumerable<Guid> ids);
        Task<List<ExamDto>> GetByDateAsync(DateOnly date);
        Task<bool> AddAsync(CreateExamDto dto);
        Task<bool> UpdateAsync(Guid id, UpdateExamDto dto);
        Task<bool> DeleteAsync(Guid id);
    }
}
