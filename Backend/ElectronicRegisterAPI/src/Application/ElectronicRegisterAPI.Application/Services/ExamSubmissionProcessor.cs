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

public class ExamSubmissionProcessor : IExamSubmissionProcessor
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
        ITimeProvider timeProvider)
    {
        _transactionManager = transactionManager;
        _submissionRepository = submissionRepository;
        _examRepository = examRepository;
        _gradingService = gradingService;
        _gradeRepository = gradeRepository;
        _auditRepository = auditRepository;
        _timeProvider = timeProvider;
    }

        public async Task<SubmissionResultDto> ProcessSubmitAsync(Guid submissionId, SubmitExamDto? dto, SubmitReason reason, ClaimsContext? caller)
        {
            SubmissionResultDto? result = null;

        await _transactionManager.ExecuteAsync(async () =>
        {
            // Acquire row lock
            var submission = await _submissionRepository.GetByIdForUpdateAsync(submissionId, withAnswers: true)
                ?? throw new KeyNotFoundException("La submission non esiste.");

            // idempotency: if already submitted (for any reason) return persisted result without reprocessing
            if (submission.SubmittedAt.HasValue)
            {
                // return current state
                var current = await _submissionRepository.GetByIdAsync(submissionId, withAnswers: true);
                var target = current ?? submission;
                decimal? officialGrade = null;
                if (target.GradeId.HasValue)
                {
                    var grade = await _gradeRepository.GetByIdAsync(target.GradeId.Value);
                    if (grade is null) throw new InvalidOperationException("Stato incoerente: GradeId presente ma Grade non trovato.");
                    officialGrade = grade.Value;
                }
                result = ExamSubmissionMapper.ToSubmissionResultDto(target, officialGrade);
                return;
            }

            // Re-read deadline after acquiring row lock and use time provider
            var nowUtc = _timeProvider.UtcNow;

            // - automatic submit (TimeExpired) must not run before deadline
            if (reason == SubmitReason.TimeExpired && submission.DeadlineAt > nowUtc)
            {
                throw new InvalidOperationException("Non è ancora tempo di eseguire il submit automatico.");
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
                var newAnswers = dto!.Answers?.Select(a => new SubmissionAnswer
                {
                    Id = Guid.NewGuid(),
                    SubmissionId = submissionId,
                    QuestionId = a.QuestionId,
                    OpenText = a.OpenText,
                    SelectedOptionIds = a.SelectedOptionIds ?? new List<Guid>()
                }).ToList() ?? new List<SubmissionAnswer>();

                var saveResult = await _submissionRepository.SaveAnswersAsync(submissionId, dto.ClientRevision, newAnswers);

                if (!saveResult.Accepted)
                {
                    // Do not hide the fact that answers were not saved
                    throw new ElectronicRegisterAPI.Domain.Exceptions.BusinessRuleException("Impossibile salvare le risposte inviate.");
                }

                // Re-read submission (with answers) after save to operate on persisted state
                submission = await _submissionRepository.GetByIdForUpdateAsync(submissionId, withAnswers: true)
                    ?? throw new KeyNotFoundException("La submission non esiste.");
            }

            // Now attempt to mark the submission as begun (persist SubmittedAt/SubmitReason) in the DB.
            // This ensures SubmittedAt/SubmitReason are only set when allowed and after the lock.
            var began = await _submissionRepository.TryBeginSubmitAsync(submissionId, nowUtc, reason);
            if (!began)
            {
                // Another actor may have submitted concurrently; return current persisted state (idempotent)
                var current = await _submissionRepository.GetByIdAsync(submissionId, withAnswers: true);
                var target = current ?? submission;
                decimal? officialGrade = null;
                if (target.GradeId.HasValue)
                {
                    var grade = await _gradeRepository.GetByIdAsync(target.GradeId.Value);
                    if (grade is null) throw new InvalidOperationException("Stato incoerente: GradeId presente ma Grade non trovato.");
                    officialGrade = grade.Value;
                }
                result = ExamSubmissionMapper.ToSubmissionResultDto(target, officialGrade);
                return;
            }

            // load exam structure (needed for required checks and grading)
            var exam = await _examRepository.GetByIdAsync(submission.ExamId, includeStructure: true)
                ?? throw new KeyNotFoundException("Esame non trovato.");

            // validate required questions only for manual submit when DTO was applied
            if (reason == SubmitReason.Manual && applyDto)
            {
                var missingRequired = exam.Questions
                    .Where(q => q.Required)
                    .Select(q => q.Id)
                    .Except(submission.Answers?.Select(a => a.QuestionId) ?? Enumerable.Empty<Guid>())
                    .Any();

                if (missingRequired)
                    throw new InvalidOperationException("Non tutte le domande obbligatorie sono state compilate.");
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
                submission.Passed = evaluation.Passed ?? false;
                submission.Status = SubmissionStatus.Graded;

                // create grade: SubmitExamDto does not contain FinalGrade; compute suggested grade from grading service
                var gradeValue = _gradingService.ConvertToGrade(submission.TotalScore ?? 0m, submission.MaxScore ?? 1m, submission.PassingScore ?? 0m);
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

            await _auditRepository.AddAsync(new Domain.Models.AuditEvent
            {
                Id = Guid.NewGuid(),
                Action = "submission.submit",
                EntityType = "exam_submission",
                EntityId = submission.Id,
                CreatedAt = DateTime.UtcNow
            });

            var current2 = await _submissionRepository.GetByIdAsync(submissionId, withAnswers: true);
            var finalTarget = current2 ?? submission;
            decimal? finalOfficialGrade = null;
            if (finalTarget.GradeId.HasValue)
            {
                var g = await _gradeRepository.GetByIdAsync(finalTarget.GradeId.Value);
                if (g is null) throw new InvalidOperationException("Stato incoerente: GradeId presente ma Grade non trovato.");
                finalOfficialGrade = g.Value;
            }
            result = ExamSubmissionMapper.ToSubmissionResultDto(finalTarget, finalOfficialGrade);
        });

        return result!;
    }
}
