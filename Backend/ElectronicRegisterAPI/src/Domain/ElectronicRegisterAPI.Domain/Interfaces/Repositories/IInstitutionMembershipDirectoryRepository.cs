using ElectronicRegisterAPI.Domain.DTOs;
namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface IInstitutionMembershipDirectoryRepository
{
    Task<IReadOnlyList<InstitutionMemberDto>> ListAsync(Guid institutionId, int pageNumber, int pageSize,
        string? status, string? role, string? search, CancellationToken ct);
    Task<InstitutionMemberDto?> GetAsync(Guid institutionId, Guid membershipId, CancellationToken ct);
}
