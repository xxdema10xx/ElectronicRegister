using System;
using System.Collections.Generic;
using System.Text;
using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories
{
    public interface ISubjectRepository
    {
        Task CreateAsync(CreateSubjectDto subjectDto);
        Task<List<SubjectDto>> ReadAllAsync();
        Task UpdateAsync(Guid id, UpdateSubjectDto dto);
        Task DeleteAsync(Guid id);
        Task<int> CountAsync();
        Task<SubjectDto> GetByIdAsync(Guid id);
        Task<SubjectDto> GetSubjectByNameAsync(string name);
        Task<List<SubjectDto>> GetSubjectByTeacherIdAsync(Guid id);
        
        
    }
}
