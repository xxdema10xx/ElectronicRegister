using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Managers;

public interface IInstitutionAcademicCatalogManager
{
    Task<InstitutionAcademicPage<InstitutionStudyAreaDto>> ListStudyAreaAsync(int page, int size, CancellationToken ct);
    Task<InstitutionStudyAreaDto?> GetStudyAreaAsync(Guid id, CancellationToken ct);
    Task<Guid> CreateStudyAreaAsync(CreateInstitutionStudyAreaDto dto, CancellationToken ct);
    Task UpdateStudyAreaAsync(
        Guid id,
        UpdateInstitutionStudyAreaDto dto,
        long revision,
        CancellationToken ct
    );
    Task DeleteStudyAreaAsync(Guid id, CancellationToken ct);
    Task<InstitutionAcademicPage<InstitutionStudyPathDto>> ListStudyPathAsync(int page, int size, CancellationToken ct);
    Task<InstitutionStudyPathDto?> GetStudyPathAsync(Guid id, CancellationToken ct);
    Task<Guid> CreateStudyPathAsync(CreateInstitutionStudyPathDto dto, CancellationToken ct);
    Task UpdateStudyPathAsync(
        Guid id,
        UpdateInstitutionStudyPathDto dto,
        long revision,
        CancellationToken ct
    );
    Task DeleteStudyPathAsync(Guid id, CancellationToken ct);
    Task<InstitutionAcademicPage<InstitutionBienniumDto>> ListBienniumAsync(int page, int size, CancellationToken ct);
    Task<InstitutionBienniumDto?> GetBienniumAsync(Guid id, CancellationToken ct);
    Task<Guid> CreateBienniumAsync(CreateInstitutionBienniumDto dto, CancellationToken ct);
    Task UpdateBienniumAsync(
        Guid id,
        UpdateInstitutionBienniumDto dto,
        long revision,
        CancellationToken ct
    );
    Task DeleteBienniumAsync(Guid id, CancellationToken ct);
    Task<InstitutionAcademicPage<InstitutionBienniumStudyAreaDto>> ListBienniumStudyAreaAsync(int page, int size, CancellationToken ct);
    Task<InstitutionBienniumStudyAreaDto?> GetBienniumStudyAreaAsync(Guid id, CancellationToken ct);
    Task<Guid> CreateBienniumStudyAreaAsync(CreateInstitutionBienniumStudyAreaDto dto, CancellationToken ct);
    Task DeleteBienniumStudyAreaAsync(Guid id, CancellationToken ct);
    Task<InstitutionAcademicPage<InstitutionBienniumStudyPathDto>> ListBienniumStudyPathAsync(int page, int size, CancellationToken ct);
    Task<InstitutionBienniumStudyPathDto?> GetBienniumStudyPathAsync(Guid id, CancellationToken ct);
    Task<Guid> CreateBienniumStudyPathAsync(CreateInstitutionBienniumStudyPathDto dto, CancellationToken ct);
    Task DeleteBienniumStudyPathAsync(Guid id, CancellationToken ct);
}
