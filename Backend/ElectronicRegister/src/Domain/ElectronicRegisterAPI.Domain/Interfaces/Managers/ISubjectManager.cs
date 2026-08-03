using System;
using System.Collections.Generic;
using System.Text;
using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Domain.Interfaces.Managers
{
    public interface ISubjectManager
    {
        Task<int> CountAsync(ClaimsContext caller);
        Task<List<SubjectDto>> GetAllAsync(ClaimsContext caller);
        Task<SubjectDto> GetByIdAsync(Guid id, ClaimsContext caller);
        Task<SubjectDto> GetSubjectByNameAsync(string name, ClaimsContext caller);
        Task<List<SubjectDto>> GetSubjectsByTeacherIdAsync(Guid teacherId, ClaimsContext caller);
        Task UpdateAsync(Guid id, UpdateSubjectDto dto, ClaimsContext caller);
        Task AddAsync(CreateSubjectDto dto, ClaimsContext caller);
        Task DeleteAsync(Guid id, ClaimsContext caller);
    }
}
