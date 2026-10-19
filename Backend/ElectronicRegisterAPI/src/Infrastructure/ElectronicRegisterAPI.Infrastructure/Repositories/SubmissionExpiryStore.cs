using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using E = ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal sealed class SubmissionExpiryStore(ElectronicRegisterContext db, ITimeProvider time) : ISubmissionExpiryStore
{
    // Deliberately internal cross-tenant scan: keys only, bounded, never used by HTTP managers.
    public async Task<List<ExpiryCandidate>> SelectAsync(DateTime now, ExpiryCandidate? after, int take, CancellationToken ct)
    {
        if (take is < 1 or > 50) throw new ArgumentOutOfRangeException(nameof(take));
        var cutoff = after?.DeadlineAt ?? DateTime.MinValue;
        var key = after?.SubmissionId.ToString() ?? "";
        var rows = await db.ExamSubmissions.FromSqlInterpolated($@"
            SELECT s.* FROM exam_submission s
            JOIN institution i ON i.id=s.institution_id AND i.status IN ('active','suspended')
            JOIN exam e ON e.id=s.exam_id AND e.institution_id=s.institution_id
            JOIN student st ON st.id=s.student_id AND st.institution_id=s.institution_id
            JOIN class_subject cs ON cs.id=e.class_subject_id AND cs.institution_id=s.institution_id
            JOIN exam_student es ON es.exam_id=e.id AND es.student_id=st.id AND es.institution_id=s.institution_id
            WHERE s.status='in_progress' AND s.deadline_at<={now}
            AND NOT EXISTS (SELECT 1 FROM mt3a_legacy_scope m WHERE m.scope='legacy' AND m.institution_id=s.institution_id)
            AND (s.deadline_at>{cutoff} OR (s.deadline_at={cutoff} AND s.id>{key}))
            ORDER BY s.deadline_at,s.id LIMIT {take}")
            .AsNoTracking().OrderBy(s => s.DeadlineAt).ThenBy(s => s.Id)
            .Select(s => new { s.Id, Owner = EF.Property<Guid>(s, "InstitutionId"), s.DeadlineAt }).ToListAsync(ct);
        return rows.Select(s => new ExpiryCandidate(s.Id, s.Owner, DateTime.SpecifyKind(s.DeadlineAt, DateTimeKind.Utc))).ToList();
    }

    public async Task<SubmissionExpiryWork?> ResolveAsync(Guid submissionId, Guid institutionId, CancellationToken ct)
    {
        if (submissionId == Guid.Empty || institutionId == Guid.Empty) return null;
        // Re-resolve, never trust a candidate or tenant supplied by another caller.
        var s = await db.ExamSubmissions.AsNoTracking().Where(s => s.Id == submissionId && EF.Property<Guid>(s, "InstitutionId") == institutionId &&
            db.Institutions.Any(i => i.Id == institutionId && (i.Status == "active" || i.Status == "suspended")))
            .Select(s => new { s.Id, s.ExamId, s.StudentId, s.Exam.ClassSubjectId,
                ExamOwner = EF.Property<Guid>(s.Exam, "InstitutionId"), StudentOwner = EF.Property<Guid>(s.Student, "InstitutionId"),
                ClassSubjectOwner = EF.Property<Guid>(s.Exam.ClassSubject, "InstitutionId") }).SingleOrDefaultAsync(ct);
        if (s is null || s.ExamOwner != institutionId || s.StudentOwner != institutionId || s.ClassSubjectOwner != institutionId) return null;
        // Technical holding cannot acquire this capability, even if its status is changed.
        if (await db.ExamSubmissions.FromSqlInterpolated($"SELECT s.* FROM exam_submission s JOIN mt3a_legacy_scope m ON m.institution_id=s.institution_id AND m.scope='legacy' WHERE s.id={submissionId}").AnyAsync(ct)) return null;
        return new(institutionId, s.Id, s.ExamId, s.StudentId, s.ClassSubjectId);
    }
    public ISubmissionExpiryRepositories Bind(SubmissionExpiryWork work, CancellationToken ct) => new Bound(db, time, work, ct);
    private sealed class Bound : ISubmissionExpiryRepositories
    {
        public IExamSubmissionRepository Submissions { get; }
        public IExamRepository Exams { get; }
        public IGradeRepository Grades { get; }
        public IAuditRepository Audit { get; }
        internal Bound(ElectronicRegisterContext db, ITimeProvider time, SubmissionExpiryWork work, CancellationToken ct)
        {
            var scope = new InstitutionExamDataScope(db, work, ct);
            Submissions = new ExamSubmissionRepository(db, time, scope); Exams = new ExamRepository(db, scope);
            Grades = new GradeRepository(db, scope); Audit = new AuditRepository(db, scope);
        }
    }
}
