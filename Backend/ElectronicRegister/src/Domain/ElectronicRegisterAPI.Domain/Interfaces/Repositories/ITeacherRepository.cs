using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface ITeacherRepository
{
    Task<int> CountAsync();
    Task<List<TeacherDto>> GetAllAsync();
    Task<TeacherDto?> GetByIdAsync(Guid id);
    Task AddAsync(TeacherDto teacherDto);
    Task UpdateAsync(TeacherDto teacherDto);
    Task DeleteAsync(Guid id);
}