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

        public ExamRepository(ElectronicRegisterContext context)
        {
            _context = context;
        }

        public async Task<Exam?> GetByIdAsync( Guid id, bool includeStructure = false, bool includeStudents = false)
        {
            IQueryable<ExamEntity> query = _context.Exams.AsNoTracking();

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
        public async Task<List<Exam>> GetAllAsync(Guid? teacherId = null)
        {
            var query = _context.Exams
                .AsNoTracking()
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
                    Name = e.Name,
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
            var query = _context.Exams.AsNoTracking();

            if (teacherId.HasValue)
                query = query.Where(e => e.ClassSubject.TeacherId == teacherId.Value);

            var rows = await query
                .OrderByDescending(e => e.AvailableFrom)
                .Select(e => new
                {
                    e.Id,
                    e.Name,
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
                AvailableFrom = r.AvailableFrom,
                AvailableUntil = r.AvailableUntil,
                DurationMinutes = r.DurationMinutes,
                PassingPercent = r.PassingPercent,
                TotalPoints = r.TotalPoints,
                Status = ExamStatusMapper.FromDbString(r.Status),
                AssignedStudentsCount = r.AssignedStudentsCount,
                PublishedAt = r.PublishedAt,
                ClosedAt = r.ClosedAt,
                CancelledAt = r.CancelledAt
            }).ToList();
        }

        public async Task<List<Exam>> GetAssignedToStudentAsync(Guid studentId)
        {
            var entities = await _context.Exams
                .AsNoTracking()
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
            var entities = await _context.Exams
                .AsNoTracking()
                .Where(e => e.ClassSubjectId == classSubjectId)
                .OrderByDescending(e => e.AvailableFrom)
                .ToListAsync();

            return entities.Select(MapToModel).ToList();
        }
        public Task<bool> HasSubmissionsAsync(Guid examId)
        {
            return _context.ExamSubmissions.AnyAsync(s => s.ExamId == examId);
        }
        public async Task AddAsync(Exam exam)
        {
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

            await _context.SaveChangesAsync();
        }
        public async Task UpdateAsync(Exam exam)
        {
            var entity = await _context.Exams.FirstOrDefaultAsync(e => e.Id == exam.Id);

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

            await _context.SaveChangesAsync();
        }
        public async Task ReplaceStructureAsync(Exam exam)
        {
            var entity = await _context.Exams
                .Include(e => e.Questions)
                    .ThenInclude(q => q.QuestionOptions)
                .FirstOrDefaultAsync(e => e.Id == exam.Id);

            if (entity is null) return;

            _context.Questions.RemoveRange(entity.Questions);

            entity.Questions.Clear();

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

                entity.Questions.Add(questionEntity);
            }

            entity.TotalPoints = exam.TotalPoints;

            await _context.SaveChangesAsync();
        }
        public async Task ReplaceAssignedStudentsAsync(Guid examId, IReadOnlyCollection<ExamStudent> students)
        {
            var currentStudents = await _context.ExamStudents
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

            await _context.SaveChangesAsync();
        }
        public async Task DeleteDraftAsync(Guid examId)
        {
            var entity = await _context.Exams
                .FirstOrDefaultAsync(e => e.Id == examId);

            if (entity is null) return;

            _context.Exams.Remove(entity);

            await _context.SaveChangesAsync();
        }

        private static Exam MapToModel(ExamEntity entity)
        {
            return new Exam
            {
                Id = entity.Id,
                ClassSubjectId = entity.ClassSubjectId,
                RetakeOfExamId = entity.RetakeOfExamId,
                Name = entity.Name,
                Description = entity.Description,
                AvailableFrom = entity.AvailableFrom,
                AvailableUntil = entity.AvailableUntil,
                DurationMinutes = entity.DurationMinutes,
                PassingPercent = entity.PassingPercent,
                TotalPoints = entity.TotalPoints,
                Status = ExamStatusMapper.FromDbString(entity.Status),
                PublishedAt = entity.PublishedAt,
                ClosedAt = entity.ClosedAt,
                CancelledAt = entity.CancelledAt,
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
