using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using E = ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

// Constructed only by the explicit institutional repository factory, never registered
// as an ambient/global repository setting. Legacy repository instances have no scope.
internal sealed class InstitutionExamDataScope
{
    private readonly ElectronicRegisterContext db;
    private readonly ExamPrincipal actor;
    private readonly SubmissionExpiryWork? work;
    private readonly CancellationToken ct;
    internal InstitutionExamDataScope(ElectronicRegisterContext db, ExamPrincipal actor) { this.db = db; this.actor = actor; }
    internal InstitutionExamDataScope(ElectronicRegisterContext db, SubmissionExpiryWork work, CancellationToken ct)
    { this.db = db; this.work = work; this.ct = ct; actor = null!; }
    internal bool Matches(SubmissionExpiryWork value) => ReferenceEquals(work, value);
    internal bool IsTechnical => work is not null;
    internal Guid InstitutionId => work?.InstitutionId ?? actor.Tenant?.InstitutionId ?? throw new UnauthorizedAccessException();
    internal bool Matches(ExamPrincipal principal) => work is null && principal.Tenant is { } t && actor.Tenant is { } bound &&
        t.UserId == bound.UserId && t.InstitutionId == bound.InstitutionId && t.MembershipId == bound.MembershipId && t.Roles.SetEquals(bound.Roles) &&
        principal.StudentId == actor.StudentId && principal.TeacherId == actor.TeacherId && principal.StudentView == actor.StudentView && principal.CombinedRead == actor.CombinedRead;
    internal IQueryable<T> Owned<T>() where T : class => db.Set<T>().Where(e => EF.Property<Guid>(e, "InstitutionId") == InstitutionId);
    internal IQueryable<E.ClassSubject> Assignments => actor.IsAdmin ? Owned<E.ClassSubject>() : actor.IsTeacher
        ? Owned<E.ClassSubject>().Where(cs => cs.TeacherId == actor.TeacherId || actor.CombinedRead && Owned<E.Student>().Any(s => s.Id == actor.StudentId && s.ClassId == cs.ClassId))
        : Owned<E.ClassSubject>().Where(cs => Owned<E.Student>().Any(s => s.Id == actor.StudentId && s.ClassId == cs.ClassId) ||
            Owned<E.Grade>().Any(g=>g.StudentId==actor.StudentId && g.ClassSubjectId==cs.Id) ||
            Owned<E.ExamSubmission>().Any(sub=>sub.StudentId==actor.StudentId && Owned<E.Exam>().Any(e=>e.Id==sub.ExamId && e.ClassSubjectId==cs.Id)));
    internal IQueryable<E.Exam> Exams => work is not null ? Owned<E.Exam>().Where(e => e.Id == work.ExamId && e.ClassSubjectId == work.ClassSubjectId &&
        Owned<E.ClassSubject>().Any(cs => cs.Id == e.ClassSubjectId) && Owned<E.ExamStudent>().Any(es => es.ExamId == e.Id && es.StudentId == work.StudentId)) : actor.IsStudent
        ? Owned<E.Exam>().Where(e => e.Status != "draft" && Owned<E.ExamStudent>().Any(es => es.ExamId == e.Id && es.StudentId == actor.StudentId) &&
            (Owned<E.ClassSubject>().Any(cs=>cs.Id==e.ClassSubjectId && Owned<E.Student>().Any(st=>st.Id==actor.StudentId && st.ClassId==cs.ClassId)) || Owned<E.ExamSubmission>().Any(sub=>sub.ExamId==e.Id && sub.StudentId==actor.StudentId)))
        : Owned<E.Exam>().Where(e => Owned<E.ClassSubject>().Any(cs => cs.Id == e.ClassSubjectId && (actor.IsAdmin || cs.TeacherId == actor.TeacherId)));
    internal IQueryable<E.ExamSubmission> Submissions => work is not null ? Owned<E.ExamSubmission>().Where(s => s.Id == work.SubmissionId && s.ExamId == work.ExamId && s.StudentId == work.StudentId &&
        Exams.Any(e => e.Id == s.ExamId) && Owned<E.Student>().Any(st => st.Id == s.StudentId)) : actor.IsStudent
        ? Owned<E.ExamSubmission>().Where(s => s.StudentId == actor.StudentId && Exams.Any(e => e.Id == s.ExamId))
        : Owned<E.ExamSubmission>().Where(s => Exams.Any(e => e.Id == s.ExamId));
    internal IQueryable<E.Grade> CurrentCareerGrades(IQueryable<E.Grade> grades) => grades.Where(g=>
        !Owned<E.StudentClassTransferEvent>().Any(e=>e.StudentId==g.StudentId && e.FromCourseEditionId!=e.ToCourseEditionId) ||
        Owned<E.Student>().Any(st=>st.Id==g.StudentId &&
        Owned<E.Class>().Any(current=>current.Id==st.ClassId && Owned<E.ClassSubject>().Any(cs=>cs.Id==g.ClassSubjectId &&
            Owned<E.Class>().Any(origin=>origin.Id==cs.ClassId && (origin.Id==current.Id ||
                EF.Property<Guid?>(current,"CourseEditionId")!=null && EF.Property<Guid?>(origin,"CourseEditionId")==EF.Property<Guid?>(current,"CourseEditionId")))))));
    internal IQueryable<T> Query<T>() where T : class
    {
        if (work is not null)
        {
            object technical = typeof(T) == typeof(E.Exam) ? Exams : typeof(T) == typeof(E.ExamSubmission) ? Submissions :
                typeof(T) == typeof(E.Question) ? Owned<E.Question>().Where(q => q.ExamId == work.ExamId) :
                typeof(T) == typeof(E.QuestionOption) ? Owned<E.QuestionOption>().Where(o => Owned<E.Question>().Any(q => q.Id == o.QuestionId && q.ExamId == work.ExamId)) :
                typeof(T) == typeof(E.SubmissionAnswer) ? Owned<E.SubmissionAnswer>().Where(a => a.SubmissionId == work.SubmissionId) :
                typeof(T) == typeof(E.Grade) ? Owned<E.Grade>().Where(g => g.StudentId == work.StudentId && g.ClassSubjectId == work.ClassSubjectId && Submissions.Any(s => s.GradeId == g.Id)) :
                throw new UnauthorizedAccessException("Lettura non consentita al contesto tecnico.");
            return (IQueryable<T>)technical;
        }
        object query = typeof(T) == typeof(E.Exam) ? Exams : typeof(T) == typeof(E.ClassSubject) ? Assignments :
            typeof(T) == typeof(E.ExamSubmission) ? Submissions :
            typeof(T) == typeof(E.Grade) ? Owned<E.Grade>().Where(g => actor.IsStudent ? g.StudentId == actor.StudentId :
                actor.CombinedRead && g.StudentId == actor.StudentId || Owned<E.ClassSubject>().Any(cs => cs.Id == g.ClassSubjectId && (actor.IsAdmin || cs.TeacherId == actor.TeacherId))) :
            typeof(T) == typeof(E.Student) ? Owned<E.Student>().Where(s => actor.IsAdmin || (actor.IsStudent ? s.Id == actor.StudentId :
                actor.CombinedRead && s.Id == actor.StudentId || Owned<E.ClassSubject>().Any(cs => cs.ClassId == s.ClassId && cs.TeacherId == actor.TeacherId) ||
                Owned<E.Grade>().Any(g=>g.StudentId==s.Id && Owned<E.ClassSubject>().Any(cs=>cs.Id==g.ClassSubjectId && cs.TeacherId==actor.TeacherId)) ||
                Owned<E.ExamSubmission>().Any(sub=>sub.StudentId==s.Id && Owned<E.Exam>().Any(e=>e.Id==sub.ExamId && Owned<E.ClassSubject>().Any(cs=>cs.Id==e.ClassSubjectId && cs.TeacherId==actor.TeacherId))))) :
            typeof(T) == typeof(E.Question) ? Owned<E.Question>().Where(q => Exams.Any(e => e.Id == q.ExamId)) :
            typeof(T) == typeof(E.QuestionOption) ? Owned<E.QuestionOption>().Where(o => Owned<E.Question>().Any(q => q.Id == o.QuestionId && Exams.Any(e => e.Id == q.ExamId))) :
            typeof(T) == typeof(E.ExamStudent) ? Owned<E.ExamStudent>().Where(es => Exams.Any(e => e.Id == es.ExamId) && (!actor.IsStudent || es.StudentId == actor.StudentId)) :
            typeof(T) == typeof(E.SubmissionAnswer) ? Owned<E.SubmissionAnswer>().Where(a => Submissions.Any(s => s.Id == a.SubmissionId)) :
            Owned<T>();
        return (IQueryable<T>)query;
    }
    // The first statement is a locking read of exactly one tenant-owned parent.
    // Permission joins are evaluated AFTER acquisition, so a waiting Start/Close or
    // Finalize cannot establish a stale REPEATABLE READ snapshot before the lock.
    internal async Task LockStudentBeforeStartAsync()
    {
        if(work is not null || !actor.IsStudent)return;
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM institution WHERE id={InstitutionId} LOCK IN SHARE MODE");
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM student WHERE institution_id={InstitutionId} AND id={actor.StudentId} FOR UPDATE");
    }
    internal IQueryable<E.Exam> LockExam(Guid id) => work is not null ? throw new UnauthorizedAccessException("Il worker non blocca Exam.") : db.Exams.FromSqlInterpolated($"SELECT * FROM exam WHERE id={id} AND institution_id={InstitutionId} FOR UPDATE");
    // Current ownership/teacher predicate after waiting for an administrative reassignment.
    internal Task LockExamCreationTenantAsync() => work is not null
        ? throw new UnauthorizedAccessException("Operazione non consentita al worker.")
        : db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM institution WHERE id={InstitutionId} FOR UPDATE");
    internal IQueryable<E.ClassSubject> LockAssignmentForExamWrite(Guid id) => work is not null
        ? throw new UnauthorizedAccessException("Operazione non consentita al worker.")
        : db.ClassSubjects.FromSqlInterpolated($"SELECT * FROM class_subject WHERE id={id} AND institution_id={InstitutionId} AND ({actor.IsAdmin}=1 OR teacher_id={actor.TeacherId}) FOR UPDATE");
    internal IQueryable<E.ExamSubmission> LockSubmission(Guid id) => work is not null
        ? db.ExamSubmissions.FromSqlInterpolated($"SELECT * FROM exam_submission WHERE id={id} AND id={work.SubmissionId} AND institution_id={InstitutionId} AND student_id={work.StudentId} AND exam_id={work.ExamId} FOR UPDATE")
        : db.ExamSubmissions.FromSqlInterpolated($"SELECT * FROM exam_submission WHERE id={id} AND institution_id={InstitutionId} AND ({!actor.IsStudent}=1 OR student_id={actor.StudentId}) FOR UPDATE");
    internal async Task SaveAsync()
    {
        ct.ThrowIfCancellationRequested();
        db.ChangeTracker.DetectChanges();
        foreach (var entry in db.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray())
        {
            if (work is not null && !(entry.Entity is E.ExamSubmission s && s.Id == work.SubmissionId && s.ExamId == work.ExamId && s.StudentId == work.StudentId && entry.State == EntityState.Modified ||
                entry.Entity is E.Grade g && g.StudentId == work.StudentId && g.ClassSubjectId == work.ClassSubjectId && entry.State == EntityState.Added ||
                entry.Entity is E.SubmissionAnswer answer && answer.SubmissionId == work.SubmissionId && entry.State == EntityState.Modified &&
                    entry.Properties.Where(p => p.IsModified).All(p => p.Metadata.Name is "AwardedPoints" or "IsCorrect" or "TeacherComment" or "LastSavedAt") ||
                entry.Entity is E.AuditEvent a && a.EntityId == work.SubmissionId && a.EntityType == "exam_submission" && a.Action == "submission.submit" && a.ActorUserId is null && entry.State == EntityState.Added))
                throw new UnauthorizedAccessException("Scrittura non consentita al contesto tecnico.");
            if (work is not null && entry.Entity is E.ExamSubmission && entry.Properties.Where(p => p.IsModified).Any(p =>
                p.Metadata.Name is "ExamId" or "StudentId" or "StartedAt" or "DeadlineAt" or "LastClientRevision" or "ViolationCount" or "ReviewedByUserId" or "FinalizedAt" or "OverrideReason"))
                throw new UnauthorizedAccessException("Il contesto tecnico non modifica risposte, revision, scadenza o revisione docente.");
            if (entry.Entity is E.ExamSubmission { GradeId: { } grade } submission && entry.State != EntityState.Deleted &&
                !await Owned<E.Grade>().AnyAsync(g => g.Id == grade && g.StudentId == submission.StudentId &&
                    Owned<E.Exam>().Any(e => e.Id == submission.ExamId && e.ClassSubjectId == g.ClassSubjectId)))
                throw new Domain.Exceptions.BusinessRuleException("Grade ufficiale incoerente con la submission.");
            var property = entry.Metadata.FindProperty("InstitutionId");
            if (property is null) throw new InvalidOperationException("Unowned write in institutional unit of work.");
            var owner = entry.Property("InstitutionId");
            if (entry.State == EntityState.Added) owner.CurrentValue = InstitutionId;
            else if (owner.OriginalValue is not Guid value || value != InstitutionId || !Equals(owner.CurrentValue, owner.OriginalValue))
                throw new UnauthorizedAccessException("Scrittura fuori ambito.");
        }
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new Domain.Exceptions.BusinessRuleException("La risorsa e stata modificata."); }
    }
}
