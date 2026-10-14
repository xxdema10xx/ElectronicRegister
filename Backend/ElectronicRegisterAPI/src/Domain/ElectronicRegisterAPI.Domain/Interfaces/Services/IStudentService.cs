using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Services

{
    public interface IStudentService
    {
        Task<Student> EnsureStudentCanBeDeletedAsync(Guid studentId);
    }
}
