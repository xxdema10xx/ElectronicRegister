using System;
using System.Collections.Generic;
using System.Text;
using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface IGradeRepository
{
    Task<GradeDto?> GetByIdAsync(Guid id);
    Task<List<GradeDto>> GetAllAsync();
    Task<int> CountAsync(Guid? teacherId = null);
    Task AddAsync(GradeDto gradeDto);
    Task UpdateAsync(GradeDto gradeDto);
    Task DeleteAsync(Guid id);
    Task<bool> ExistsForStudentAsync(Guid studentId);
    Task<bool> ExistsForSubjectAsync(Guid subjectId);
    Task<bool> ExistsForTeacherAsync(Guid teacherId);
}
