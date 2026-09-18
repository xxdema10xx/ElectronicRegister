namespace ElectronicRegisterAPI.Domain.Interfaces.Services
{
    public interface IStudentService
    {
        Task EnsureStudentCanBeDeletedAsync(Guid studentId);
    }
}
