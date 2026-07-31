using System;
using System.Collections.Generic;
using System.Text;
using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories
{
    public interface ITeacherRepository
    {
        Task CreateAsync(CreateTeacherDto teacherDto);
        Task<List<TeacherDto>> ReadAllAsync();
        Task UpdateAsync(Guid id, UpdateTeacherDto dto);
        Task DeleteAsync(Guid id);
        Task<int> CountAsync();
        Task<TeacherDto> GetByIdAsync(Guid id);
    }
}
