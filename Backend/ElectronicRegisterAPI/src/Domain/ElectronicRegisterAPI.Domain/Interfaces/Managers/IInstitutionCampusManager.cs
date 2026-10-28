using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Managers;

public interface IInstitutionCampusManager
{
    Task<InstitutionAcademicPage<InstitutionCampusDto>> ListAsync(
        int page,
        int size,
        bool? isActive,
        CancellationToken ct
    );
    Task<InstitutionCampusDto?> GetAsync(Guid id, CancellationToken ct);
    Task<Guid> CreateAsync(CreateInstitutionCampusDto dto, CancellationToken ct);
    Task UpdateAsync(
        Guid id,
        UpdateInstitutionCampusDto dto,
        long revision,
        CancellationToken ct
    );
    Task DeleteAsync(Guid id, CancellationToken ct);
}
