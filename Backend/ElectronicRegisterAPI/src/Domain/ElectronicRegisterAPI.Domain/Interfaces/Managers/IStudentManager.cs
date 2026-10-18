using System;
using System.Collections.Generic;
using System.Text;
using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Domain.Interfaces.Managers
{
    public interface IStudentManager
    {
        Task<int> CountAsync(ClaimsContext caller);
        Task<List<StudentDto>> GetAllAsync(ClaimsContext caller);
        Task<StudentDto?> GetByIdAsync(Guid id, ClaimsContext caller);
        Task<List<StudentDto>> GetStudentsByLastNameAsync(string lastName, ClaimsContext caller);
        Task<bool> UpdateAsync(Guid id, UpdateStudentDto dto);
        Task<Guid> AddAsync(CreateStudentDto dto);
        Task<bool> DeleteAsync(Guid id);
    }
}
