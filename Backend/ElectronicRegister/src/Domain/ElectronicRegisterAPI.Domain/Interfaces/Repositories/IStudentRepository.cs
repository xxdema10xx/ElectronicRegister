using System;
using System.Collections.Generic;
using System.Text;
using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories
{
    public interface IStudentRepository
    {
        Task CreateAsync(CreateStudentDto studentDto);
        Task<List<StudentDto>> ReadAllAsync();
        Task UpdateAsync (UpdateStudentDto studentDto);
        Task DeleteAsync(Guid id);
        Task<int> CountAsync();
        Task<StudentDto> GetByIdAsync(Guid id);
        Task<List<StudentDto>> GetStudentsByNameAsync(string lastName);
    }
}
