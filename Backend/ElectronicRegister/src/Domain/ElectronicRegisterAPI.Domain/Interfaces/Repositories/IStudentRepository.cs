using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface IStudentRepository
{
    Task<int> CountAsync();
    Task<List<StudentDto>> GetAllAsync();
    Task<StudentDto?> GetByIdAsync(Guid id);
    Task<List<StudentDto>> GetByIdsAsync(IEnumerable<Guid> ids);
    Task<List<StudentDto>> GetByLastNameAsync(string lastName);
    Task AddAsync(StudentDto studentDto);
    Task UpdateAsync(StudentDto studentDto);
    Task DeleteAsync(Guid id);
}