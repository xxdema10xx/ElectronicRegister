using ElectronicRegisterAPI.Application.Services;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Application.Managers;

internal sealed class InstitutionSubjectManager(InstitutionSchoolAuthorization access,
    IInstitutionSubjectRepository subjects, ITransactionManager transactions) : IInstitutionSubjectManager
{
    private static SubjectDto Map(Subject s) => new() { Id = s.Id, Name = s.Name };
    public async Task<List<SubjectDto>> GetAllAsync(CancellationToken ct) => (await subjects.GetAllAsync(await access.ReadAsync(false, ct), ct)).Select(Map).ToList();
    public async Task<int> CountAsync(CancellationToken ct) => await subjects.CountAsync(await access.ReadAsync(false, ct), ct);
    public async Task<SubjectDto?> GetByIdAsync(Guid id, CancellationToken ct)
    { var row = await subjects.GetByIdAsync(await access.ReadAsync(false, ct), id, ct); return row is null ? null : Map(row); }
    public async Task<List<SubjectDto>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        (await subjects.GetByIdsAsync(await access.ReadAsync(false, ct), ids, ct)).Select(Map).ToList();
    public async Task<SubjectDto?> GetByNameAsync(string name, CancellationToken ct)
    { var row = await subjects.GetByNameAsync(await access.ReadAsync(false, ct), name, ct); return row is null ? null : Map(row); }
    public async Task<List<SubjectDto>> GetByTeacherAsync(Guid id, CancellationToken ct) =>
        (await subjects.GetByTeacherAsync(await access.ReadAsync(false, ct), id, ct)).Select(Map).ToList();
    public async Task<Guid> AddAsync(CreateSubjectDto dto, CancellationToken ct)
    {
        var t = await access.AdminAsync(ct);
        var row = new Subject { Id = Guid.NewGuid(), Name = InstitutionSchoolAuthorization.Name(dto.Name, 150) };
        await transactions.ExecuteAsync(() => subjects.AddAsync(t, row, ct), ct);
        return row.Id;
    }
    public async Task UpdateAsync(Guid id, UpdateSubjectDto dto, CancellationToken ct)
    { var t = await access.AdminAsync(ct); await subjects.UpdateAsync(t, new Subject { Id = id, Name = InstitutionSchoolAuthorization.Name(dto.Name, 150) }, ct); }
    public async Task DeleteAsync(Guid id, CancellationToken ct)
    { var t = await access.AdminAsync(ct); await transactions.ExecuteAsync(() => subjects.DeleteAsync(t, id, ct), ct); }
}
