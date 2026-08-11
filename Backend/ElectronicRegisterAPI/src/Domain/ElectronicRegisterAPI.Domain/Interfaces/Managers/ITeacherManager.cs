using System;
using System.Collections.Generic;
using System.Text;
using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Domain.Interfaces.Managers
{
    public interface ITeacherManager
    {
        Task<int> CountAsync();
        Task<List<TeacherDto>> GetAllAsync();
        Task<TeacherDto?> GetByIdAsync(Guid id);
        Task<Guid?> AddAsync(CreateTeacherDto dto);
        Task<bool> UpdateAsync(Guid id, UpdateTeacherDto dto);
        Task<bool> DeleteAsync(Guid id);
    }
}
