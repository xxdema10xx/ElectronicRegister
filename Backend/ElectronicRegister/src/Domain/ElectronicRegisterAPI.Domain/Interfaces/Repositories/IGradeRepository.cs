using System;
using System.Collections.Generic;
using System.Text;
using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface IGradeRepository
{
    Task CreateAsync(CreateGradeDto gradeDto);
    Task<List<GradeDto>> ReadAllAsync();
    Task UpdateAsync(Guid id, UpdateGradeDto dto);
    Task DeleteAsync(Guid id);
    Task<int> CountAsync();
    Task<GradeStatisticsDto> GetStatisticsAsync();
    Task<GradeFiltersDto> GetFiltersAsync();
    Task<GradePageDto> GetPagedAsync(int pageNumber = 1,int pageSize = 20,Guid? subjectId = null,Guid? studentId = null,DateOnly? date = null);
    Task<GradeDto> GetByIdAsync(Guid id);
    Task<List<GradeDto>> GetGradesByStudentIdAsync(Guid id);
    Task<List<GradeDto>> GetGradesBySubjectNameAsync(string subject);
    Task<List<GradeDto>> GetGradesByDateAsync(DateOnly date);
    
}
