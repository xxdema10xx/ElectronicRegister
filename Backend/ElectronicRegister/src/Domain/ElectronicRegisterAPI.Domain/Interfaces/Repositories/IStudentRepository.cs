using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface IStudentRepository
{
    Task<int> CountAsync();
    Task<List<Student>> GetAllAsync();
    Task<Student?> GetByIdAsync(Guid id);
    Task<List<Student>> GetByIdsAsync(IEnumerable<Guid> ids);
    Task<List<Student>> GetByLastNameAsync(string lastName);
    Task AddAsync(Student student);
    Task UpdateAsync(Student student);
    Task DeleteAsync(Guid id);
}