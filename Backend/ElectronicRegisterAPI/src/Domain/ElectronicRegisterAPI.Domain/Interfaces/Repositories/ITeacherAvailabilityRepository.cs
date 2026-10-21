using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories
{
    public interface ITeacherAvailabilityRepository
    {
        Task<TeacherAvailability?> GetByIdAsync(Guid id);
        Task<List<TeacherAvailability>> GetAsync(
        AvailabilityStatus? status, DateOnly? from, DateOnly? to);

        Task<List<TeacherAvailability>> GetByClassSubjectIdsAsync(
            IEnumerable<Guid> classSubjectIds,
            AvailabilityStatus? status = null,
            DateOnly? from = null,
            DateOnly? to = null);

        Task<List<TeacherAvailability>> GetActiveByTeacherAndDateAsync(Guid teacherId, DateOnly date);

        Task AddAsync(TeacherAvailability availability);
        Task UpdateAsync(TeacherAvailability availability);
        Task DeleteAsync(TeacherAvailability availability);
    }
}
