namespace ElectronicRegisterAPI.Domain.Interfaces.Services
{
    public interface ITeacherService
    {
        Task EnsureTeacherCanBeDeletedAsync(Guid teacherId);
    }
}
