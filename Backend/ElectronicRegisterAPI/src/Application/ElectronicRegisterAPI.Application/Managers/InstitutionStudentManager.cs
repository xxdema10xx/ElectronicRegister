using ElectronicRegisterAPI.Application.Services;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Application.Managers;

internal sealed class InstitutionStudentManager(InstitutionSchoolAuthorization access,
    IInstitutionStudentRepository students, ITransactionManager transactions) : IInstitutionStudentManager
{
    private static StudentDto Map(Student s) => new() { Id = s.Id, FirstName = s.FirstName, LastName = s.LastName };
    public async Task<List<StudentDto>> GetForClassAsync(Guid classId, CancellationToken ct) =>
        (await students.GetForClassAsync(await access.ReadAsync(true, ct), classId, ct)).Select(Map).ToList();
    public async Task<List<StudentDto>> GetAllAsync(CancellationToken ct) => (await students.GetAllAsync(await access.ReadAsync(true, ct), ct)).Select(Map).ToList();
    public async Task<int> CountAsync(CancellationToken ct) => await students.CountAsync(await access.ReadAsync(true, ct), ct);
    public async Task<StudentDto?> GetByIdAsync(Guid id, CancellationToken ct)
    { var s = await students.GetByIdAsync(await access.ReadAsync(true, ct), id, ct); return s is null ? null : Map(s); }
    public async Task<List<StudentDto>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        (await students.GetByIdsAsync(await access.ReadAsync(true, ct), ids, ct)).Select(Map).ToList();
    public async Task<List<StudentDto>> GetByLastNameAsync(string name, CancellationToken ct) =>
        (await students.GetByLastNameAsync(await access.ReadAsync(true, ct), name, ct)).Select(Map).ToList();
    public async Task<StudentDto?> GetOwnProfileAsync(CancellationToken ct)
    { var (t, id) = await access.OwnAsync(false, ct); var row = await students.GetOwnAsync(t, id, ct); return row is null ? null : Map(row); }
    public async Task<Guid> AddAsync(CreateStudentDto dto, Guid? classId, Guid? membershipId, CancellationToken ct)
    {
        var t = await access.AdminAsync(ct);
        var row = new Student { Id = Guid.NewGuid(), FirstName = InstitutionSchoolAuthorization.Name(dto.FirstName), LastName = InstitutionSchoolAuthorization.Name(dto.LastName), ClassId = classId };
        await transactions.ExecuteAsync(() => students.AddAsync(t, row, membershipId, ct), ct);
        return row.Id;
    }
    public async Task UpdateAsync(Guid id, UpdateStudentDto dto, CancellationToken ct)
    {
        var t = await access.AdminAsync(ct);
        await students.UpdateAsync(t, new Student { Id = id, FirstName = InstitutionSchoolAuthorization.Name(dto.FirstName), LastName = InstitutionSchoolAuthorization.Name(dto.LastName) }, ct);
    }
    public async Task DeleteAsync(Guid id, CancellationToken ct)
    { var t = await access.AdminAsync(ct); await transactions.ExecuteAsync(() => students.DeleteAsync(t, id, ct), ct); }
}
