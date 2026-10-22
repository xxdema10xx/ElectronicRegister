using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using E=ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
namespace ElectronicRegisterAPI.Infrastructure.Repositories;
internal sealed class InstitutionStudentTransferRepository(ElectronicRegisterContext db,InstitutionAdministrationPersistence audit) : IInstitutionStudentTransferRepository
{
    private static BusinessRuleException Conflict(string message="Trasferimento incompatibile con lo stato amministrativo corrente.")=>new(message);
    private static StudentClassTransferResultDto Receipt(E.StudentClassTransferEvent e)=>new(e.Id,e.StudentId,e.FromClassId,e.ToClassId,e.FromCourseEditionId,e.ToCourseEditionId,e.EffectiveDate,e.AppliedLimitMonths,e.RevisionAfter,DateTime.SpecifyKind(e.OccurredAtUtc,DateTimeKind.Utc));
    private IQueryable<E.Student> Students(TenantContext t)=>db.Students.Where(s=>EF.Property<Guid>(s,"InstitutionId")==t.InstitutionId);
    private IQueryable<E.StudentClassTransferEvent> Events(TenantContext t)=>db.StudentClassTransferEvents.AsNoTracking().Where(e=>e.InstitutionId==t.InstitutionId);
    public async Task<StudentClassTransferPageDto> HistoryAsync(TenantContext t,Guid studentId,int page,int size,CancellationToken ct)
    {
        var revision=await Students(t).Where(s=>s.Id==studentId).Select(s=>(long?)EF.Property<long>(s,"TransferRevision")).SingleOrDefaultAsync(ct) ?? throw InstitutionSchoolQueries.Missing();
        var query=Events(t).Where(e=>e.StudentId==studentId);
        var rows=await query.OrderBy(e=>e.OccurredAtUtc).ThenBy(e=>e.RevisionAfter).ThenBy(e=>e.Id).Skip((page-1)*size).Take(size).ToListAsync(ct);
        return new(rows.Select(e=>new StudentClassTransferHistoryDto(Receipt(e),e.FromClassName,e.ToClassName,e.FromCourseName,e.ToCourseName,e.SourceEnrollmentId,e.DestinationEnrollmentId,e.Reason,e.ActorUserId,e.ActorMembershipId,e.RevisionBefore,e.SourceCourseStartDate,e.DestinationCourseStartDate)).ToList(),await query.CountAsync(ct),page,size,revision);
    }
    public async Task<StudentClassTransferResultDto> TransferAsync(TenantContext t,Guid studentId,TransferStudentClassDto dto,AcademicTransferOptions options,TimeProvider clock,CancellationToken ct)
    {
        await using var transaction=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted,ct);
        StudentClassTransferResultDto receipt=null!;
        await audit.WriteAsync(t,async()=>
        {
            // IAM tenant-first locks acquired by WriteAsync. Student is the shared
            // serialization point with institutional exam START, before any exam lock.
            var student=await db.Students.FromSqlInterpolated($"SELECT * FROM student WHERE institution_id={t.InstitutionId} AND id={studentId} FOR UPDATE").AsNoTracking().SingleOrDefaultAsync(ct) ?? throw InstitutionSchoolQueries.Missing();
            var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{studentId,dto.EventId,dto.ToClassId,dto.EffectiveDate,dto.Reason,dto.ExpectedRevision,t.UserId,t.MembershipId}))));
            // Replay precedes current class, date, policy and revision checks. A committed
            // receipt remains valid after subsequent transfers and configuration changes.
            var prior=await Events(t).SingleOrDefaultAsync(e=>e.Id==dto.EventId,ct);
            if(prior is not null)
            {
                if(prior.StudentId!=studentId || prior.ActorUserId!=t.UserId || prior.ActorMembershipId!=t.MembershipId || prior.RequestHash!=hash)throw Conflict("EventId giÃ  utilizzato per una richiesta differente.");
                receipt=Receipt(prior);return;
            }
            var instant=clock.GetUtcNow();
            var today=StudentClassTransferPolicy.OperationalDate(instant,options.TimeZoneId);
            if(dto.EffectiveDate!=today)throw new ArgumentException("La data effettiva deve coincidere con la data operativa corrente.");
            var now=instant.UtcDateTime;now=new DateTime(now.Ticks/10*10,DateTimeKind.Utc);
            var revision=await Students(t).Where(s=>s.Id==studentId).Select(s=>EF.Property<long>(s,"TransferRevision")).SingleAsync(ct);
            if(revision!=dto.ExpectedRevision || revision==long.MaxValue)throw Conflict("Revisione dello studente non aggiornata.");
            if(student.ClassId is not {} from)throw Conflict("La classe di provenienza non Ã¨ configurata.");
            if(from==dto.ToClassId)throw Conflict("La classe destinazione deve essere diversa dalla provenienza.");
            var classes=new Dictionary<Guid,E.Class>();
            foreach(var id in new[]{from,dto.ToClassId}.OrderBy(x=>x))
                classes[id]=await db.Classes.FromSqlInterpolated($"SELECT * FROM class WHERE institution_id={t.InstitutionId} AND id={id} FOR UPDATE").AsNoTracking().SingleOrDefaultAsync(ct) ?? throw InstitutionSchoolQueries.Missing();
            var editions=await db.Classes.AsNoTracking().Where(c=>EF.Property<Guid>(c,"InstitutionId")==t.InstitutionId&&(c.Id==from||c.Id==dto.ToClassId)).Select(c=>new{c.Id,Edition=EF.Property<Guid?>(c,"CourseEditionId")}).ToDictionaryAsync(c=>c.Id,c=>c.Edition,ct);
            if(editions[from] is not {} source || editions[dto.ToClassId] is not {} target)throw Conflict("Entrambe le classi devono essere collegate a un'edizione del corso.");
            var courses=new Dictionary<Guid,E.CourseEdition>();
            foreach(var id in new[]{source,target}.Distinct().OrderBy(x=>x))
            {
                var course=await db.CourseEditions.FromSqlInterpolated($"SELECT * FROM course_edition WHERE institution_id={t.InstitutionId} AND id={id} FOR UPDATE").AsNoTracking().SingleOrDefaultAsync(ct) ?? throw InstitutionSchoolQueries.Missing();
                if(course.Status is not ("planned" or "active"))throw Conflict("Edizione conclusa o annullata.");
                StudentClassTransferPolicy.EnsureAllowed(course.StartDate,today,options.MaxMonthsFromCourseStart);courses[id]=course;
            }
            foreach(var pair in new[]{(from,source),(dto.ToClassId,target)})
                if(!await db.BienniumStudyPaths.AnyAsync(p=>p.Id==classes[pair.Item1].BienniumStudyPathId&&EF.Property<Guid>(p,"InstitutionId")==t.InstitutionId&&p.StudyPathId==courses[pair.Item2].StudyPathId,ct))throw Conflict("Classe ed edizione non compatibili.");
            var enrollments=await db.StudentEnrollments.FromSqlInterpolated($"SELECT * FROM student_enrollment WHERE institution_id={t.InstitutionId} AND student_id={studentId} ORDER BY id FOR UPDATE").AsNoTracking().ToListAsync(ct);
            var enrollment=enrollments.SingleOrDefault(e=>e.CourseEditionId==source);
            if(enrollment is null||enrollment.Status!="enrolled"||enrollment.CompletionDate is not null||enrollment.EnrollmentDate>today)throw Conflict("Iscrizione di provenienza non attiva o incoerente.");
            if(source!=target&&enrollments.Any(e=>e.CourseEditionId==target))throw Conflict("Esiste giÃ  un'iscrizione all'edizione destinazione: occorre un workflow separato.");
            // A current locking read sees START's committed attempt after waiting on Student.
            var active=await db.ExamSubmissions.FromSqlInterpolated($"SELECT * FROM exam_submission WHERE institution_id={t.InstitutionId} AND student_id={studentId} AND status='in_progress' AND submitted_at IS NULL AND deadline_at>{now} FOR UPDATE").AsNoTracking().AnyAsync(ct);
            if(active)throw Conflict("Lo studente ha un tentativo d'esame in corso.");
            var destination=enrollment.Id;
            if(source!=target)
            {
                var oldRevision=await db.StudentEnrollments.Where(e=>e.InstitutionId==t.InstitutionId&&e.Id==enrollment.Id).Select(e=>EF.Property<long>(e,"Revision")).SingleAsync(ct);
                if(oldRevision==long.MaxValue)throw Conflict();
                if(await db.StudentEnrollments.Where(e=>e.InstitutionId==t.InstitutionId&&e.Id==enrollment.Id&&EF.Property<long>(e,"Revision")==oldRevision).ExecuteUpdateAsync(s=>s.SetProperty(e=>e.Status,"withdrawn").SetProperty(e=>e.CompletionDate,(DateOnly?)today).SetProperty(e=>EF.Property<long>(e,"Revision"),oldRevision+1),ct)!=1)throw Conflict();
                await EnrollmentEvent(t,source,enrollment.Id,"enrollment.transition","enrolled","withdrawn",null,today,oldRevision,oldRevision+1,StudentClassTransferPolicy.Reason(dto.Reason),hash,dto.EventId,ct);
                destination=Guid.NewGuid();
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO student_enrollment(id,institution_id,student_id,course_edition_id,enrollment_date,status) VALUES({destination},{t.InstitutionId},{studentId},{target},{today},'enrolled')",ct);
                await EnrollmentEvent(t,target,destination,"enrollment.create",null,"enrolled",null,null,null,0,StudentClassTransferPolicy.Reason(dto.Reason),hash,dto.EventId,ct);
            }
            if(await Students(t).Where(s=>s.Id==studentId&&s.ClassId==from&&EF.Property<long>(s,"TransferRevision")==revision).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.ClassId,(Guid?)dto.ToClassId).SetProperty(x=>EF.Property<long>(x,"TransferRevision"),revision+1),ct)!=1)throw Conflict();
            var ev=new E.StudentClassTransferEvent{Id=dto.EventId,InstitutionId=t.InstitutionId,StudentId=studentId,FromClassId=from,ToClassId=dto.ToClassId,FromCourseEditionId=source,ToCourseEditionId=target,SourceEnrollmentId=enrollment.Id,DestinationEnrollmentId=destination,EffectiveDate=today,Reason=StudentClassTransferPolicy.Reason(dto.Reason),ActorUserId=t.UserId,ActorMembershipId=t.MembershipId,OccurredAtUtc=now,AppliedLimitMonths=options.MaxMonthsFromCourseStart,SourceCourseStartDate=courses[source].StartDate!.Value,DestinationCourseStartDate=courses[target].StartDate!.Value,RevisionBefore=revision,RevisionAfter=revision+1,RequestHash=hash,FromClassName=classes[from].Name,ToClassName=classes[dto.ToClassId].Name,FromCourseName=courses[source].Name,ToCourseName=courses[target].Name};
            db.StudentClassTransferEvents.Add(ev);await db.SaveChangesAsync(ct);receipt=Receipt(ev);
        });
        await transaction.CommitAsync(ct);
        return receipt;
    }
    private Task<int> EnrollmentEvent(TenantContext t,Guid course,Guid enrollment,string action,string? previous,string next,DateOnly? previousDate,DateOnly? date,long? before,long after,string reason,string hash,Guid transferEventId,CancellationToken ct)=>
        db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO course_administration_event(institution_id,id,actor_user_id,actor_membership_id,course_edition_id,enrollment_id,action,previous_status,new_status,previous_completion_date,new_completion_date,revision_before,revision_after,reason,request_hash,before_json,after_json,occurred_at_utc) VALUES({t.InstitutionId},{Guid.NewGuid()},{t.UserId},{t.MembershipId},{course},{enrollment},{action},{previous},{next},{previousDate},{date},{before},{after},{reason},{hash},{JsonSerializer.Serialize(new{TransferEventId=transferEventId,Reason="Trasferimento",Status=previous,CompletionDate=previousDate,Revision=before})},{JsonSerializer.Serialize(new{TransferEventId=transferEventId,Reason="Trasferimento",Status=next,CompletionDate=date,Revision=after})},UTC_TIMESTAMP(6))",ct);
}
