using ElectronicRegisterAPI.Application.Mappers;
using ElectronicRegisterAPI.Domain.Exceptions;
using System.Text.Json;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Application.Managers;

internal class ExamSubmissionManager : IExamSubmissionManager
{
    private readonly IExamSubmissionRepository _submissionRepository;
    private readonly IExamRepository _examRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IGradeRepository _gradeRepository;
    private readonly IAuditRepository _auditEventRepository;
    private readonly ISubmissionGradingService _gradingService;
    private readonly IGradeService _gradeService;
    private readonly ITransactionManager _transactionManager;
    private readonly IClassSubjectRepository _classSubjectRepository;

    public ExamSubmissionManager(
        IExamSubmissionRepository submissionRepository,
        IExamRepository examRepository,
        IStudentRepository studentRepository,
        IGradeRepository gradeRepository,
        IAuditRepository auditEventRepository,
        ISubmissionGradingService gradingService,
        ITransactionManager transactionManager,
        IClassSubjectRepository classSubjectRepository,
        IGradeService gradeService)
    {
        _submissionRepository = submissionRepository;
        _examRepository = examRepository;
        _studentRepository = studentRepository;
        _gradeRepository = gradeRepository;
        _auditEventRepository = auditEventRepository;
        _gradingService = gradingService;
        _gradeService = gradeService;
        _transactionManager = transactionManager;
        _classSubjectRepository = classSubjectRepository;
    }

    public async Task<List<SubmissionSummaryDto>> GetByExamAsync(Guid examId, SubmissionStatus? status, ExamPrincipal caller)
    {
        ArgumentNullException.ThrowIfNull(caller);
        if (status.HasValue && !Enum.IsDefined(status.Value))
            throw new ArgumentException("Stato submission non valido.", nameof(status));
        // Only Admin or the Teacher owning the ClassSubject for the exam are allowed
        if (caller.HasInstitutionReadScope)
        {
            _ = await _examRepository.GetByIdAsync(examId)
                ?? throw new KeyNotFoundException("Esame non trovato.");
        }
        else if (caller.IsTeacher)
        {
            if (!caller.TeacherId.HasValue)
                throw new UnauthorizedAccessException("Identificativo docente non presente.");

            var exam = await _examRepository.GetByIdAsync(examId);
            if (exam is null) throw new KeyNotFoundException("Esame non trovato.");

            var classSubject = await _classSubjectRepository.GetByIdAsync(exam.ClassSubjectId);
            if (classSubject is null || classSubject.TeacherId != caller.TeacherId)
                throw new UnauthorizedAccessException("Non sei autorizzato a visualizzare le submissions.");
        }
        else
        {
            throw new UnauthorizedAccessException("Non sei autorizzato a visualizzare le submissions.");
        }

        var submissions = await _submissionRepository.GetByExamAsync(examId, status);

        var students = (await _studentRepository.GetByIdsAsync(submissions.Select(s => s.StudentId).Distinct())).ToDictionary(s => s.Id);

        return submissions.Select(s => new SubmissionSummaryDto
        {
            Id = s.Id,
            ExamId = s.ExamId,
            StudentId = s.StudentId,
            StudentFirstName = students.TryGetValue(s.StudentId, out var st) ? st.FirstName : string.Empty,
            StudentLastName = students.TryGetValue(s.StudentId, out var st2) ? st2.LastName : string.Empty,
            Status = s.Status,
            StartedAt = s.StartedAt,
            DeadlineAt = s.DeadlineAt,
            SubmittedAt = s.SubmittedAt,
            SubmitReason = s.SubmitReason,
            ViolationCount = s.ViolationCount,
            TotalScore = s.TotalScore,
            MaxScore = s.MaxScore,
            Passed = s.Passed,
            GradeId = s.GradeId
        }).ToList();
    }

    public async Task<SubmissionReviewDto> GetByIdAsync(Guid id, ExamPrincipal caller)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var submission = await _submissionRepository.GetByIdAsync(id, withAnswers: true)
            ?? throw new KeyNotFoundException("La submission non esiste.");

        if (!caller.IsStudent && !caller.IsTeacher && !caller.HasInstitutionReadScope)
            throw new UnauthorizedAccessException("Non sei autorizzato a visualizzare questa submission.");

        // authorization: teacher/admin or the student who owns it
        if (caller.IsStudent && caller.StudentId != submission.StudentId)
            throw new UnauthorizedAccessException("Non sei autorizzato a visualizzare questa submission.");

        if (caller.IsTeacher)
        {
            var exam = await _examRepository.GetByIdAsync(submission.ExamId);
            if (exam is null) throw new KeyNotFoundException("Esame non trovato.");

            // teacher can view only if they are assigned to the classSubject
            if (caller.IsTeacher)
            {
                if (!caller.TeacherId.HasValue)
                    throw new UnauthorizedAccessException("Non sei autorizzato a visualizzare questa submission.");

                // verify teacher ownership of classSubject
                var classSubject = await _classSubjectRepository.GetByIdAsync(exam.ClassSubjectId);
                if (classSubject is null || classSubject.TeacherId != caller.TeacherId)
                    throw new UnauthorizedAccessException("Non sei autorizzato a visualizzare questa submission.");
            }
        }

        var student = await _studentRepository.GetByIdAsync(submission.StudentId);
        var examWithStructure = await _examRepository.GetByIdAsync(submission.ExamId, includeStructure: true)
            ?? throw new KeyNotFoundException("Esame non trovato.");

        var answers = new List<SubmissionAnswerReviewDto>();

        foreach (var a in submission.Answers)
        {
            var question = examWithStructure.Questions.FirstOrDefault(q => q.Id == a.QuestionId);
            var selectedOptionIds = a.SelectedOptionIds ?? new List<Guid>();
            var options = new List<SubmissionAnswerOptionReviewDto>();

            if (question?.Options != null)
            {
                foreach (var opt in question.Options)
                {
                    options.Add(new SubmissionAnswerOptionReviewDto
                    {
                        Id = opt.Id,
                        Position = opt.Position,
                        Text = opt.Text,
                        IsCorrect = opt.IsCorrect,
                        IsSelected = selectedOptionIds.Contains(opt.Id)
                    });
                }
            }
            else
            {
                foreach (var optionId in selectedOptionIds)
                {
                    options.Add(new SubmissionAnswerOptionReviewDto
                    {
                        Id = optionId,
                        Position = 0,
                        Text = string.Empty,
                        IsCorrect = false,
                        IsSelected = true
                    });
                }
            }

            answers.Add(new SubmissionAnswerReviewDto
            {
                AnswerId = a.Id,
                QuestionId = a.QuestionId,
                QuestionText = question?.Text ?? string.Empty,
                Position = question?.Position ?? 0,
                QuestionType = question?.Type ?? default,
                MaxPoints = question?.Points ?? 0m,
                Required = question?.Required ?? false,
                OpenText = a.OpenText,
                Options = options,
                IsCorrect = a.IsCorrect,
                AwardedPoints = a.AwardedPoints,
                TeacherComment = a.TeacherComment
            });
        }

        decimal? officialGrade = null;
        if (submission.GradeId.HasValue)
        {
            var grade = await _gradeRepository.GetByIdAsync(submission.GradeId.Value)
                ?? throw new InvalidOperationException("Stato incoerente: GradeId presente ma Grade non trovato.");
            officialGrade = grade.Value;
        }

        var dto = new SubmissionReviewDto
        {
            Id = submission.Id,
            ExamId = submission.ExamId,
            ExamName = examWithStructure.Name ?? string.Empty,
            StudentId = submission.StudentId,
            StudentFirstName = student?.FirstName ?? string.Empty,
            StudentLastName = student?.LastName ?? string.Empty,
            Status = submission.Status,
            StartedAt = submission.StartedAt,
            DeadlineAt = submission.DeadlineAt,
            SubmittedAt = submission.SubmittedAt,
            SubmitReason = submission.SubmitReason,
            ViolationCount = submission.ViolationCount,
            MaxScore = submission.MaxScore,
            PassingScore = submission.PassingScore,
            ClosedScore = submission.ClosedScore,
            OpenMaxPoints = submission.OpenMaxPoints,
            OpenScore = submission.OpenScore,
            TotalScore = submission.TotalScore,
            Passed = submission.Passed,
            SuggestedGrade = submission.SuggestedGrade,
            GradeId = submission.GradeId,
            FinalGrade = officialGrade,
            ReviewedByUserId = submission.ReviewedByUserId,
            FinalizedAt = submission.FinalizedAt,
            Answers = answers
        };

        return SubmissionVisibility.Apply(dto, caller.IsStaff ? null : caller.VisibilityCaller);
    }

    public async Task ReviewAsync(Guid id, ReviewOpenAnswersDto dto, ExamPrincipal caller)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(dto);
        if (!caller.IsTeacher && !caller.IsAdmin)
            throw new UnauthorizedAccessException("Non sei autorizzato a revisionare submissions.");
        if (dto.Answers is null || dto.Answers.Any(a => a is null))
            throw new ArgumentException("La lista delle risposte non può essere nulla o contenere elementi null.");
        // Review must be transactional to avoid races with Finalize
        await _transactionManager.ExecuteAsync(async () =>
        {
            var submission = await _submissionRepository.GetByIdForUpdateAsync(id, withAnswers: true)
                ?? throw new KeyNotFoundException("La submission non esiste.");

            // Authorization: if teacher, must own the classSubject
            if (caller.IsTeacher)
            {
                if (!caller.TeacherId.HasValue) throw new UnauthorizedAccessException("Identificativo docente non presente.");
                var examCheck = await _examRepository.GetByIdAsync(submission.ExamId);
                if (examCheck is null) throw new KeyNotFoundException("Esame non trovato.");
                var classSubject = await _classSubjectRepository.GetByIdAsync(examCheck.ClassSubjectId);
                if (classSubject is null || classSubject.TeacherId != caller.TeacherId)
                    throw new UnauthorizedAccessException("Non sei autorizzato a revisionare questa submission.");
            }

            if (!submission.SubmittedAt.HasValue)
                throw SubmissionErrors.Conflict("La submission non è stata consegnata e non può essere revisionata.");
            if (submission.Status == SubmissionStatus.InProgress)
                throw SubmissionErrors.Conflict("Non è possibile revisionare una submission InProgress.");
            if (submission.Status == SubmissionStatus.Finalized)
                throw SubmissionErrors.Conflict("Non è possibile revisionare una submission già Finalized.");
            if (submission.GradeId.HasValue)
                throw SubmissionErrors.Conflict("Non è possibile modificare i punteggi di una submission che ha già un voto ufficiale.");

            // load exam once for question metadata
            var exam = await _examRepository.GetByIdAsync(submission.ExamId, includeStructure: true)
                ?? throw new KeyNotFoundException("Esame non trovato.");

            // validate and apply points
            foreach (var input in dto.Answers)
            {
                // OpenAnswerReviewInputDto uses AnswerId
                var answer = submission.Answers.FirstOrDefault(a => a.Id == input.AnswerId);
                if (answer is null) throw new KeyNotFoundException($"Answer {input.AnswerId} non trovato nella submission.");

                var question = exam.Questions.FirstOrDefault(q => q.Id == answer.QuestionId);
                if (question is null) throw new KeyNotFoundException($"Question {answer.QuestionId} non trovato nell'esame.");
                if (question.Type != QuestionType.Open)
                    throw SubmissionErrors.InvalidRequest("È consentita la correzione manuale solo per domande di tipo Open.");

                _gradingService.EnsureReviewPointsValid(question, input.AwardedPoints);

                answer.AwardedPoints = input.AwardedPoints;
                answer.TeacherComment = input.Comment;
            }

            submission.OpenScore = _gradingService.CalculateOpenScore(exam, submission.Answers);
            submission.TotalScore = (submission.ClosedScore ?? 0m) + (submission.OpenScore ?? 0m);
            // Passed is not official until a Grade exists; here we set a provisional Passed based on passing percent
            submission.Passed = submission.TotalScore >= (submission.PassingScore ?? 0m);

            // Do NOT finalize here; mark as pending review and persist awarded points.
            submission.Status = SubmissionStatus.PendingReview;

            var allOpenAnswersReviewed = submission.Answers
                .Where(a => exam.Questions.Any(q => q.Id == a.QuestionId && q.Type == QuestionType.Open))
                .All(a => a.AwardedPoints.HasValue);

            if (allOpenAnswersReviewed)
            {
                submission.SuggestedGrade = _gradingService.ConvertToGrade(
                    submission.TotalScore ?? 0m,
                    submission.MaxScore ?? 1m,
                    exam.PassingPercent);
            }

            await _submissionRepository.UpdateAfterReviewAsync(submission);
        });
    }

    public async Task FinalizeAsync(Guid id, FinalizeSubmissionDto dto, ExamPrincipal caller)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(dto);

        if (!caller.IsTeacher && !caller.IsAdmin)
            throw new UnauthorizedAccessException("Non sei autorizzato a finalizzare submissions.");

        // create grade and update submission in a transaction with DB row lock
        await _transactionManager.ExecuteAsync(async () =>
        {
            var submission = await _submissionRepository.GetByIdForUpdateAsync(id, withAnswers: true)
                ?? throw new KeyNotFoundException("La submission non esiste.");

            // Authorization: if teacher, must own the classSubject
            if (caller.IsTeacher)
            {
                if (!caller.TeacherId.HasValue) throw new UnauthorizedAccessException("Identificativo docente non presente.");
                var examCheck = await _examRepository.GetByIdAsync(submission.ExamId);
                if (examCheck is null) throw new KeyNotFoundException("Esame non trovato.");
                var classSubject = await _classSubjectRepository.GetByIdAsync(examCheck.ClassSubjectId);
                if (classSubject is null || classSubject.TeacherId != caller.TeacherId)
                    throw new UnauthorizedAccessException("Non sei autorizzato a finalizzare questa submission.");
            }

            // Submission must have been submitted before finalization
            if (!submission.SubmittedAt.HasValue)
                throw SubmissionErrors.Conflict("Non è possibile finalizzare una submission non consegnata.");

            // Prevent finalizing an InProgress submission
            if (submission.Status == SubmissionStatus.InProgress)
                throw SubmissionErrors.Conflict("Non è possibile finalizzare una submission InProgress.");

            // If already finalized, honor idempotency only when a Grade exists. If Finalized but GradeId is null, treat as inconsistent.
            if (submission.Status == SubmissionStatus.Finalized)
            {
                if (submission.GradeId.HasValue)
                {
                    var grade = await _gradeRepository.GetByIdAsync(submission.GradeId.Value)
                        ?? throw new InvalidOperationException("Stato incoerente: GradeId presente ma Grade non trovato.");
                    var requestedGrade = dto.FinalGrade ?? submission.SuggestedGrade;
                    if (requestedGrade != grade.Value || dto.OverrideReason?.Trim() != submission.OverrideReason)
                        throw SubmissionErrors.Conflict("Richiesta di finalizzazione in conflitto con quella già applicata.");
                    return;
                }
                else
                {
                    throw new InvalidOperationException("Stato incoerente: submission Finalized senza GradeId.");
                }
            }

            var exam = await _examRepository.GetByIdAsync(submission.ExamId, includeStructure: true)
                ?? throw new KeyNotFoundException("Esame non trovato.");

            // ensure all open answers are reviewed before finalization
            foreach (var answer in submission.Answers)
            {
                var question = exam.Questions.FirstOrDefault(q => q.Id == answer.QuestionId);
                if (question != null && question.Type == QuestionType.Open && !answer.AwardedPoints.HasValue)
                    throw SubmissionErrors.Conflict("Non tutte le risposte aperte sono state corrette.");
            }

            var previousGrade = submission.GradeId.HasValue
                ? await _gradeRepository.GetByIdAsync(submission.GradeId.Value) : null;
            var beforeJson = JsonSerializer.Serialize(new
            {
                submission.Status, submission.SuggestedGrade, Grade = previousGrade?.Value,
                submission.OverrideReason, submission.Passed, submission.FinalizedAt
            });

            submission.OpenScore = _gradingService.CalculateOpenScore(exam, submission.Answers);
            submission.TotalScore = (submission.ClosedScore ?? 0m) + (submission.OpenScore ?? 0m);
            submission.Passed = submission.TotalScore >= (submission.PassingScore ?? 0m);

            var suggestedGrade = _gradingService.ConvertToGrade(submission.TotalScore ?? 0m, submission.MaxScore ?? 1m, exam.PassingPercent);
            submission.SuggestedGrade = suggestedGrade;
            var gradeValue = dto.FinalGrade ?? suggestedGrade;
            var isOverride = gradeValue != suggestedGrade;

            if (dto.FinalGrade.HasValue)
            {
                // Validate final grade value
                _gradeService.EnsureValidGradeValue(dto.FinalGrade.Value);
                // When overriding, OverrideReason required if differs from suggested
                var suggested = submission.SuggestedGrade ?? _gradingService.ConvertToGrade(submission.TotalScore ?? 0m, submission.MaxScore ?? 1m, exam.PassingPercent);
                if (dto.FinalGrade.Value != suggested)
                {
                    if (string.IsNullOrWhiteSpace(dto.OverrideReason))
                        throw new ArgumentException("OverrideReason is required when FinalGrade differs from SuggestedGrade.");
                    if (dto.OverrideReason.Trim().Length > 1000)
                        throw new ArgumentException("OverrideReason exceeds maximum length of 1000.");
                }
                // trim and persist reason
                submission.OverrideReason = dto.OverrideReason?.Trim();
            }

            // If no official grade exists, create one with gradeValue (either suggested or final)
            if (!submission.GradeId.HasValue)
            {
                var grade = new Grade
                {
                    Id = Guid.NewGuid(),
                    StudentId = submission.StudentId,
                    ClassSubjectId = exam.ClassSubjectId,
                    Value = gradeValue,
                    Date = DateOnly.FromDateTime(DateTime.UtcNow)
                };
                await _gradeRepository.AddAsync(grade);
                submission.GradeId = grade.Id;
            }
            else
            {
                // Update existing Grade when overriding
                var existing = await _gradeRepository.GetByIdAsync(submission.GradeId.Value);
                if (existing is null) throw new InvalidOperationException("Stato incoerente: GradeId presente ma Grade non trovato.");
                if (dto.FinalGrade.HasValue && existing.Value != dto.FinalGrade.Value)
                {
                    // update grade value
                    existing.Value = dto.FinalGrade.Value;
                    await _gradeRepository.UpdateAsync(existing);
                }
            }

            // Ensure official Passed follows Grade.Value
            var finalGradeRow = await _gradeRepository.GetByIdAsync(submission.GradeId.Value);
            if (finalGradeRow is null) throw new InvalidOperationException("Stato incoerente: GradeId presente ma Grade non trovato.");
            submission.Passed = finalGradeRow.Value >= 6m;
            submission.FinalizedAt = DateTime.UtcNow;

            // ReviewedByUserId must reference a persisted User.Id (authenticated actor)
            if (caller.IsTeacher || caller.IsAdmin)
                submission.ReviewedByUserId = caller.UserId;

            submission.Status = SubmissionStatus.Finalized;
            submission.FinalizedAt = DateTime.UtcNow;

            await _submissionRepository.UpdateAfterGradingAsync(submission);

            await _auditEventRepository.AddAsync(new Domain.Models.AuditEvent
            {
                Id = Guid.NewGuid(),
                ActorUserId = caller.UserId,
                Action = isOverride ? "submission.override" : "submission.finalize",
                BeforeJson = beforeJson,
                AfterJson = JsonSerializer.Serialize(new
                {
                    submission.Status, submission.SuggestedGrade, Grade = finalGradeRow.Value,
                    submission.OverrideReason, submission.Passed, submission.FinalizedAt
                }),
                EntityType = "exam_submission",
                EntityId = submission.Id,
                CreatedAt = DateTime.UtcNow
            });
        });
    }

        Task<List<SubmissionSummaryDto>> IExamSubmissionManager.GetByExamAsync(Guid examId, SubmissionStatus? status, ClaimsContext caller) => GetByExamAsync(examId, status, ExamPrincipal.Legacy(caller));
        Task<SubmissionReviewDto> IExamSubmissionManager.GetByIdAsync(Guid id, ClaimsContext caller) => GetByIdAsync(id, ExamPrincipal.Legacy(caller));
        Task IExamSubmissionManager.ReviewAsync(Guid id, ReviewOpenAnswersDto dto, ClaimsContext caller) => ReviewAsync(id, dto, ExamPrincipal.Legacy(caller));
        Task IExamSubmissionManager.FinalizeAsync(Guid id, FinalizeSubmissionDto dto, ClaimsContext caller) => FinalizeAsync(id, dto, ExamPrincipal.Legacy(caller));
}
