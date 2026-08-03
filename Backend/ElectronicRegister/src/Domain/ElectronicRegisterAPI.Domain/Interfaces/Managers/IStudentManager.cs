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
        Task<StudentDto> GetByIdAsync(Guid id, ClaimsContext caller);
        Task<List<StudentDto>> GetStudentsByLastnameAsync(string lastName, ClaimsContext caller);
        Task UpdateAsync(Guid id, UpdateStudentDto dto, ClaimsContext caller);
        Task AddAsync(CreateStudentDto dto, ClaimsContext caller);
        Task DeleteAsync(Guid id, ClaimsContext caller);
    }
}
