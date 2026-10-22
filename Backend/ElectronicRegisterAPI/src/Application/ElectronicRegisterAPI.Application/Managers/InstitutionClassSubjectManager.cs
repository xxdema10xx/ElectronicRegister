using ElectronicRegisterAPI.Application.Services;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Application.Managers;
internal sealed class InstitutionClassSubjectManager(InstitutionSchoolAuthorization authorization, IInstitutionProfileResolver profiles, IInstitutionClassSubjectRepository repository, ITransactionManager transactions) : IInstitutionClassSubjectManager
{
    public async Task<InstitutionAcademicPage<InstitutionClassSubjectDto>> ListAsync(int page, int size, Guid? classId, Guid? subjectId, Guid? teacherId, CancellationToken ct)
    {
        var t = await authorization.RequireAsync(ct);
        if (!t.Roles.Contains(InstitutionRole.InstitutionAdmin))
            await profiles.RequireTeacherAsync(t, ct);
        InstitutionClassRules.Page(page, size);
        if (classId == Guid.Empty || subjectId == Guid.Empty || teacherId == Guid.Empty)
            throw new ArgumentException("Filtro non valido.");
        return await repository.ListAsync(t, page, size, classId, subjectId, teacherId, ct);
    }

    public async Task<InstitutionClassSubjectDto?> GetAsync(Guid id, CancellationToken ct) => await repository.GetAsync(await authorization.AdminAsync(ct), id, ct);
    public async Task<Guid> CreateAsync(CreateInstitutionClassSubjectDto dto, CancellationToken ct)
    {
        var t = await authorization.AdminAsync(ct);
        InstitutionAcademicRules.Id(dto.ClassId);
        InstitutionAcademicRules.Id(dto.SubjectId);
        InstitutionAcademicRules.Id(dto.TeacherId);
        Guid id = Guid.Empty;
        await transactions.ExecuteAsync(async () => id = await repository.CreateAsync(t, dto, ct), ct);
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateInstitutionClassSubjectDto dto, long revision, CancellationToken ct)
    {
        var t = await authorization.AdminAsync(ct);
        InstitutionAcademicRules.Id(dto.TeacherId);
        await transactions.ExecuteAsync(() => repository.UpdateAsync(t, id, dto, revision, ct), ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var t = await authorization.AdminAsync(ct);
        await transactions.ExecuteAsync(() => repository.DeleteAsync(t, id, ct), ct);
    }
}
