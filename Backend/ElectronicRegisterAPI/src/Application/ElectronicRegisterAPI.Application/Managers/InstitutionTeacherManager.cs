using ElectronicRegisterAPI.Application.Services;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Application.Managers;

internal sealed class InstitutionTeacherManager(InstitutionSchoolAuthorization access,
    IInstitutionTeacherRepository teachers, ITransactionManager transactions) : IInstitutionTeacherManager
{
    private static TeacherDto Map(Teacher t) => new() { Id = t.Id, FirstName = t.FirstName, LastName = t.LastName };
    public async Task<List<TeacherDto>> GetAllAsync(CancellationToken ct) => (await teachers.GetAllAsync(await access.ReadAsync(false, ct), ct)).Select(Map).ToList();
    public async Task<int> CountAsync(CancellationToken ct)
    {
        var t = await access.ReadAsync(false, ct);
        if (!t.Roles.Contains(InstitutionRole.InstitutionAdmin) && !t.Roles.Contains(InstitutionRole.Staff) && !t.Roles.Contains(InstitutionRole.Teacher)) throw new UnauthorizedAccessException("Conteggio non autorizzato.");
        return await teachers.CountAsync(t, ct);
    }
    public async Task<TeacherDto?> GetByIdAsync(Guid id, CancellationToken ct)
    { var t = await teachers.GetByIdAsync(await access.ReadAsync(false, ct), id, ct); return t is null ? null : Map(t); }
    public async Task<List<TeacherDto>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        (await teachers.GetByIdsAsync(await access.ReadAsync(false, ct), ids, ct)).Select(Map).ToList();
    public async Task<List<TeacherDto>> GetByLastNameAsync(string name, CancellationToken ct) =>
        (await teachers.GetByLastNameAsync(await access.ReadAsync(false, ct), name, ct)).Select(Map).ToList();
    public async Task<List<TeacherDto>> GetForClassAsync(Guid id, CancellationToken ct) =>
        (await teachers.GetForClassAsync(await access.ReadAsync(false, ct), id, ct)).Select(Map).ToList();
    public async Task<TeacherDto?> GetOwnProfileAsync(CancellationToken ct)
    { var (t, id) = await access.OwnAsync(true, ct); var row = await teachers.GetByIdAsync(t, id, ct); return row is null ? null : Map(row); }
    public async Task<Guid> AddAsync(CreateTeacherDto dto, Guid? membershipId, CancellationToken ct)
    {
        var t = await access.PermissionAsync(InstitutionPermission.UserCreate, ct);
        var row = new Teacher { Id = Guid.NewGuid(), FirstName = InstitutionSchoolAuthorization.Name(dto.FirstName), LastName = InstitutionSchoolAuthorization.Name(dto.LastName) };
        await transactions.ExecuteAsync(() => teachers.AddAsync(t, row, membershipId, ct), ct);
        return row.Id;
    }
    public async Task UpdateAsync(Guid id, UpdateTeacherDto dto, CancellationToken ct)
    {
        var t = await access.PermissionAsync(InstitutionPermission.UserUpdate, ct);
        await teachers.UpdateAsync(t, new Teacher { Id = id, FirstName = InstitutionSchoolAuthorization.Name(dto.FirstName), LastName = InstitutionSchoolAuthorization.Name(dto.LastName) }, ct);
    }
    public async Task DeleteAsync(Guid id, CancellationToken ct)
    { var t = await access.AdminAsync(ct); await transactions.ExecuteAsync(() => teachers.DeleteAsync(t, id, ct), ct); }
}
