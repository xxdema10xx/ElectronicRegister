using System;
using System.Collections.Generic;
using System.Text;
using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Domain.Interfaces.Managers
{
    public interface IStudentManager
    {
        Task<int> CountAsync();
        Task<List<StudentDto>> GetAllAsync();
        Task<StudentDto?> GetByIdAsync(Guid id);
        Task<List<StudentDto>> GetStudentsByLastNameAsync(string lastName);
        Task<bool> UpdateAsync(Guid id, UpdateStudentDto dto);
        Task<bool> AddAsync(CreateStudentDto dto);
        Task<bool> DeleteAsync(Guid id);
    }
}
