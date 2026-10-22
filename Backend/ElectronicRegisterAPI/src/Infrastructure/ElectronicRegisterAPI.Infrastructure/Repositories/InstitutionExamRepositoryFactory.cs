using System.Text;
using System.Text.Json;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Mappers;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using E = ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal sealed class InstitutionExamRepositoryFactory(ElectronicRegisterContext db, ITimeProvider time,
    Domain.Interfaces.Security.ICurrentTenant? current = null) : IInstitutionExamRepositoryFactory
{
    public IInstitutionExamRepositories Create(ExamPrincipal principal)
    {
        if (principal.Tenant is null) throw new UnauthorizedAccessException("Contesto istituzionale richiesto.");
        var verified = current?.Value ?? throw new UnauthorizedAccessException("Contesto verificato assente.");
        if (verified.UserId != principal.Tenant.UserId || verified.InstitutionId != principal.Tenant.InstitutionId ||
            verified.MembershipId != principal.Tenant.MembershipId || !verified.Roles.SetEquals(principal.Tenant.Roles))
            throw new UnauthorizedAccessException("Contesto non corrispondente.");
        return new BoundRepositories(db, time, principal);
    }
    private sealed class BoundRepositories : IInstitutionExamRepositories
    {
        private readonly ElectronicRegisterContext _db;
        private readonly ITimeProvider _time;
        private readonly ExamPrincipal _actor;
        private readonly InstitutionExamDataScope _scope;
        public IExamRepository Exams { get; }
        public IExamSubmissionRepository Submissions { get; }
        public IGradeRepository Grades { get; }
        public IStudentRepository Students { get; }
        public ISubjectRepository Subjects { get; }
        public IClassSubjectRepository ClassSubjects { get; }
        public IAuditRepository Audit { get; }
        internal BoundRepositories(ElectronicRegisterContext db, ITimeProvider time, ExamPrincipal actor)
        {
            _db = db; _time = time; _actor = actor; _scope = new(db, actor);
            Exams = new ExamRepository(db, _scope); Submissions = new ExamSubmissionRepository(db, time, _scope);
            Grades = new GradeRepository(db, _scope); Students = new StudentRepository(db, _scope);
            Subjects = new SubjectRepository(db, _scope); ClassSubjects = new ClassSubjectRepository(db, _scope); Audit = new AuditRepository(db, _scope);
        }
        public Task<List<ClassSubjectChoiceDto>> ClassSubjectChoicesAsync()
        {
            if (_actor.IsStudent) throw new UnauthorizedAccessException();
            return _scope.Assignments.AsNoTracking().Select(cs => new ClassSubjectChoiceDto(cs.Id, cs.ClassId, cs.Class.Name,
                cs.SubjectId, cs.Subject.Name, cs.TeacherId, cs.Teacher.FirstName, cs.Teacher.LastName)).ToListAsync();
        }
        public async Task<List<StudentDto>> StudentsForClassAsync(Guid classId)
        {
            if (_actor.IsStudent) throw new UnauthorizedAccessException();
            if (!await _scope.Owned<E.Class>().AnyAsync(c => c.Id == classId && (_actor.IsAdmin || _scope.Assignments.Any(cs => cs.ClassId == c.Id))))
                throw new KeyNotFoundException("La risorsa non esiste.");
            return await _scope.Query<E.Student>().AsNoTracking().Where(s => s.ClassId == classId)
                .Select(s => new StudentDto { Id = s.Id, FirstName = s.FirstName, LastName = s.LastName }).ToListAsync();
        }
        public async Task<SubmissionSessionDto> SessionAsync(Guid submissionId)
        {
            if (!_actor.IsStudent) throw new UnauthorizedAccessException();
            var submission = await Submissions.GetByIdAsync(submissionId, true) ?? throw new KeyNotFoundException("La risorsa non esiste.");
            var exam = await Exams.GetByIdAsync(submission.ExamId, true) ?? throw new KeyNotFoundException("La risorsa non esiste.");
            return new()
            {
                SubmissionId = submission.Id, ExamId = exam.Id, Status = submission.Status, StartedAt = DateTime.SpecifyKind(submission.StartedAt, DateTimeKind.Utc),
                DeadlineAt = DateTime.SpecifyKind(submission.DeadlineAt, DateTimeKind.Utc), SubmittedAt = submission.SubmittedAt is { } submitted ? DateTime.SpecifyKind(submitted, DateTimeKind.Utc) : null, CurrentRevision = submission.Revision,
                Answers = submission.Answers.Select(a => new AnswerInputDto { QuestionId = a.QuestionId, OpenText = a.OpenText, SelectedOptionIds = a.SelectedOptionIds }).ToList(),
                Exam = ElectronicRegisterAPI.Domain.Models.StudentExamPresentation.Build(exam, submission.DisplayOrderJson)
            };
        }
        public async Task<ViolationReceiptDto> ReportViolationAsync(Guid submissionId, InstitutionViolationDto dto)
        {
            if (!_actor.IsStudent) throw new UnauthorizedAccessException();
            if (dto.EventId == Guid.Empty || dto.Type is not ("tab_hidden" or "window_blur" or "fullscreen_exit")) throw new ArgumentException("Evento non valido.");
            if (dto.Metadata is not null)
            {
                if (Encoding.UTF8.GetByteCount(dto.Metadata) > 1024) throw new ArgumentException("Metadata eccessivi.");
                try { using var json = JsonDocument.Parse(dto.Metadata, new JsonDocumentOptions { MaxDepth = 4 }); if (json.RootElement.ValueKind != JsonValueKind.Object) throw new ArgumentException("Metadata non validi."); }
                catch (JsonException) { throw new ArgumentException("Metadata non validi."); }
            }
            if (_db.Database.CurrentTransaction is null) throw new InvalidOperationException("Violation requires an ambient transaction.");
            var submission = await _scope.LockSubmission(submissionId).AsTracking().SingleOrDefaultAsync() ?? throw new KeyNotFoundException("La risorsa non esiste.");
            var events = _scope.Owned<E.ExamViolation>();
            var existing = await events.AsNoTracking().SingleOrDefaultAsync(v => v.Id == dto.EventId);
            if (existing is not null)
            {
                if (existing.SubmissionId != submissionId || existing.Type != dto.Type || existing.MetadataJson != dto.Metadata) throw new BusinessRuleException("EventId gia utilizzato.");
                return new(dto.EventId, submission.ViolationCount, true);
            }
            var now = _time.UtcNow;
            if (submission.Status != "in_progress" || submission.SubmittedAt is not null || submission.DeadlineAt <= now)
                throw new BusinessRuleException("La submission non accetta nuovi eventi.");
            if (submission.ViolationCount >= 128 || await events.CountAsync(v => v.SubmissionId == submissionId && v.OccurredAt >= now.AddSeconds(-10)) >= 5)
                throw new BusinessRuleException("Limite eventi raggiunto.");
            _db.ExamViolations.Add(new() { Id = dto.EventId, SubmissionId = submissionId, Type = dto.Type, MetadataJson = dto.Metadata, OccurredAt = now });
            submission.ViolationCount++;
            try { await _scope.SaveAsync(); }
            catch (DbUpdateException e) when (e.InnerException is MySqlException { Number: 1062 }) { throw new BusinessRuleException("EventId gia utilizzato."); }
            return new(dto.EventId, submission.ViolationCount, false);
        }
        public async Task<List<ViolationEventDto>> ViolationsAsync(Guid submissionId)
        {
            if (_actor.IsStudent) throw new UnauthorizedAccessException();
            if (!await _scope.Submissions.AnyAsync(s => s.Id == submissionId)) throw new KeyNotFoundException("La risorsa non esiste.");
            return await _scope.Owned<E.ExamViolation>().AsNoTracking().Where(v => v.SubmissionId == submissionId).OrderBy(v => v.OccurredAt)
                .Select(v => new ViolationEventDto(v.Id, v.Type, DbUtc.Read(v.OccurredAt), v.MetadataJson)).Take(128).ToListAsync();
        }
    }
}
