using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Services
{
    public interface ITeacherAvailabilityService
    {
        void EnsureValidTimeRange(DateOnly date, TimeOnly start, TimeOnly end);
        Task EnsureTeacherTeachesClassSubjectAsync(Guid teacherId, Guid classSubjectId);
        Task EnsureNoOverlapAsync(Guid teacherId, DateOnly date, TimeOnly start, TimeOnly end);
        Task<int> GetApprovedHoursAsync(Guid classSubjectId);
        Task EnsureEnoughHoursAsync(Guid classSubjectId, int hours);
        void EnsureIsPending(TeacherAvailability availability);
    }
}
