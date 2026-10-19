using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Models;
using Microsoft.EntityFrameworkCore;
using E=ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal sealed partial class CourseStructureRepository
{
    private Task<int> AppendEventAsync(TenantContext t, Guid eventId, Guid editionId, Guid? enrollmentId, Guid? campusId, Guid? classId,
        string action, string? previousStatus, string? newStatus, DateOnly? previousDate, DateOnly? newDate,
        long? revisionBefore, long? revisionAfter, string? reason, string? hash, string? before, string after, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO course_administration_event (institution_id,id,actor_user_id,actor_membership_id,course_edition_id,enrollment_id,campus_id,class_id,action,previous_status,new_status,previous_completion_date,new_completion_date,revision_before,revision_after,reason,request_hash,before_json,after_json,occurred_at_utc) VALUES ({t.InstitutionId},{eventId},{t.UserId},{t.MembershipId},{editionId},{enrollmentId},{campusId},{classId},{action},{previousStatus},{newStatus},{previousDate},{newDate},{revisionBefore},{revisionAfter},{reason},{hash},{before},{after},UTC_TIMESTAMP(6))",ct);

    public async Task<EnrollmentMutationResult> MutateEnrollmentAsync(TenantContext t, Guid id, Guid eventId, string? targetStatus,
        DateOnly completionDate, string reason, long expectedRevision, bool correction, CancellationToken ct)
    {
        var action=correction?"enrollment.completion-date.correct":"enrollment.transition";
        var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
            id,eventId,targetStatus,completionDate,reason,expectedRevision,correction,t.UserId,t.MembershipId }))));
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM student_enrollment WHERE institution_id={t.InstitutionId} AND id={id} FOR UPDATE",ct);
        var row=await db.StudentEnrollments.AsNoTracking().Where(e=>e.InstitutionId==t.InstitutionId && e.Id==id)
            .Select(e=>new {e.CourseEditionId,e.EnrollmentDate,e.CompletionDate,e.Status,Revision=EF.Property<long>(e,"Revision")}).SingleOrDefaultAsync(ct) ?? throw Missing();
        // Enrollment lock precedes the first consistent read; same-enrollment replay observes the committed winner.
        // No missing-event gap lock: unrelated A/B intentions do not contend on an audit index gap.
        var prior=await db.CourseAdministrationEvents.AsNoTracking().SingleOrDefaultAsync(e=>e.InstitutionId==t.InstitutionId && e.Id==eventId,ct);
        if(prior is not null)
        {
            if(prior.EnrollmentId!=id || prior.Action!=action || prior.RequestHash!=hash) throw new CourseConflictException();
            await tx.CommitAsync(ct);
            return new(eventId,prior.RevisionAfter!.Value,prior.NewStatus!,prior.NewCompletionDate);
        }
        if(row.Revision!=expectedRevision || row.Revision==long.MaxValue) throw new CourseConflictException();
        if(completionDate<row.EnrollmentDate) throw new ArgumentException("Data amministrativa non valida.");
        string next;
        if(correction)
        {
            if(row.Status is not("completed" or "withdrawn" or "cancelled") || row.CompletionDate==completionDate) throw new CourseConflictException();
            next=row.Status;
        }
        else
        {
            if(row.Status!="enrolled" || targetStatus is not("completed" or "withdrawn" or "cancelled")) throw new CourseConflictException();
            next=targetStatus;
        }
        var revision=row.Revision+1;
        var changed=await db.StudentEnrollments.Where(e=>e.InstitutionId==t.InstitutionId && e.Id==id && EF.Property<long>(e,"Revision")==expectedRevision)
            .ExecuteUpdateAsync(s=>s.SetProperty(e=>e.Status,next).SetProperty(e=>e.CompletionDate,(DateOnly?)completionDate)
                .SetProperty(e=>EF.Property<long>(e,"Revision"),revision),ct);
        if(changed!=1) throw new CourseConflictException();
        await AppendEventAsync(t,eventId,row.CourseEditionId,id,null,null,action,row.Status,next,row.CompletionDate,completionDate,
            row.Revision,revision,reason,hash,JsonSerializer.Serialize(new {row.Status,row.CompletionDate,row.Revision}),
            JsonSerializer.Serialize(new {Status=next,CompletionDate=completionDate,Revision=revision}),ct);
        await tx.CommitAsync(ct);
        return new(eventId,revision,next,completionDate);
    }
    private async Task<List<CourseAdministrationEventDto>> EventsAsync(TenantContext t, Guid? enrollment, Guid? edition, int page, int size, CancellationToken ct)
    {
        var q=db.CourseAdministrationEvents.AsNoTracking().Where(e=>e.InstitutionId==t.InstitutionId);
        if(enrollment is {} id) q=q.Where(e=>e.EnrollmentId==id);
        if(edition is {} course) q=q.Where(e=>e.CourseEditionId==course);
        // Select only ledger scalar columns; no navigation graph or global materialization.
        var ordered=enrollment is not null ? q.OrderBy(e=>e.RevisionAfter).ThenBy(e=>e.OccurredAtUtc).ThenBy(e=>e.Id)
            : q.OrderBy(e=>e.OccurredAtUtc).ThenBy(e=>e.Id);
        var rows=await ordered.Skip((page-1)*size).Take(size)
            .Select(e=>new CourseAdministrationEventDto(e.Id,e.InstitutionId,e.ActorUserId,e.ActorMembershipId,e.CourseEditionId,
                e.EnrollmentId,e.CampusId,e.ClassId,e.Action,e.PreviousStatus,e.NewStatus,e.PreviousCompletionDate,e.NewCompletionDate,
                e.RevisionBefore,e.RevisionAfter,e.Reason,e.BeforeJson,e.AfterJson,e.OccurredAtUtc)).ToListAsync(ct);
        return rows.Select(e=>e with { OccurredAtUtc=DateTime.SpecifyKind(e.OccurredAtUtc,DateTimeKind.Utc) }).ToList();
    }
    public Task<List<CourseAdministrationEventDto>> EnrollmentHistoryAsync(TenantContext t, Guid id, int page, int size, CancellationToken ct) => EventsAsync(t,id,null,page,size,ct);
    public Task<List<CourseAdministrationEventDto>> EditionAuditAsync(TenantContext t, Guid id, int page, int size, CancellationToken ct) => EventsAsync(t,null,id,page,size,ct);
    public async Task LinkCampusAuditedAsync(TenantContext t, Guid edition, Guid campus, CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        await LinkCampusAsync(t,edition,campus,ct);
        await AppendEventAsync(t,Guid.NewGuid(),edition,null,campus,null,"campus.link",null,null,null,null,null,null,null,null,null,
            JsonSerializer.Serialize(new {CourseEditionId=edition,CampusId=campus}),ct);
        await tx.CommitAsync(ct);
    }
    public async Task AssignClassAuditedAsync(TenantContext t, Guid id, Guid edition, CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM class WHERE institution_id={t.InstitutionId} AND id={id} FOR UPDATE",ct);
        var previous=await db.Classes.AsNoTracking().Where(e=>e.Id==id && EF.Property<Guid>(e,"InstitutionId")==t.InstitutionId)
            .Select(e=>new {Edition=EF.Property<Guid?>(e,"CourseEditionId")}).SingleOrDefaultAsync(ct) ?? throw Missing();
        await AssignClassAsync(t,id,edition,ct);
        if(previous.Edition is null) await AppendEventAsync(t,Guid.NewGuid(),edition,null,null,id,"class.assign",null,null,null,null,null,null,null,null,null,
            JsonSerializer.Serialize(new {ClassId=id,CourseEditionId=edition}),ct);
        await tx.CommitAsync(ct);
    }
}
