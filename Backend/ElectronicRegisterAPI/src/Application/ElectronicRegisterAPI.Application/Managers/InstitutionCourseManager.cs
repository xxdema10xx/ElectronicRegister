using ElectronicRegisterAPI.Application.Services;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Models;
namespace ElectronicRegisterAPI.Application.Managers;

internal sealed class InstitutionCourseManager(InstitutionSchoolAuthorization access,
    IInstitutionProfileResolver profiles, ICourseStructureRepository repository) : IInstitutionCourseManager
{
    private static void ValidateMutation(Guid eventId, DateOnly? date, string? reason, long? revision)
    {
        if(eventId==Guid.Empty || date is null || date.Value.Year<1000 || revision is null || revision<0 ||
            string.IsNullOrWhiteSpace(reason) || reason.Length>1000) throw new ArgumentException("Operazione amministrativa non valida.");
    }
    public async Task<EnrollmentMutationResult> TransitionAsync(Guid id, TransitionEnrollmentDto dto, CancellationToken ct)
    {
        var t=await access.AdminAsync(ct); ValidateMutation(dto.EventId,dto.CompletionDate,dto.Reason,dto.ExpectedRevision);
        if(dto.TargetStatus is not("enrolled" or "completed" or "withdrawn" or "cancelled")) throw new ArgumentException("Stato non valido.");
        return await repository.MutateEnrollmentAsync(t,id,dto.EventId,dto.TargetStatus,dto.CompletionDate!.Value,dto.Reason,dto.ExpectedRevision!.Value,false,ct);
    }
    public async Task<EnrollmentMutationResult> CorrectCompletionAsync(Guid id, CorrectEnrollmentCompletionDto dto, CancellationToken ct)
    {
        var t=await access.AdminAsync(ct); ValidateMutation(dto.EventId,dto.CompletionDate,dto.Reason,dto.ExpectedRevision);
        return await repository.MutateEnrollmentAsync(t,id,dto.EventId,null,dto.CompletionDate!.Value,dto.Reason,dto.ExpectedRevision!.Value,true,ct);
    }
    public async Task<List<CourseAdministrationEventDto>> HistoryAsync(Guid id, int page, int size, CancellationToken ct)
    {
        var t=await access.AdminAsync(ct); Page(page,size);
        _=await repository.FindEnrollmentAsync(t,id,ct) ?? throw new KeyNotFoundException();
        return await repository.EnrollmentHistoryAsync(t,id,page,size,ct);
    }
    public async Task<List<CourseAdministrationEventDto>> EditionAuditAsync(Guid id, int page, int size, CancellationToken ct)
    {
        var t=await access.AdminAsync(ct); Page(page,size); await EditionExists(t,id,ct);
        return await repository.EditionAuditAsync(t,id,page,size,ct);
    }
    private static void Page(int page, int size)
    { if (page < 1 || page > 1000000 || size < 1 || size > 100) throw new ArgumentException("Pagina non valida."); }
    private async Task EditionExists(TenantContext t, Guid id, CancellationToken ct)
    { _ = await repository.FindEditionAsync(t, id, null, ct) ?? throw new KeyNotFoundException(); }
    public async Task<List<CourseEditionDto>> EditionsAsync(Guid? studyPathId, int page, int size, CancellationToken ct)
    { var t=await access.AdminAsync(ct); Page(page,size); return await repository.FindEditionsAsync(t,studyPathId,page,size,ct); }
    public async Task<CourseEditionDto> EditionAsync(Guid? id, string? code, CancellationToken ct)
    { var t=await access.AdminAsync(ct); return await repository.FindEditionAsync(t,id,code,ct) ?? throw new KeyNotFoundException(); }
    public async Task<Guid> CreateEditionAsync(CreateCourseEditionDto dto, CancellationToken ct)
    {
        var t=await access.AdminAsync(ct); var id=Guid.NewGuid();
        await repository.InsertEditionAsync(t,new(id,t.InstitutionId,dto.StudyPathId,dto.Code,dto.Name,dto.StartDate,dto.EndDate,dto.Status),ct);
        return id;
    }
    public async Task UpdateEditionAsync(Guid id, UpdateCourseEditionDto dto, CancellationToken ct)
    { var t=await access.AdminAsync(ct); await repository.UpdateDescriptionAsync(t,id,dto,ct); }
    public async Task<List<CampusDto>> CampusesAsync(Guid? editionId, int page, int size, CancellationToken ct)
    {
        var t=await access.AdminAsync(ct); Page(page,size); if(editionId is {} id) await EditionExists(t,id,ct);
        return await repository.FindCampusesAsync(t,editionId,page,size,ct);
    }
    public async Task LinkCampusAsync(Guid editionId, Guid campusId, CancellationToken ct)
    { var t=await access.AdminAsync(ct); await repository.LinkCampusAuditedAsync(t,editionId,campusId,ct); }
    public async Task<List<EditionClassDto>> ClassesAsync(Guid editionId, int page, int size, CancellationToken ct)
    { var t=await access.AdminAsync(ct); Page(page,size); await EditionExists(t,editionId,ct); return await repository.FindClassesAsync(t,editionId,page,size,ct); }
    public async Task AssignClassAsync(Guid editionId, Guid classId, CancellationToken ct)
    {
        var t=await access.AdminAsync(ct);
        try { await repository.AssignClassAuditedAsync(t,classId,editionId,ct); }
        catch(InvalidOperationException ex) when(ex.Message == "Classe gia attribuita.") { throw new CourseConflictException(); }
    }
    public async Task<StudentEnrollmentDto> EnrollmentAsync(Guid id, CancellationToken ct)
    { var t=await access.AdminAsync(ct); return await repository.FindEnrollmentAsync(t,id,ct) ?? throw new KeyNotFoundException(); }
    public async Task<List<StudentEnrollmentDto>> EnrollmentsAsync(Guid? studentId, Guid? editionId, int page, int size, CancellationToken ct)
    {
        var t=await access.AdminAsync(ct); Page(page,size);
        if(studentId is {} s && !await repository.StudentExistsAsync(t,s,ct)) throw new KeyNotFoundException();
        if(editionId is {} e) await EditionExists(t,e,ct);
        if(studentId is null && editionId is null) throw new ArgumentException("Filtro richiesto.");
        return await repository.FindEnrollmentsAsync(t,studentId,editionId,page,size,ct);
    }
    public async Task<Guid> CreateEnrollmentAsync(CreateStudentEnrollmentDto dto, CancellationToken ct)
    {
        var t=await access.AdminAsync(ct); var id=Guid.NewGuid();
        await repository.InsertEnrollmentAsync(t,new(id,t.InstitutionId,dto.StudentId,dto.CourseEditionId,dto.EnrollmentDate,dto.CompletionDate,dto.Status),ct);
        return id;
    }
    public async Task<List<OwnEnrollmentDto>> OwnEnrollmentsAsync(int page, int size, CancellationToken ct)
    {
        var t=await access.RequireAsync(ct);
        if(!t.Roles.Contains(InstitutionRole.Student)) throw new UnauthorizedAccessException();
        var id=await profiles.RequireStudentAsync(t,ct); Page(page,size);
        var rows=await repository.FindEnrollmentsAsync(t,id,null,page,size,ct);
        return rows.Select(e=>new OwnEnrollmentDto(e.EnrollmentId,e.CourseEditionId,e.CourseEditionCode,e.CourseEditionName,e.EnrollmentDate,e.CompletionDate,e.Status)).ToList();
    }
}
