using ElectronicRegisterAPI.Infrastructure.Repositories;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Mappers;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ExamEntity = ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Exam;
using ExamStudentEntity = ElectronicRegisterAPI.Infrastructure.Persistence.Entities.ExamStudent;
using QuestionEntity = ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Question;
using QuestionOptionEntity = ElectronicRegisterAPI.Infrastructure.Persistence.Entities.QuestionOption;

namespace ElectronicRegisterAPI.Infrastructure.Repositories
{
    internal class ExamRepository : IExamRepository
    {
        private readonly ElectronicRegisterContext _context;
    private readonly InstitutionExamDataScope? _scope;
    private IQueryable<T> Rows<T>() where T : class => _scope?.Query<T>() ?? _context.Set<T>();
    private Task SaveAsync() => _scope is null ? _context.SaveChangesAsync() : _scope.SaveAsync();

        public ExamRepository(ElectronicRegisterContext context, InstitutionExamDataScope? scope = null)
        {
            _context = context;
        _scope = scope;
        }

        public async Task<Exam?> GetByIdAsync( Guid id, bool includeStructure = false, bool includeStudents = false)
        {
            IQueryable<ExamEntity> query = Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Exam>().Include(e => e.ClassSubject).ThenInclude(cs => cs.Subject).AsNoTracking();

            if (includeStructure)
            {
                query = query
                    .Include(e => e.Questions)
                        .ThenInclude(q => q.QuestionOptions);
            }

            if (includeStudents)
            {
                query = query
                    .Include(e => e.ExamStudents);
            }

            var entity = await query.FirstOrDefaultAsync(e => e.Id == id);

            return entity is null ? null : MapToModel(entity);
        }

        public async Task<Exam?> GetByIdForUpdateAsync(Guid id)
        {
            if (_context.Database.CurrentTransaction is null)
                throw new InvalidOperationException("Exam row lock requires an ambient transaction.");
            if (_scope is not null)
            {
                await _scope.LockStudentBeforeStartAsync();
                if (await _scope.LockExam(id).AsTracking().FirstOrDefaultAsync() is null) return null;
                var scoped = await _scope.Exams.AsTracking().Include(e => e.ClassSubject).ThenInclude(cs => cs.Subject).Where(e => e.Id == id)
                    .Include(e => e.Questions).ThenInclude(q => q.QuestionOptions).Include(e => e.ExamStudents).FirstOrDefaultAsync();
                return scoped is null ? null : MapToModel(scoped);
            }
            var entity = await (_scope is null ? _context.Exams.FromSqlRaw("SELECT * FROM exam WHERE id = {0} FOR UPDATE", id) : _scope.LockExam(id))
                .AsTracking().Include(e => e.ClassSubject).ThenInclude(cs => cs.Subject).Include(e => e.Questions).ThenInclude(q => q.QuestionOptions)
                .Include(e => e.ExamStudents).FirstOrDefaultAsync();
            return entity is null ? null : MapToModel(entity);
        }

        public Task<bool> HasRetakesAsync(Guid examId) =>
            Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Exam>().AnyAsync(e => e.RetakeOfExamId == examId);
        public async Task<List<Exam>> GetAllAsync(Guid? teacherId = null)
        {
            var query = Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Exam>()
                .Include(e => e.ClassSubject).ThenInclude(cs => cs.Subject).AsNoTracking()
                .AsQueryable();

            if (teacherId.HasValue)
            {
                query = query.Where(e =>
                    e.ClassSubject.TeacherId == teacherId.Value);
            }

            return await query
                .OrderByDescending(e => e.AvailableFrom)
                .Select(e => new Exam
                {
                    Id = e.Id,
                    ClassSubjectId = e.ClassSubjectId,
                    RetakeOfExamId = e.RetakeOfExamId,
                    Name = e.ClassSubject.Subject.Name,
                    Description = e.Description,
                    AvailableFrom = e.AvailableFrom,
                    AvailableUntil = e.AvailableUntil,
                    DurationMinutes = e.DurationMinutes,
                    PassingPercent = e.PassingPercent,
                    TotalPoints = e.TotalPoints,
                    Status = ExamStatusMapper.FromDbString(e.Status),
                    PublishedAt = e.PublishedAt,
                    ClosedAt = e.ClosedAt,
                    CancelledAt = e.CancelledAt
                })
                .ToListAsync();
        }
        public async Task<List<ExamSummaryDto>> GetSummariesAsync(Guid? teacherId = null)
        {
            var query = Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Exam>().Include(e => e.ClassSubject).ThenInclude(cs => cs.Subject).AsNoTracking();

            if (teacherId.HasValue)
                query = query.Where(e => e.ClassSubject.TeacherId == teacherId.Value);

            var rows = await query
                .OrderByDescending(e => e.AvailableFrom)
                .Select(e => new
                {
                    e.Id,
                    Name = e.ClassSubject.Subject.Name,
                    e.Description,
                    e.ClassSubjectId,
                    e.RetakeOfExamId,
                    e.AvailableFrom,
                    e.AvailableUntil,
                    e.DurationMinutes,
                    e.PassingPercent,
                    e.TotalPoints,
                    e.Status,
                    AssignedStudentsCount = e.ExamStudents.Count,
                    e.PublishedAt,
                    e.ClosedAt,
                    e.CancelledAt
                })
                .ToListAsync();

            return rows.Select(r => new ExamSummaryDto
            {
                Id = r.Id,
                Name = r.Name,
                Description = r.Description,
                ClassSubjectId = r.ClassSubjectId,
                RetakeOfExamId = r.RetakeOfExamId,
                AvailableFrom = DbUtc.Read(r.AvailableFrom),
                AvailableUntil = DbUtc.Read(r.AvailableUntil),
                DurationMinutes = r.DurationMinutes,
                PassingPercent = r.PassingPercent,
                TotalPoints = r.TotalPoints,
                Status = ExamStatusMapper.FromDbString(r.Status),
                AssignedStudentsCount = r.AssignedStudentsCount,
                PublishedAt = DbUtc.Read(r.PublishedAt),
                ClosedAt = DbUtc.Read(r.ClosedAt),
                CancelledAt = DbUtc.Read(r.CancelledAt)
            }).ToList();
        }

        public async Task<List<Exam>> GetAssignedToStudentAsync(Guid studentId)
        {
            var entities = await Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Exam>()
                .Include(e => e.ClassSubject).ThenInclude(cs => cs.Subject).AsNoTracking()
                .Where(e =>
                    e.ExamStudents.Any(es => es.StudentId == studentId))
                .Where(e => e.Status != "draft")
                .Include(e => e.ExamStudents
                    .Where(es => es.StudentId == studentId))
                .OrderByDescending(e => e.AvailableFrom)
                .ToListAsync();

            return entities.Select(MapToModel).ToList();
        }

        public async Task<List<Exam>> GetByClassSubjectAsync(Guid classSubjectId)
        {
            var entities = await Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Exam>()
                .Include(e => e.ClassSubject).ThenInclude(cs => cs.Subject).AsNoTracking()
                .Where(e => e.ClassSubjectId == classSubjectId)
                .OrderByDescending(e => e.AvailableFrom)
                .ToListAsync();

            return entities.Select(MapToModel).ToList();
        }
        public Task<bool> HasSubmissionsAsync(Guid examId)
        {
            return Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.ExamSubmission>().AnyAsync(s => s.ExamId == examId);
        }
        public Task<bool> HasInProgressSubmissionsAsync(Guid examId)
        {
            if (_context.Database.CurrentTransaction is null)
                throw new InvalidOperationException("Submission check requires an ambient transaction and the Exam row lock.");

            // Close acquires the Exam lock before any consistent read establishes a snapshot.
            // All earlier Starts have committed; later Starts wait for Close to commit/rollback.
            // Do not lock submissions: Submit/worker own those locks and can read exam structure.
            return Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.ExamSubmission>().AnyAsync(s => s.ExamId == examId && s.Status == "in_progress");
        }
        private async Task DeriveNameForWriteAsync(Exam exam)
        {
            var subjectId = await Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.ClassSubject>()
                .Where(cs => cs.Id == exam.ClassSubjectId).Select(cs => cs.SubjectId).SingleAsync();
            // A current locking read serializes with subject renames, even under REPEATABLE READ.
            // ClassSubject was resolved through the caller's authorized scope before this lock.
            var subject = await _context.Subjects.FromSqlInterpolated(
                $"SELECT * FROM subject WHERE id = {subjectId} FOR UPDATE").AsNoTracking().SingleAsync();
            exam.Name = subject.Name;
        }

        public async Task AddAsync(Exam exam)
        {
            // Creation and administrative reassignment share the tenant-first order.
            // Existing exams already prevent reassignment, so updates need no new lock.
            if (_scope is not null)
            {
                await _scope.LockExamCreationTenantAsync();
                if (await _scope.LockAssignmentForExamWrite(exam.ClassSubjectId).AsNoTracking().SingleOrDefaultAsync() is null)
                    throw new KeyNotFoundException("Assegnazione non disponibile.");
            }
            await DeriveNameForWriteAsync(exam);
            var entity = new ExamEntity
            {
                Id = exam.Id,
                ClassSubjectId = exam.ClassSubjectId,
                RetakeOfExamId = exam.RetakeOfExamId,
                Name = exam.Name,
                Description = exam.Description,
                AvailableFrom = exam.AvailableFrom,
                AvailableUntil = exam.AvailableUntil,
                DurationMinutes = exam.DurationMinutes,
                PassingPercent = exam.PassingPercent,
                TotalPoints = exam.TotalPoints,
                Status = ExamStatusMapper.ToDbString(exam.Status),
                PublishedAt = exam.PublishedAt,
                ClosedAt = exam.ClosedAt,
                CancelledAt = exam.CancelledAt
            };

            foreach (var question in exam.Questions)
            {
                var questionEntity = new QuestionEntity
                {
                    Id = question.Id,
                    ExamId = exam.Id,
                    Position = question.Position,
                    Text = question.Text,
                    Type = QuestionTypeMapper.ToDbString(question.Type),
                    Points = question.Points,
                    Required = question.Required
                };

                foreach (var option in question.Options)
                {
                    questionEntity.QuestionOptions.Add(
                        new QuestionOptionEntity
                        {
                            Id = option.Id,
                            QuestionId = question.Id,
                            Position = option.Position,
                            Text = option.Text,
                            IsCorrect = option.IsCorrect
                        }
                    );
                }

                entity.Questions.Add(questionEntity);
            }

            foreach (var assignedStudent in exam.AssignedStudents)
            {
                entity.ExamStudents.Add(
                    new ExamStudentEntity
                    {
                        ExamId = exam.Id,
                        StudentId = assignedStudent.StudentId,
                        ExtraTimeMinutes = assignedStudent.ExtraTimeMinutes,
                        AssignedAt = assignedStudent.AssignedAt
                    }
                );
            }

            await _context.Exams.AddAsync(entity);

            await SaveAsync();
        }
        public async Task UpdateAsync(Exam exam)
        {
            if (exam.Status == Domain.Enums.ExamStatus.Cancelled) _scope?.RequireExamCancel();
            await DeriveNameForWriteAsync(exam);
            var entity = await Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Exam>().FirstOrDefaultAsync(e => e.Id == exam.Id);

            if (entity is null) return;

            entity.ClassSubjectId = exam.ClassSubjectId;
            entity.RetakeOfExamId = exam.RetakeOfExamId;
            entity.Name = exam.Name;
            entity.Description = exam.Description;
            entity.AvailableFrom = exam.AvailableFrom;
            entity.AvailableUntil = exam.AvailableUntil;
            entity.DurationMinutes = exam.DurationMinutes;
            entity.PassingPercent = exam.PassingPercent;
            entity.TotalPoints = exam.TotalPoints;
            entity.Status = ExamStatusMapper.ToDbString(exam.Status);
            entity.PublishedAt = exam.PublishedAt;
            entity.ClosedAt = exam.ClosedAt;
            entity.CancelledAt = exam.CancelledAt;

            await SaveAsync();
        }
        public async Task ReplaceStructureAsync(Exam exam)
        {
            if (_scope is not null) await _scope.RequireDraftReplacementAsync(exam.Id);
            var entity = await Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Exam>()
                .Include(e => e.Questions)
                    .ThenInclude(q => q.QuestionOptions)
                .FirstOrDefaultAsync(e => e.Id == exam.Id);

            if (entity is null) return;

            _context.Questions.RemoveRange(entity.Questions);

            entity.Questions.Clear();
            // Release unique (exam_id, position) keys before inserting the replacement.
            // The application owns the transaction across this save and the following saves.
            if (_context.Database.CurrentTransaction is null)
                throw new InvalidOperationException("Structure replacement requires an ambient transaction.");
            await SaveAsync();

            foreach (var question in exam.Questions)
            {
                var questionEntity = new QuestionEntity
                {
                    Id = question.Id,
                    ExamId = exam.Id,
                    Position = question.Position,
                    Text = question.Text,
                    Type = QuestionTypeMapper.ToDbString(question.Type),
                    Points = question.Points,
                    Required = question.Required
                };

                foreach (var option in question.Options)
                {
                    questionEntity.QuestionOptions.Add(
                        new QuestionOptionEntity
                        {
                            Id = option.Id,
                            QuestionId = question.Id,
                            Position = option.Position,
                            Text = option.Text,
                            IsCorrect = option.IsCorrect
                        });
                }

                _context.Questions.Add(questionEntity);
                entity.Questions.Add(questionEntity);
            }

            entity.TotalPoints = exam.TotalPoints;

            await SaveAsync();
        }
        public async Task ReplaceAssignedStudentsAsync(Guid examId, IReadOnlyCollection<ExamStudent> students)
        {
            if (_scope is not null) await _scope.RequireDraftReplacementAsync(examId);
            var currentStudents = await Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.ExamStudent>()
                .Where(es => es.ExamId == examId)
                .ToListAsync();

            _context.ExamStudents.RemoveRange(currentStudents);

            var newEntities = students.Select(student =>
                new ExamStudentEntity
                {
                    ExamId = examId,
                    StudentId = student.StudentId,
                    ExtraTimeMinutes =
                        student.ExtraTimeMinutes,
                    AssignedAt =
                        student.AssignedAt
                });

            await _context.ExamStudents.AddRangeAsync(newEntities);

            await SaveAsync();
        }
        public async Task DeleteDraftAsync(Guid examId)
        {
            _scope?.RequireExamDelete();
            var entity = await Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Exam>()
                .FirstOrDefaultAsync(e => e.Id == examId);

            if (entity is null) return;

            _context.Exams.Remove(entity);

            await SaveAsync();
        }

        private static Exam MapToModel(ExamEntity entity)
        {
            return new Exam
            {
                Id = entity.Id,
                ClassSubjectId = entity.ClassSubjectId,
                RetakeOfExamId = entity.RetakeOfExamId,
                Name = entity.ClassSubject.Subject.Name,
                Description = entity.Description,
                AvailableFrom = DbUtc.Read(entity.AvailableFrom),
                AvailableUntil = DbUtc.Read(entity.AvailableUntil),
                DurationMinutes = entity.DurationMinutes,
                PassingPercent = entity.PassingPercent,
                TotalPoints = entity.TotalPoints,
                Status = ExamStatusMapper.FromDbString(entity.Status),
                PublishedAt = DbUtc.Read(entity.PublishedAt),
                ClosedAt = DbUtc.Read(entity.ClosedAt),
                CancelledAt = DbUtc.Read(entity.CancelledAt),
                Questions = entity.Questions
                    .OrderBy(q => q.Position)
                    .Select(q => new Question
                    {
                        Id = q.Id,
                        ExamId = q.ExamId,
                        Position = q.Position,
                        Text = q.Text,
                        Type = QuestionTypeMapper.FromDbString(q.Type),
                        Points = q.Points,
                        Required = q.Required,
                        Options = q.QuestionOptions
                            .OrderBy(o => o.Position)
                            .Select(o => new QuestionOption
                            {
                                Id = o.Id,
                                QuestionId = o.QuestionId,
                                Position = o.Position,
                                Text = o.Text,
                                IsCorrect = o.IsCorrect
                            }).ToList()
                    }).ToList(),

                AssignedStudents = entity.ExamStudents
                    .Select(es => new ExamStudent
                    {
                        ExamId = es.ExamId,
                        StudentId = es.StudentId,
                        ExtraTimeMinutes = es.ExtraTimeMinutes,
                        AssignedAt = es.AssignedAt
                    }).ToList()
            };
        }
    }
}
