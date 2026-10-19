using Microsoft.EntityFrameworkCore;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Results;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using ElectronicRegisterAPI.Infrastructure.Mappers;
using SubmissionEntity = ElectronicRegisterAPI.Infrastructure.Persistence.Entities.ExamSubmission;
using SubmissionAnswerEntity = ElectronicRegisterAPI.Infrastructure.Persistence.Entities.SubmissionAnswer;
using QuestionOptionEntity = ElectronicRegisterAPI.Infrastructure.Persistence.Entities.QuestionOption;
using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal class ExamSubmissionRepository : IExamSubmissionRepository
{
    private readonly ElectronicRegisterContext _context;
    private readonly ElectronicRegisterAPI.Domain.Interfaces.Services.ITimeProvider _timeProvider;

    public ExamSubmissionRepository(ElectronicRegisterContext context, ElectronicRegisterAPI.Domain.Interfaces.Services.ITimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<ExamSubmission?> GetByIdAsync(Guid id, bool withAnswers = false)
    {
        IQueryable<SubmissionEntity> query = _context.ExamSubmissions.AsNoTracking();

        if (withAnswers)
        {
            query = query
                .Include(s => s.SubmissionAnswers)
                    .ThenInclude(a => a.Options);
        }

        var entity = await query.FirstOrDefaultAsync(s => s.Id == id);

        if (entity is null)
            return null;

        return MapToModel(entity, withAnswers);
    }

    public async Task<ExamSubmission?> GetByIdForUpdateAsync(Guid id, bool withAnswers = false)
    {
        // Must be executed inside an existing EF Core transaction to ensure FOR UPDATE lock is held
        if (_context.Database.CurrentTransaction == null)
            throw new InvalidOperationException("GetByIdForUpdateAsync requires an ambient EF Core transaction. Call inside ITransactionManager.ExecuteAsync.");

        // Use FromSqlRaw with FOR UPDATE to acquire row lock within the existing transaction
        // Build base query including answers if requested
        IQueryable<SubmissionEntity> query = _context.ExamSubmissions;

        if (withAnswers)
        {
            query = query
                .Include(s => s.SubmissionAnswers)
                    .ThenInclude(a => a.Options);
        }

        // Execute raw SQL on the mapped table to ensure FOR UPDATE is applied
        var sql = "SELECT * FROM exam_submission WHERE id = {0} FOR UPDATE";
        var lockedEntityQuery = _context.ExamSubmissions.FromSqlRaw(sql, id).AsTracking();
        if (withAnswers)
        {
            lockedEntityQuery = lockedEntityQuery.Include(s => s.SubmissionAnswers).ThenInclude(a => a.Options);
        }

        var lockedEntity = await lockedEntityQuery.FirstOrDefaultAsync();

        return lockedEntity is null ? null : MapToModel(lockedEntity, withAnswers);
    }

    public async Task<ExamSubmission?> GetByExamAndStudentAsync(Guid examId, Guid studentId, bool withAnswers = false)
    {
        IQueryable<SubmissionEntity> query = _context.ExamSubmissions.AsNoTracking();

        if (withAnswers)
        {
            query = query
                .Include(s => s.SubmissionAnswers)
                    .ThenInclude(a => a.Options);
        }

        var entity = await query.FirstOrDefaultAsync(s => s.ExamId == examId && s.StudentId == studentId);

        return entity is null ? null : MapToModel(entity, withAnswers);
    }

    public async Task<List<ExamSubmission>> GetByExamAsync(Guid examId, SubmissionStatus? status = null)
    {
        var query = _context.ExamSubmissions.AsNoTracking().AsQueryable();

        query = query.Where(s => s.ExamId == examId);

        if (status.HasValue)
        {
            var dbStatus = SubmissionStatusMapper.ToDbString(status.Value);
            query = query.Where(s => s.Status == dbStatus);
        }

        var entities = await query
            .OrderBy(s => s.StartedAt)
            .ToListAsync();

        return entities.Select(e => MapToModel(e, withAnswers: false)).ToList();
    }

    public async Task<List<ExamSubmission>> GetByStudentAsync(Guid studentId)
    {
        var entities = await _context.ExamSubmissions
            .AsNoTracking()
            .Where(s => s.StudentId == studentId)
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync();

        return entities.Select(e => MapToModel(e, withAnswers: false)).ToList();
    }

    public async Task<List<ExamSubmission>> GetExpiredInProgressAsync(DateTime utcNow, int take)
    {
        var dbStatus = SubmissionStatusMapper.ToDbString(Domain.Enums.SubmissionStatus.InProgress);

        // For worker search we only need ids and minimal fields. However repository contract requires returning models with answers for now.
        var entities = await _context.ExamSubmissions
            .AsNoTracking()
            .Where(s => s.Status == dbStatus && s.DeadlineAt <= utcNow)
            .OrderBy(s => s.DeadlineAt)
            .Take(take)
            .ToListAsync();

        return entities.Select(e => MapToModel(e, withAnswers: false)).ToList();
    }

    public async Task AddAsync(ExamSubmission submission)
    {
        var entity = new SubmissionEntity
        {
            Id = submission.Id,
            ExamId = submission.ExamId,
            StudentId = submission.StudentId,
            Status = SubmissionStatusMapper.ToDbString(submission.Status),
            StartedAt = submission.StartedAt,
            DeadlineAt = submission.DeadlineAt,
            SubmittedAt = submission.SubmittedAt,
                LastClientRevision = (ulong)submission.Revision,
                SubmitReason = submission.SubmitReason.HasValue ? SubmitReasonMapper.ToDbString(submission.SubmitReason.Value) : null,
                ViolationCount = submission.ViolationCount,
                OpenMaxPoints = submission.OpenMaxPoints,
                SuggestedGrade = submission.SuggestedGrade,
                ReviewedByUserId = submission.ReviewedByUserId,
                FinalizedAt = submission.FinalizedAt,
            MaxScore = submission.MaxScore,
            PassingScore = submission.PassingScore,
            ClosedScore = submission.ClosedScore,
            OpenScore = submission.OpenScore,
            TotalScore = submission.TotalScore,
            Passed = submission.Passed,
            GradeId = submission.GradeId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // map answers if present
        if (submission.Answers != null && submission.Answers.Count > 0)
        {
            foreach (var a in submission.Answers)
            {
                var answerEntity = new SubmissionAnswerEntity
                {
                    Id = a.Id == Guid.Empty ? Guid.NewGuid() : a.Id,
                    SubmissionId = submission.Id,
                    QuestionId = a.QuestionId,
                    OpenText = a.OpenText,
                    IsCorrect = a.IsCorrect,
                    AwardedPoints = a.AwardedPoints,
                    TeacherComment = a.TeacherComment,
                    LastSavedAt = DateTime.UtcNow
                };

                if (a.SelectedOptionIds != null && a.SelectedOptionIds.Count > 0)
                {
                    var options = await _context.QuestionOptions.Where(o => a.SelectedOptionIds.Contains(o.Id)).ToListAsync();
                    foreach (var opt in options)
                        answerEntity.Options.Add(opt);
                }

                entity.SubmissionAnswers.Add(answerEntity);
            }
        }

        _context.ExamSubmissions.Add(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<SaveAnswersResult> SaveAnswersAsync(Guid submissionId, long clientRevision, IReadOnlyList<SubmissionAnswer> answers)
    {
        var ambient = _context.Database.CurrentTransaction != null;
        var tx = ambient ? null : await _context.Database.BeginTransactionAsync();

        try
        {
            // Acquire row lock to protect revision and answers (SELECT ... FOR UPDATE)
            var sql = "SELECT * FROM exam_submission WHERE id = {0} FOR UPDATE";
            var lockedQuery = _context.ExamSubmissions.FromSqlRaw(sql, submissionId).AsTracking();
            lockedQuery = lockedQuery.Include(s => s.SubmissionAnswers).ThenInclude(a => a.Options);
            var entity = await lockedQuery.FirstOrDefaultAsync();

            if (entity is null)
            {
                throw new KeyNotFoundException("La submission non esiste.");
            }

            var currentRevision = (long)entity.LastClientRevision;

            if (clientRevision <= currentRevision)
            {
                if (tx != null) await tx.RollbackAsync();
                return new SaveAnswersResult
                {
                    Accepted = false,
                    CurrentRevision = (long)entity.LastClientRevision,
                    SavedAt = null
                };
            }

            // Ensure submission in progress and not past deadline
            if (entity.Status != SubmissionStatusMapper.ToDbString(Domain.Enums.SubmissionStatus.InProgress))
            {
                throw new InvalidOperationException("Submission non è in stato InProgress.");
            }

            var now = _timeProvider.UtcNow;
            if (entity.DeadlineAt <= now)
            {
                if (tx != null) await tx.RollbackAsync();
                return new SaveAnswersResult { Accepted = false, CurrentRevision = currentRevision, SavedAt = null };
            }

            // Replace answers completely
            // Validate incoming answers against the exam structure to prevent tampering
            if (answers != null && answers.Count > 0)
            {
                // check duplicate question ids
                var dupQ = answers.GroupBy(a => a.QuestionId).FirstOrDefault(g => g.Count() > 1);
                if (dupQ != null)
                {
                    throw new InvalidOperationException($"Risposte duplicate per la domanda {dupQ.Key}.");
                }

                // load questions and options for the exam (persistence entities)
                var questionEntities = await _context.Questions
                    .Where(q => q.ExamId == entity.ExamId)
                    .Include(q => q.QuestionOptions)
                    .ToListAsync();

                foreach (var a in answers)
                {
                    var q = questionEntities.FirstOrDefault(x => x.Id == a.QuestionId);
                    if (q == null)
                    {
                        throw new KeyNotFoundException($"Question {a.QuestionId} non appartiene a questo esame.");
                    }

                    // ensure no duplicate selected options
                    if (a.SelectedOptionIds != null && a.SelectedOptionIds.Count != a.SelectedOptionIds.Distinct().Count())
                    {
                        throw new InvalidOperationException($"Opzioni duplicate nella risposta per la domanda {a.QuestionId}.");
                    }

                    // validate selected options belong to question
                    if (a.SelectedOptionIds != null && a.SelectedOptionIds.Count > 0)
                    {
                        var optionIds = q.QuestionOptions.Select(o => o.Id).ToHashSet();
                        if (a.SelectedOptionIds.Except(optionIds).Any())
                        {
                            throw new KeyNotFoundException($"Una o più opzioni selezionate non appartengono alla domanda {a.QuestionId}.");
                        }
                    }

                    // validate question type constraints
                    var qType = QuestionTypeMapper.FromDbString(q.Type);
                    if (qType == Domain.Enums.QuestionType.Open)
                    {
                        if (a.SelectedOptionIds != null && a.SelectedOptionIds.Count > 0)
                        {
                            throw new InvalidOperationException($"La domanda {a.QuestionId} è di tipo Open e non può avere opzioni selezionate.");
                        }
                    }
                    else if (qType == Domain.Enums.QuestionType.Single)
                    {
                        if (a.SelectedOptionIds != null && a.SelectedOptionIds.Count > 1)
                        {
                            throw new InvalidOperationException($"La domanda {a.QuestionId} è di tipo Single e non può avere più di un'opzione selezionata.");
                        }
                    }
                }
            }

            _context.SubmissionAnswers.RemoveRange(entity.SubmissionAnswers);

            var newAnswers = new List<SubmissionAnswerEntity>();

            if (answers != null)
            {
                foreach (var a in answers)
                {
                    // Ensure grading fields are not accepted from client-side payload
                    var ae = new SubmissionAnswerEntity
                    {
                        Id = a.Id == Guid.Empty ? Guid.NewGuid() : a.Id,
                        SubmissionId = submissionId,
                        QuestionId = a.QuestionId,
                        OpenText = a.OpenText,
                        IsCorrect = false,
                        AwardedPoints = null,
                        TeacherComment = null,
                        LastSavedAt = now
                    };

                    if (a.SelectedOptionIds != null && a.SelectedOptionIds.Count > 0)
                    {
                        var opts = await _context.QuestionOptions.Where(o => a.SelectedOptionIds.Contains(o.Id)).ToListAsync();
                        foreach (var o in opts) ae.Options.Add(o);
                    }

                    newAnswers.Add(ae);
                }
            }

            await _context.SubmissionAnswers.AddRangeAsync(newAnswers);

            // update revision
            entity.LastClientRevision = (ulong)clientRevision;
            entity.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            if (tx != null) await tx.CommitAsync();

            return new SaveAnswersResult
            {
                Accepted = true,
                CurrentRevision = (long)entity.LastClientRevision,
                SavedAt = now
            };
        }
        catch
        {
            if (tx != null) await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> TryBeginSubmitAsync(Guid submissionId, DateTime submittedAt, SubmitReason reason)
    {
        var entity = await _context.ExamSubmissions.FirstOrDefaultAsync(s => s.Id == submissionId);
        if (entity is null) throw new KeyNotFoundException("La submission non esiste.");

        // Only allow if still in progress and not previously submitted
        var inProgress = entity.Status == SubmissionStatusMapper.ToDbString(Domain.Enums.SubmissionStatus.InProgress);
        if (!inProgress || entity.SubmittedAt.HasValue)
            return false;

        entity.SubmittedAt = submittedAt;
        entity.SubmitReason = SubmitReasonMapper.ToDbString(reason);
        entity.Status = SubmissionStatusMapper.ToDbString(Domain.Enums.SubmissionStatus.PendingReview);
        entity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task UpdateAfterGradingAsync(ExamSubmission submission)
    {
        var entity = await _context.ExamSubmissions.FirstOrDefaultAsync(s => s.Id == submission.Id);
        if (entity is null) throw new KeyNotFoundException("La submission non esiste.");

        entity.Status = SubmissionStatusMapper.ToDbString(submission.Status);
        entity.MaxScore = submission.MaxScore;
        entity.PassingScore = submission.PassingScore;
        entity.ClosedScore = submission.ClosedScore;
        entity.OpenScore = submission.OpenScore;
        entity.TotalScore = submission.TotalScore;
        entity.Passed = submission.Passed;
        entity.GradeId = submission.GradeId;
            entity.OpenMaxPoints = submission.OpenMaxPoints;
            entity.SuggestedGrade = submission.SuggestedGrade;
            entity.OverrideReason = submission.OverrideReason;
            entity.ReviewedByUserId = submission.ReviewedByUserId;
            entity.FinalizedAt = submission.FinalizedAt;
        entity.UpdatedAt = DateTime.UtcNow;

        // Also persist awarded points and teacher comments on answers if present in the model
        if (submission.Answers != null && submission.Answers.Count > 0)
        {
            // load tracked answer entities
            var answerEntities = await _context.SubmissionAnswers.Where(a => a.SubmissionId == submission.Id).ToListAsync();

            foreach (var a in submission.Answers)
            {
                var ae = answerEntities.FirstOrDefault(x => x.Id == a.Id);
                if (ae != null)
                {
                    ae.AwardedPoints = a.AwardedPoints;
                    ae.TeacherComment = a.TeacherComment;
                    ae.IsCorrect = a.IsCorrect;
                    ae.LastSavedAt = DateTime.UtcNow;
                }
            }
        }

        await _context.SaveChangesAsync();
    }

    public async Task UpdateAfterReviewAsync(ExamSubmission submission)
    {
        // Same fields updated after review
        await UpdateAfterGradingAsync(submission);
    }

    private static ExamSubmission MapToModel(SubmissionEntity entity, bool withAnswers)
    {
        var model = new ExamSubmission
        {
            Id = entity.Id,
            ExamId = entity.ExamId,
            StudentId = entity.StudentId,
            Status = SubmissionStatusMapper.FromDbString(entity.Status),
            StartedAt = entity.StartedAt,
            DeadlineAt = entity.DeadlineAt,
            SubmittedAt = entity.SubmittedAt,
            Revision = (long)entity.LastClientRevision,
            MaxScore = entity.MaxScore,
            PassingScore = entity.PassingScore,
            ClosedScore = entity.ClosedScore,
            OpenScore = entity.OpenScore,
            TotalScore = entity.TotalScore,
            Passed = entity.Passed,
            GradeId = entity.GradeId,
            SubmitReason = string.IsNullOrWhiteSpace(entity.SubmitReason) ? null : SubmitReasonMapper.FromDbString(entity.SubmitReason),
            ViolationCount = entity.ViolationCount,
            OpenMaxPoints = entity.OpenMaxPoints,
            SuggestedGrade = entity.SuggestedGrade,
            OverrideReason = entity.OverrideReason,
            ReviewedByUserId = entity.ReviewedByUserId,
            FinalizedAt = entity.FinalizedAt,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            Answers = new List<SubmissionAnswer>()
        };

        if (withAnswers && entity.SubmissionAnswers != null)
        {
            model.Answers = entity.SubmissionAnswers.Select(a => new SubmissionAnswer
            {
                Id = a.Id,
                SubmissionId = a.SubmissionId,
                QuestionId = a.QuestionId,
                OpenText = a.OpenText,
                IsCorrect = a.IsCorrect,
                AwardedPoints = a.AwardedPoints,
                TeacherComment = a.TeacherComment,
                SelectedOptionIds = a.Options?.Select(o => o.Id).ToList() ?? new List<Guid>()
            }).ToList();
        }

        return model;
    }
}
