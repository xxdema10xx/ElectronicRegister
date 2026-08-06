using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface IGradeRepository
{
    Task<Grade?> GetByIdAsync(Guid id);
    Task<List<Grade>> GetAllAsync(Guid? teacherId, Guid? studentId);
    Task<int> CountAsync(Guid? teacherId = null, Guid? studentId = null);
    Task<List<Grade>> GetByDateAsync(DateOnly date, Guid? studentId = null, Guid? teacherId = null);
    Task<List<Grade>> GetBySubjectNameAsync(string subjectName, Guid? studentId = null, Guid? teacherId = null);
    Task<List<Grade>> GetByStudentIdAsync(Guid studentId);
    Task<(List<Grade> Items, int TotalCount)> GetPagedAsync(
        int pageNumber, int pageSize,
        Guid? subjectId, Guid? studentId, DateOnly? date,
        Guid? restrictToStudentId, Guid? restrictToTeacherId);
    Task<GradeStatistics> GetStatisticsAsync(Guid? teacherId, Guid? studentId);
    Task AddAsync(Grade grade);
    Task UpdateAsync(Grade grade);
    Task DeleteAsync(Grade grade);
    Task<bool> ExistsForStudentAsync(Guid studentId);
    Task<bool> ExistsForSubjectAsync(Guid subjectId);
    Task<bool> ExistsForTeacherAsync(Guid teacherId);
}
