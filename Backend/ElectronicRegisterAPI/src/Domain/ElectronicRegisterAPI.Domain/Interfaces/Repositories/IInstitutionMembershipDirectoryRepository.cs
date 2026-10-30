using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface IInstitutionMembershipDirectoryRepository
{
    Task<IReadOnlyList<ElectronicRegisterAPI.Domain.Results.ProfileAcquisitionResult>> AcquisitionsAsync(
        Guid institutionId,
        Guid membershipId,
        int page,
        int size,
        string? status,
        CancellationToken ct
    );
    Task<IReadOnlyList<InstitutionMemberDto>> ListAsync(
        Guid institutionId,
        int pageNumber,
        int pageSize,
        string? status,
        string? role,
        string? search,
        CancellationToken ct
    );
    Task<InstitutionMemberDto?> GetAsync(Guid institutionId, Guid membershipId, CancellationToken ct);
}
