using System;
using System.Collections.Generic;
using System.Text;
using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Domain.Interfaces.Managers
{
    public interface IGradeManager
    {
        Task<List<GradeDto>> GetAllAsync(ClaimsContext caller);
        Task<GradeDto?> GetByIdAsync(Guid id, ClaimsContext caller);
        Task<GradePageDto> GetPagedAsync(int pageNumber, int pageSize, Guid? subjectId, Guid? studentId, DateOnly? date, ClaimsContext caller);
        Task<GradeStatisticsDto?> GetStatisticsAsync(ClaimsContext caller);
        Task<GradeFiltersDto> GetFiltersAsync(ClaimsContext caller);
        Task<List<GradeDto>> GetGradesByStudentIdAsync(Guid studentId);
        Task<List<GradeDto>?> GetGradesBySubjectNameAsync(string subjectName, ClaimsContext caller);
        Task<List<GradeDto>> GetGradesByDateAsync(DateOnly date, ClaimsContext caller);
        Task<Guid?> AddAsync(CreateGradeDto dto, ClaimsContext caller);
        Task<bool> UpdateAsync(Guid id, UpdateGradeDto dto, ClaimsContext caller);
        Task<bool> DeleteAsync(Guid id);
    }
}
