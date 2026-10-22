using ElectronicRegisterAPI.Domain.DTOs;
namespace ElectronicRegisterAPI.Domain.Interfaces.Managers;

public interface IInstitutionClassManager
{
    Task<InstitutionClassPageDto> GetAllAsync(int page, int size, Guid? path, Guid? edition, CancellationToken ct);
    Task<InstitutionClassDto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Guid> AddAsync(CreateInstitutionClassDto dto, CancellationToken ct);
    Task UpdateAsync(Guid id, UpdateInstitutionClassDto dto, long expectedRevision, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}
