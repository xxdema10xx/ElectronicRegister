using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface IStudentRepository
{
    Task<int> CountAsync(Guid? teacherId = null);
    Task<List<Student>> GetAllAsync(Guid? teacherId = null);
    Task<Student?> GetByIdAsync(Guid id, Guid? teacherId = null);
    Task<List<Student>> GetByIdsAsync(IEnumerable<Guid> ids);
    Task<List<Student>> GetByLastNameAsync(string lastName, Guid? teacherId = null);
    Task AddAsync(Student student);
    Task UpdateAsync(Student student);
    Task DeleteAsync(Student student);
}
