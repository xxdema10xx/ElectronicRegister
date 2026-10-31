using ElectronicRegisterAPI.Domain.Policies;
using System;
using System.Collections.Generic;
using System.Linq;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Application.Mappers;

namespace ElectronicRegisterAPI.Application.Services;

public class ExamSubmissionProcessor
{
    private readonly ITransactionManager _transactionManager;
    private readonly IExamSubmissionRepository _submissionRepository;
    private readonly IExamRepository _examRepository;
    private readonly ISubmissionGradingService _gradingService;
    private readonly IGradeRepository _gradeRepository;
    private readonly IAuditRepository _auditRepository;
    private readonly ITimeProvider _timeProvider;
    public ExamSubmissionProcessor(
        ITransactionManager transactionManager,
        IExamSubmissionRepository submissionRepository,
        IExamRepository examRepository,
        ISubmissionGradingService gradingService,
        IGradeRepository gradeRepository,
        IAuditRepository auditRepository,
        ITimeProvider timeProvider
    )
    {
        _transactionManager = transactionManager;
        _submissionRepository = submissionRepository;
        _examRepository = examRepository;
        _gradingService = gradingService;
        _gradeRepository = gradeRepository;
        _auditRepository = auditRepository;
        _timeProvider = timeProvider;
    }

    public Task<SubmissionResultDto> ProcessInstitutionAsync(
        Guid submissionId,
        SubmitExamDto? dto,
        SubmitReason reason,
        ExamPrincipal principal
    )
    {
        if (principal.Tenant is null || !principal.IsStudent || principal.StudentId is null || _submissionRepository is not IInstitutionBoundRepository bound || !bound.Matches(principal))
            throw new UnauthorizedAccessException("Contesto di elaborazione non autorizzato.");
        if (reason is not (SubmitReason.Manual or SubmitReason.TimeExpired))
            throw new ArgumentException("Motivo non valido.");
        return ProcessCoreAsync(submissionId, dto, reason, principal.UserId, principal.IsStudent);
    }

    internal Task<SubmissionResultDto> ProcessExpiryAsync(SubmissionExpiryWork work, CancellationToken ct)
    {
        if (_submissionRepository is not ISubmissionExpiryBoundRepository bound || !bound.Matches(work))
            throw new UnauthorizedAccessException("Contesto tecnico non corrispondente.");
        return ProcessCoreAsync(work.SubmissionId, null, SubmitReason.TimeExpired, null, false, work, ct);
    }

    // Used by initial submit and both replay paths inside the caller's transaction.
    // SuggestedGrade never substitutes for the persisted official Grade.
    private async Task<SubmissionResultDto> ReadResultAsync(Guid submissionId, ExamSubmission fallback, bool studentView)
    {
        var current = await _submissionRepository.GetByIdAsync(submissionId, withAnswers: true) ?? fallback;
        decimal? officialGrade = null;
        if (current.GradeId.HasValue)
        {
            var grade = await _gradeRepository.GetByIdAsync(current.GradeId.Value) ?? throw new InvalidOperationException("Stato incoerente: GradeId presente ma Grade non trovato.");
            officialGrade = grade.Value;
        }

        return ExamSubmissionMapper.ToSubmissionResultDto(current, officialGrade, studentView);
    }

    internal async Task<SubmissionResultDto> ProcessCoreAsync(
        Guid submissionId,
        SubmitExamDto? dto,
        SubmitReason reason,
        Guid? actorUserId,
        bool studentView,
        SubmissionExpiryWork? work = null,
        CancellationToken ct = default
    )
    {
        SubmissionResultDto? result = null;
        await _transactionManager.ExecuteAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();
            // Acquire row lock
            var submission = await _submissionRepository.GetByIdForUpdateAsync(submissionId, withAnswers: true) ?? throw new KeyNotFoundException("La submission non esiste.");
            if (work is not null && (submission.Id != work.SubmissionId || submission.ExamId != work.ExamId || submission.StudentId != work.StudentId))
                throw new UnauthorizedAccessException("Ownership tecnica non corrispondente.");
            // idempotency: if already submitted (for any reason) return persisted result without reprocessing
            if (submission.SubmittedAt.HasValue)
            {
                // return current state
                result = await ReadResultAsync(submissionId, submission, studentView);
                return;
            }

            // Re-read deadline after acquiring row lock and use time provider
            var nowUtc = _timeProvider.UtcNow;
            if (work is not null && submission.Status != SubmissionStatus.InProgress)
                throw new InvalidOperationException("Submission tecnica non in corso.");
            // - automatic submit (TimeExpired) must not run before deadline
            if (reason == SubmitReason.TimeExpired && submission.DeadlineAt > nowUtc)
            {
                throw ElectronicRegisterAPI.Domain.Exceptions.SubmissionErrors.Conflict("Non è ancora tempo di eseguire il submit automatico.");
            }

            // If this is a manual submit arriving after deadline, reject explicitly and do not apply DTO or change submission metadata.
            if (reason == SubmitReason.Manual && submission.DeadlineAt <= nowUtc)
            {
                // Do not modify submission; let background worker handle automatic submit of persisted answers
                throw new ElectronicRegisterAPI.Domain.Exceptions.BusinessRuleException("La submission è scaduta; le ultime risposte non possono essere accettate. Attendere la consegna automatica.");
            }

            // If this is a manual submit before deadline, we may apply DTO; otherwise (automatic) dto is ignored.
            var applyDto = dto != null && reason == SubmitReason.Manual;
            // Do NOT modify SubmitReason or SubmittedAt here. Those are persisted atomically below only when the submit is actually accepted.
            // If DTO should be applied, persist answers now inside the transaction and after acquiring the lock.
            if (applyDto)
            {
                // build answers from DTO
                var newAnswers = dto!.Answers?.Select(a => new SubmissionAnswer { Id = Guid.NewGuid(), SubmissionId = submissionId, QuestionId = a.QuestionId, OpenText = a.OpenText, SelectedOptionIds = a.SelectedOptionIds ?? new List<Guid>() }).ToList() ?? new List<SubmissionAnswer>();
                var saveResult = await _submissionRepository.SaveAnswersAsync(submissionId, dto.ClientRevision, newAnswers);
                if (!saveResult.Accepted)
                {
                    // Do not hide the fact that answers were not saved
                    throw new ElectronicRegisterAPI.Domain.Exceptions.BusinessRuleException("Impossibile salvare le risposte inviate.");
                }

                // Re-read submission (with answers) after save to operate on persisted state
                submission = await _submissionRepository.GetByIdForUpdateAsync(submissionId, withAnswers: true) ?? throw new KeyNotFoundException("La submission non esiste.");
            }

            // Now attempt to mark the submission as begun (persist SubmittedAt/SubmitReason) in the DB.
            // This ensures SubmittedAt/SubmitReason are only set when allowed and after the lock.
            var began = await _submissionRepository.TryBeginSubmitAsync(submissionId, nowUtc, reason);
            if (!began)
            {
                // Another actor may have submitted concurrently; return current persisted state (idempotent)
                result = await ReadResultAsync(submissionId, submission, studentView);
                return;
            }

            // load exam structure (needed for required checks and grading)
            var exam = await _examRepository.GetByIdAsync(submission.ExamId, includeStructure: true) ?? throw new KeyNotFoundException("Esame non trovato.");
            // validate required questions only for manual submit when DTO was applied
            if (reason == SubmitReason.Manual)
            {
                var missingRequired = exam.Questions.Where(q => q.Required).Any(q => !(submission.Answers ?? new List<SubmissionAnswer>()).Any(a => a.QuestionId == q.Id && (q.Type == QuestionType.Open ? !string.IsNullOrWhiteSpace(a.OpenText) : a.SelectedOptionIds?.Count > 0)));
                if (missingRequired)
                    throw ElectronicRegisterAPI.Domain.Exceptions.SubmissionErrors.InvalidRequest("Non tutte le domande obbligatorie sono state compilate.");
            }

            // Use persisted answers for grading
            var answers = submission.Answers ?? new List<SubmissionAnswer>();
            var closedResult = _gradingService.GradeClosedQuestions(exam, answers);
            submission.ClosedScore = closedResult.ClosedScore;
            submission.OpenMaxPoints = closedResult.OpenMaxPoints;
            submission.MaxScore = closedResult.MaxScore;
            submission.PassingScore = closedResult.PassingScore;
            var evaluation = _gradingService.EvaluateAfterSubmit(exam, closedResult);
            submission.OpenScore = submission.OpenScore ?? 0m;
            if (evaluation.Status == SubmissionStatus.Graded)
            {
                submission.TotalScore = evaluation.TotalScore;
                // For closed-only graded submissions, compute SuggestedGrade and create the official Grade equal to SuggestedGrade
                submission.Status = SubmissionStatus.Graded;
                var gradeValue = _gradingService.ConvertToGrade(submission.TotalScore ?? 0m, submission.MaxScore ?? 1m, exam.PassingPercent);
                // persist suggested grade
                submission.SuggestedGrade = gradeValue;
                var grade = new Grade
                {
                    Id = Guid.NewGuid(),
                    StudentId = submission.StudentId,
                    ClassSubjectId = exam.ClassSubjectId,
                    Value = gradeValue,
                    Date = DateOnly.FromDateTime(nowUtc)
                };
                await _gradeRepository.AddAsync(grade);
                submission.GradeId = grade.Id;
                // Official Passed is derived from Grade.Value
                submission.Passed = GradeRules.IsPassing(gradeValue);
            }
            else if (evaluation.Status == SubmissionStatus.PendingReview)
            {
                submission.Status = SubmissionStatus.PendingReview;
            }
            else
            {
                submission.Status = SubmissionStatus.FailedAuto;
            }

            // Set SubmittedAt only when we are actually committing the submit (we are inside transaction and passed checks above)
            submission.SubmittedAt = nowUtc;
            await _submissionRepository.UpdateAfterGradingAsync(submission);
            await _auditRepository.AddAsync(new Domain.Models.AuditEvent { Id = Guid.NewGuid(), ActorUserId = actorUserId, Action = "submission.submit", EntityType = "exam_submission", EntityId = submission.Id, CreatedAt = nowUtc, RequestId = work is null ? null : "worker:" + work.SubmissionId.ToString("N"), BeforeJson = work is null ? null : System.Text.Json.JsonSerializer.Serialize(new { status = "in_progress" }), AfterJson = work is null ? null : System.Text.Json.JsonSerializer.Serialize(new { source = "institution-worker", reason = "TimeExpired", status = submission.Status.ToString() }) });
            result = await ReadResultAsync(submissionId, submission, studentView);
            ct.ThrowIfCancellationRequested();
        }, ct);
        return result!;
    }
}
