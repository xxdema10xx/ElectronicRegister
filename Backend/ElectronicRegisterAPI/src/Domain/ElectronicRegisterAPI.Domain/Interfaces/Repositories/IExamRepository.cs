using System;
using System.Collections.Generic;
using System.Text;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories
{
    public interface IExamRepository
    {
        Task<Exam?> GetByIdAsync(Guid id, bool includeStructure = false, bool includeStudents = false);
        Task<Exam?> GetByIdForUpdateAsync(Guid id);
        Task<bool> HasRetakesAsync(Guid examId);
        Task<List<Exam>> GetAllAsync(Guid? teacherId = null);
        Task<List<ExamSummaryDto>> GetSummariesAsync(Guid? teacherId = null);
        Task<List<Exam>> GetAssignedToStudentAsync(Guid studentId);
        Task<List<Exam>> GetByClassSubjectAsync(Guid classSubjectId);
        Task<bool> HasSubmissionsAsync(Guid examId);
        Task<bool> HasInProgressSubmissionsAsync(Guid examId);
        Task AddAsync(Exam exam);
        Task UpdateAsync(Exam exam);
        Task ReplaceStructureAsync(Exam exam);
        Task ReplaceAssignedStudentsAsync(Guid examId, IReadOnlyCollection<ExamStudent> students);
        Task DeleteDraftAsync(Guid examId);
    }
}
