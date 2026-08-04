using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface IGradeRepository
{
    Task<Grade?> GetByIdAsync(Guid id);
    Task<List<Grade>> GetAllAsync();
    Task<int> CountAsync(Guid? teacherId = null);
    Task<List<Grade>> GetByDateAsync(DateOnly date, Guid? studentId = null, Guid? teacherId = null);
    Task<List<Grade>> GetBySubjectNameAsync(string subjectName, Guid? studentId = null, Guid? teacherId = null);
    Task<List<Grade>> GetByStudentIdAsync(Guid studentId);
    Task<List<Grade>> GetPagedAsync(int pageNumber, int pageSize, Guid? subjectId, Guid? studentId, DateOnly? date);
    Task AddAsync(Grade grade);
    Task UpdateAsync(Grade grade);
    Task DeleteAsync(Grade grade);
    Task<bool> ExistsForStudentAsync(Guid studentId);
    Task<bool> ExistsForSubjectAsync(Guid subjectId);
    Task<bool> ExistsForTeacherAsync(Guid teacherId);
}
