using System;
using System.Collections.Generic;
using System.Text;
using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface IGradeRepository
{
    Task<Grade?> GetByIdAsync(Guid id);
    Task<List<Grade>> GetAllAsync();
    Task<int> CountAsync(Guid? teacherId = null);
    Task AddAsync(Grade grade);
    Task UpdateAsync(Grade grade);
    Task DeleteAsync(Guid id);
    Task<bool> ExistsForStudentAsync(Guid studentId);
    Task<bool> ExistsForSubjectAsync(Guid subjectId);
    Task<bool> ExistsForTeacherAsync(Guid teacherId);
}
