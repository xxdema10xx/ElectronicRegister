using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface IInstitutionMembershipRepository
{
    Task<InstitutionMembership?> GetAsync(Guid institutionId, Guid userId);
    Task AddAsync(InstitutionMembership membership);
    Task AddRoleAsync(Guid institutionId, Guid membershipId, InstitutionRole role);
    Task SuspendAsync(Guid institutionId, Guid membershipId, DateTime suspendedAtUtc);
}
