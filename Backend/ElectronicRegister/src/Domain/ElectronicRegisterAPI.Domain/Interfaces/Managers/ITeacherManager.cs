using System;
using System.Collections.Generic;
using System.Text;
using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Domain.Interfaces.Managers
{
    public interface ITeacherManager
    {
        Task<int> CountAsync(ClaimsContext claimsContext);
        Task<List<TeacherDto>> GetAllAsync(ClaimsContext claimsContext);
        Task<TeacherDto> GetByIdAsync(Guid id, ClaimsContext claimsContext);
        Task UpdateAsync(Guid id, UpdateTeacherDto dto, ClaimsContext claimsContext);
        Task AddAsync(CreateTeacherDto dto, ClaimsContext claimsContext);
        Task DeleteAsync(Guid id, ClaimsContext claimsContext);
    }
}
