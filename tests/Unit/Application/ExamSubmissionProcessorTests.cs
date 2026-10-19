using System;
using System.Collections.Generic;
using System.Linq;
using ElectronicRegisterAPI.Business.Services;
using ElectronicRegisterAPI.Domain.Results;
using System.Threading.Tasks;
using Xunit;
using Moq;
using ElectronicRegisterAPI.Application.Services;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Tests.Unit.Application
{
    public class ExamSubmissionProcessorTests
    {
        private readonly Mock<ITransactionManager> _txMock = new();
        private readonly Mock<IExamSubmissionRepository> _subRepoMock = new();
        private readonly Mock<IExamRepository> _examRepoMock = new();
        private readonly Mock<ISubmissionGradingService> _gradingMock = new();
        private readonly Mock<IGradeRepository> _gradeRepoMock = new();
        private readonly Mock<IAuditRepository> _auditRepoMock = new();

        private static readonly DateTime Now = new(2026, 10, 17, 10, 0, 0, DateTimeKind.Utc);
        [Theory]
        [InlineData(QuestionType.Open)]
        [InlineData(QuestionType.Single)]
        [InlineData(QuestionType.Multiple)]
        public async Task Mt3dRequiredQuestionIdWithEmptyContentIsRejected(QuestionType type)
        {
            var processor = CreateProcessor();
            var submission = new ExamSubmission { Id = Guid.NewGuid(), DeadlineAt = Now.AddMinutes(10), Answers = new() };
            ConfigureSubmission(submission);
            var question = new Question { Id = Guid.NewGuid(), Type = type, Required = true, Text = "Required", Points = 1 };
            _examRepoMock.Setup(r => r.GetByIdAsync(submission.ExamId, true)).ReturnsAsync(new Exam { Name = "Test", PassingPercent = 60, Questions = new() { question } });
            _gradingMock.Setup(g => g.GradeClosedQuestions(It.IsAny<Exam>(), It.IsAny<IReadOnlyList<SubmissionAnswer>>())).Returns(new ClosedGradingResult { ClosedScore = 0, MaxScore = 1, PassingScore = .6m });
            _gradingMock.Setup(g => g.EvaluateAfterSubmit(It.IsAny<Exam>(), It.IsAny<ClosedGradingResult>())).Returns(new SubmissionEvaluation { Status = SubmissionStatus.Graded, TotalScore = 0 });
            var dto = new SubmitExamDto { ClientRevision = 1, Answers = new() { new() { QuestionId = question.Id, OpenText = type == QuestionType.Open ? "   " : null, SelectedOptionIds = new() } } };
            await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessSubmitAsync(submission.Id, dto, SubmitReason.Manual, new ClaimsContext(Guid.NewGuid(), UserRole.Student, submission.StudentId, null)));
            _gradeRepoMock.Verify(r => r.AddAsync(It.IsAny<Grade>()), Times.Never);
        }
        private readonly Dictionary<Guid, Grade> _grades = new();

        private void ConfigureSubmission(ExamSubmission submission)
        {
            _subRepoMock.Setup(r => r.GetByIdForUpdateAsync(submission.Id, true)).ReturnsAsync(submission);
            _subRepoMock.Setup(r => r.GetByIdAsync(submission.Id, true)).ReturnsAsync(submission);
            _subRepoMock.Setup(r => r.SaveAnswersAsync(submission.Id, It.IsAny<long>(), It.IsAny<IReadOnlyList<SubmissionAnswer>>()))
                .ReturnsAsync((Guid id, long revision, IReadOnlyList<SubmissionAnswer> answers) =>
                {
                    submission.Answers = answers.ToList();
                    return new SaveAnswersResult { Accepted = true, CurrentRevision = revision, SavedAt = Now };
                });
            _subRepoMock.Setup(r => r.TryBeginSubmitAsync(submission.Id, Now, It.IsAny<SubmitReason>()))
                .ReturnsAsync((Guid id, DateTime at, SubmitReason reason) =>
                {
                    if (submission.SubmittedAt.HasValue || submission.Status != SubmissionStatus.InProgress)
                        return false;
                    submission.SubmittedAt = at;
                    submission.SubmitReason = reason;
                    submission.Status = SubmissionStatus.PendingReview;
                    return true;
                });
            _subRepoMock.Setup(r => r.UpdateAfterGradingAsync(submission)).Returns(Task.CompletedTask);
        }

        private void VerifyPublished(ExamSubmission submission, Guid? actorUserId = null)
        {
            _txMock.Verify(t => t.ExecuteAsync(It.IsAny<Func<Task>>(), default), Times.Once);
            _subRepoMock.Verify(r => r.TryBeginSubmitAsync(submission.Id, Now, submission.SubmitReason!.Value), Times.Once);
            _subRepoMock.Verify(r => r.UpdateAfterGradingAsync(submission), Times.Once);
            _auditRepoMock.Verify(r => r.AddAsync(It.Is<AuditEvent>(a =>
                a.EntityId == submission.Id && a.EntityType == "exam_submission" &&
                a.Action == "submission.submit" && a.ActorUserId == actorUserId)), Times.Once);
        }

        private ExamSubmissionProcessor CreateProcessor()
        {
            _txMock.Setup(t => t.ExecuteAsync(It.IsAny<Func<Task>>(), default)).Returns<Func<Task>, System.Threading.CancellationToken>(async (f, ct) => await f());

            _gradeRepoMock.Setup(r => r.AddAsync(It.IsAny<Grade>()))
                .Callback<Grade>(grade => _grades.Add(grade.Id, grade)).Returns(Task.CompletedTask);
            _gradeRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((Guid id) => _grades.TryGetValue(id, out var grade) ? grade : null);
            _auditRepoMock.Setup(r => r.AddAsync(It.IsAny<AuditEvent>())).Returns(Task.CompletedTask);
            // Use the real conversion formula, independently covered by grading tests.
            _gradingMock.Setup(g => g.ConvertToGrade(It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<decimal>()))
                .Returns((decimal total, decimal max, decimal passing) => new SubmissionGradingService().ConvertToGrade(total, max, passing));
            var timeMock = new Mock<ITimeProvider>();
            timeMock.Setup(t => t.UtcNow).Returns(Now);

            return new ExamSubmissionProcessor(
                _txMock.Object,
                _subRepoMock.Object,
                _examRepoMock.Object,
                _gradingMock.Object,
                _gradeRepoMock.Object,
                _auditRepoMock.Object,
                timeMock.Object);
        }

        [Fact]
        public async Task ClosedOnly_GradedAndGradeCreated()
        {
            var proc = CreateProcessor();

            var submission = new ExamSubmission { Id = Guid.NewGuid(), Answers = new List<SubmissionAnswer>(), DeadlineAt = Now.AddMinutes(10) };
            ConfigureSubmission(submission);
            _examRepoMock.Setup(r => r.GetByIdAsync(submission.ExamId, true)).ReturnsAsync(new Exam { Name = "T", PassingPercent = 60m, Questions = new List<Question>() });
            _gradingMock.Setup(g => g.GradeClosedQuestions(It.IsAny<Exam>(), It.IsAny<IReadOnlyList<SubmissionAnswer>>())).Returns(new Domain.Results.ClosedGradingResult { ClosedScore = 10, OpenMaxPoints = 0, MaxScore = 10, PassingScore = 6 });
            _gradingMock.Setup(g => g.EvaluateAfterSubmit(It.IsAny<Exam>(), It.IsAny<Domain.Results.ClosedGradingResult>())).Returns(new Domain.Results.SubmissionEvaluation { Status = SubmissionStatus.Graded, TotalScore = 10m, Passed = true });

            _subRepoMock.Setup(r => r.GetByIdAsync(submission.Id, true)).ReturnsAsync(submission);

            var caller = new ClaimsContext(Guid.NewGuid(), UserRole.Student, submission.StudentId, null);
            var res = await proc.ProcessSubmitAsync(submission.Id, new SubmitExamDto(), SubmitReason.Manual, caller);

            _gradeRepoMock.Verify(g => g.AddAsync(It.IsAny<Grade>()), Times.Once);
            Assert.Equal(SubmissionStatus.Graded, res.Status);
            Assert.Equal(10m, res.Grade);
            Assert.Equal(10m, submission.SuggestedGrade);
            Assert.True(res.Passed);
            VerifyPublished(submission, caller.UserId);
            _subRepoMock.Verify(r => r.SaveAnswersAsync(submission.Id, 0, It.IsAny<IReadOnlyList<SubmissionAnswer>>()), Times.Once);
            _gradingMock.Verify(g => g.ConvertToGrade(10m, 10m, 60m), Times.Once);
        }



        [Fact]
        public async Task OpenQuestions_PendingReview_NoGradeCreated()
        {
            var proc = CreateProcessor();
            var submission = new ExamSubmission { Id = Guid.NewGuid(), Answers = new List<SubmissionAnswer>(), DeadlineAt = Now.AddMinutes(10) };
            ConfigureSubmission(submission);
            _examRepoMock.Setup(r => r.GetByIdAsync(submission.ExamId, true)).ReturnsAsync(new Exam { Name = "T", PassingPercent = 60m, Questions = new List<Question> { new Question { Id = Guid.NewGuid(), Type = QuestionType.Open, Points = 5, Text = "q" } } });
            _gradingMock.Setup(g => g.GradeClosedQuestions(It.IsAny<Exam>(), It.IsAny<IReadOnlyList<SubmissionAnswer>>())).Returns(new Domain.Results.ClosedGradingResult { ClosedScore = 0, OpenMaxPoints = 5, MaxScore = 5, PassingScore = 3 });
            _gradingMock.Setup(g => g.EvaluateAfterSubmit(It.IsAny<Exam>(), It.IsAny<Domain.Results.ClosedGradingResult>())).Returns(new Domain.Results.SubmissionEvaluation { Status = SubmissionStatus.PendingReview });

            _subRepoMock.Setup(r => r.GetByIdAsync(submission.Id, true)).ReturnsAsync(submission);

            var res = await proc.ProcessSubmitAsync(submission.Id, new SubmitExamDto(), SubmitReason.Manual, null);

            _gradeRepoMock.Verify(g => g.AddAsync(It.IsAny<Grade>()), Times.Never);
            Assert.Equal(SubmissionStatus.PendingReview, res.Status);
            Assert.Null(res.Grade);
            Assert.Null(submission.SuggestedGrade);
            VerifyPublished(submission);
            _gradingMock.Verify(g => g.ConvertToGrade(It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<decimal>()), Times.Never);
        }

        [Fact]
        public async Task FailedAuto_NoGradeCreated()
        {
            var proc = CreateProcessor();
            var submission = new ExamSubmission { Id = Guid.NewGuid(), Answers = new List<SubmissionAnswer>(), DeadlineAt = Now.AddMinutes(10) };
            ConfigureSubmission(submission);
            _examRepoMock.Setup(r => r.GetByIdAsync(submission.ExamId, true)).ReturnsAsync(new Exam { Name = "T", PassingPercent = 60m, Questions = new List<Question>() });
            _gradingMock.Setup(g => g.GradeClosedQuestions(It.IsAny<Exam>(), It.IsAny<IReadOnlyList<SubmissionAnswer>>())).Returns(new Domain.Results.ClosedGradingResult { ClosedScore = 0, OpenMaxPoints = 0, MaxScore = 0, PassingScore = 1 });
            // Exercise the processor fallback for a FailedAuto evaluation, independent of the real evaluator.
            _gradingMock.Setup(g => g.EvaluateAfterSubmit(It.IsAny<Exam>(), It.IsAny<Domain.Results.ClosedGradingResult>())).Returns(new Domain.Results.SubmissionEvaluation { Status = SubmissionStatus.FailedAuto, TotalScore = 0m, Passed = false });

            _subRepoMock.Setup(r => r.GetByIdAsync(submission.Id, true)).ReturnsAsync(submission);

            var res = await proc.ProcessSubmitAsync(submission.Id, new SubmitExamDto(), SubmitReason.Manual, null);

            _gradeRepoMock.Verify(g => g.AddAsync(It.IsAny<Grade>()), Times.Never);
            Assert.Equal(SubmissionStatus.FailedAuto, res.Status);
            Assert.Null(res.Grade);
            VerifyPublished(submission);
        }

        [Fact]
        public async Task DoubleSubmit_NoDuplicateGradeCreated()
        {
            var proc = CreateProcessor();
            var submission = new ExamSubmission { Id = Guid.NewGuid(), Answers = new List<SubmissionAnswer>(), SubmittedAt = Now };
            submission.GradeId = Guid.NewGuid();
            _grades.Add(submission.GradeId.Value, new Grade { Id = submission.GradeId.Value, Value = 7m });

            ConfigureSubmission(submission);
            _subRepoMock.Setup(r => r.GetByIdAsync(submission.Id, true)).ReturnsAsync(submission);

            var res = await proc.ProcessSubmitAsync(submission.Id, null, SubmitReason.Manual, null);

            _gradeRepoMock.Verify(g => g.AddAsync(It.IsAny<Grade>()), Times.Never);
            Assert.NotNull(res);
            Assert.Equal(7m, res.Grade);
            Assert.Single(_grades);
            _subRepoMock.Verify(r => r.SaveAnswersAsync(It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<IReadOnlyList<SubmissionAnswer>>()), Times.Never);
            _subRepoMock.Verify(r => r.TryBeginSubmitAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<SubmitReason>()), Times.Never);
            _subRepoMock.Verify(r => r.UpdateAfterGradingAsync(It.IsAny<ExamSubmission>()), Times.Never);
            _auditRepoMock.Verify(r => r.AddAsync(It.IsAny<AuditEvent>()), Times.Never);
            _gradingMock.Verify(g => g.GradeClosedQuestions(It.IsAny<Exam>(), It.IsAny<IReadOnlyList<SubmissionAnswer>>()), Times.Never);
        }

        [Fact]
        public async Task SubmitWithDifferentReason_NoReprocess()
        {
            var proc = CreateProcessor();
            var submission = new ExamSubmission { Id = Guid.NewGuid(), Answers = new List<SubmissionAnswer>(), SubmittedAt = Now };
            submission.SubmitReason = SubmitReason.Manual;

            ConfigureSubmission(submission);
            _subRepoMock.Setup(r => r.GetByIdAsync(submission.Id, true)).ReturnsAsync(submission);

            var res = await proc.ProcessSubmitAsync(submission.Id, null, SubmitReason.TimeExpired, null);

            _gradeRepoMock.Verify(g => g.AddAsync(It.IsAny<Grade>()), Times.Never);
            Assert.NotNull(res);
            Assert.Equal(SubmitReason.Manual, submission.SubmitReason);
            _subRepoMock.Verify(r => r.SaveAnswersAsync(It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<IReadOnlyList<SubmissionAnswer>>()), Times.Never);
            _subRepoMock.Verify(r => r.TryBeginSubmitAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<SubmitReason>()), Times.Never);
            _subRepoMock.Verify(r => r.UpdateAfterGradingAsync(It.IsAny<ExamSubmission>()), Times.Never);
            _auditRepoMock.Verify(r => r.AddAsync(It.IsAny<AuditEvent>()), Times.Never);
            _gradingMock.Verify(g => g.GradeClosedQuestions(It.IsAny<Exam>(), It.IsAny<IReadOnlyList<SubmissionAnswer>>()), Times.Never);
        }

        [Fact]
        public async Task RequiredMissing_ManualRejected()
        {
            var proc = CreateProcessor();

            var q1 = new Question { Id = Guid.NewGuid(), Required = true, Text = "q1" };
            var exam = new Exam { Name = "T", PassingPercent = 60m, Questions = new List<Question> { q1 } };
            var submission = new ExamSubmission { Id = Guid.NewGuid(), Answers = new List<SubmissionAnswer>(), DeadlineAt = Now.AddMinutes(10) };

            ConfigureSubmission(submission);
            _examRepoMock.Setup(r => r.GetByIdAsync(submission.ExamId, true)).ReturnsAsync(exam);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => proc.ProcessSubmitAsync(submission.Id, new SubmitExamDto(), SubmitReason.Manual, null));
            Assert.Contains("obbligatorie", error.Message);
            _subRepoMock.Verify(r => r.SaveAnswersAsync(submission.Id, 0, It.Is<IReadOnlyList<SubmissionAnswer>>(a => a.Count == 0)), Times.Once);
            _gradingMock.Verify(g => g.GradeClosedQuestions(It.IsAny<Exam>(), It.IsAny<IReadOnlyList<SubmissionAnswer>>()), Times.Never);
            _gradeRepoMock.Verify(r => r.AddAsync(It.IsAny<Grade>()), Times.Never);
            _auditRepoMock.Verify(r => r.AddAsync(It.IsAny<AuditEvent>()), Times.Never);
        }

        [Fact]
        public async Task RequiredMissing_AutoAllowed()
        {
            var proc = CreateProcessor();

            var q1 = new Question { Id = Guid.NewGuid(), Required = true, Text = "q1" };
            var exam = new Exam { Name = "T", PassingPercent = 60m, Questions = new List<Question> { q1 } };
            var submission = new ExamSubmission { Id = Guid.NewGuid(), Answers = new List<SubmissionAnswer>() };

            ConfigureSubmission(submission);
            _examRepoMock.Setup(r => r.GetByIdAsync(submission.ExamId, true)).ReturnsAsync(exam);
            _gradingMock.Setup(g => g.GradeClosedQuestions(It.IsAny<Exam>(), It.IsAny<IReadOnlyList<SubmissionAnswer>>())).Returns(new Domain.Results.ClosedGradingResult { ClosedScore = 0, OpenMaxPoints = 0, MaxScore = 10, PassingScore = 6 });
            _gradingMock.Setup(g => g.EvaluateAfterSubmit(It.IsAny<Exam>(), It.IsAny<Domain.Results.ClosedGradingResult>())).Returns(new Domain.Results.SubmissionEvaluation { Status = SubmissionStatus.Graded, TotalScore = 0m, Passed = false });

            _subRepoMock.Setup(r => r.GetByIdAsync(submission.Id, true)).ReturnsAsync(submission);

            var res = await proc.ProcessSubmitAsync(submission.Id, null, SubmitReason.TimeExpired, null);

            Assert.Equal(SubmissionStatus.Graded, res.Status);
            Assert.Equal(1m, res.Grade);
            Assert.False(res.Passed);
            VerifyPublished(submission);
            _subRepoMock.Verify(r => r.SaveAnswersAsync(It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<IReadOnlyList<SubmissionAnswer>>()), Times.Never);
            _gradeRepoMock.Verify(r => r.AddAsync(It.Is<Grade>(g => g.Value == 1m)), Times.Once);
        }

        [Fact]
        public async Task AutoSubmit_NoDto_NoCaller_UsesPersistedAnswers()
        {
            var proc = CreateProcessor();
            var ans = new SubmissionAnswer { Id = Guid.NewGuid(), QuestionId = Guid.NewGuid() };
            var submission = new ExamSubmission { Id = Guid.NewGuid(), Answers = new List<SubmissionAnswer> { ans } };
            ConfigureSubmission(submission);
            _examRepoMock.Setup(r => r.GetByIdAsync(submission.ExamId, true)).ReturnsAsync(new Exam { Name = "T", PassingPercent = 60m, Questions = new List<Question>() });
            _gradingMock.Setup(g => g.GradeClosedQuestions(It.IsAny<Exam>(), It.IsAny<IReadOnlyList<SubmissionAnswer>>())).Returns(new Domain.Results.ClosedGradingResult { ClosedScore = 0, OpenMaxPoints = 0, MaxScore = 10, PassingScore = 6 });
            _gradingMock.Setup(g => g.EvaluateAfterSubmit(It.IsAny<Exam>(), It.IsAny<Domain.Results.ClosedGradingResult>())).Returns(new Domain.Results.SubmissionEvaluation { Status = SubmissionStatus.Graded, TotalScore = 0m, Passed = false });

            _subRepoMock.Setup(r => r.GetByIdAsync(submission.Id, true)).ReturnsAsync(submission);

            var res = await proc.ProcessSubmitAsync(submission.Id, null, SubmitReason.TimeExpired, null);

            Assert.Equal(SubmissionStatus.Graded, res.Status);
            Assert.Equal(1m, res.Grade);
            Assert.False(res.Passed);
            VerifyPublished(submission);
            _gradingMock.Verify(g => g.GradeClosedQuestions(It.IsAny<Exam>(),
                It.Is<IReadOnlyList<SubmissionAnswer>>(a => a.Count == 1 && a[0] == ans)), Times.Once);
            _subRepoMock.Verify(r => r.SaveAnswersAsync(It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<IReadOnlyList<SubmissionAnswer>>()), Times.Never);
            _gradeRepoMock.Verify(r => r.AddAsync(It.Is<Grade>(g => g.Value == 1m)), Times.Once);
        }

        [Fact]
        public async Task PersistenceError_RollsBack_AndThrows()
        {
            var proc = CreateProcessor();
            var submission = new ExamSubmission { Id = Guid.NewGuid(), Answers = new List<SubmissionAnswer>(), DeadlineAt = Now.AddMinutes(10) };
            ConfigureSubmission(submission);
            _examRepoMock.Setup(r => r.GetByIdAsync(submission.ExamId, true)).ReturnsAsync(new Exam { Name = "T", PassingPercent = 60m, Questions = new List<Question>() });
            _gradingMock.Setup(g => g.GradeClosedQuestions(It.IsAny<Exam>(), It.IsAny<IReadOnlyList<SubmissionAnswer>>())).Returns(new Domain.Results.ClosedGradingResult { ClosedScore = 10, OpenMaxPoints = 0, MaxScore = 10, PassingScore = 6 });
            _gradingMock.Setup(g => g.EvaluateAfterSubmit(It.IsAny<Exam>(), It.IsAny<Domain.Results.ClosedGradingResult>())).Returns(new Domain.Results.SubmissionEvaluation { Status = SubmissionStatus.Graded, TotalScore = 10m, Passed = true });

            _gradeRepoMock.Setup(g => g.AddAsync(It.IsAny<Grade>())).ThrowsAsync(new InvalidOperationException("DB error"));

            // This unit verifies that the transaction delegate receives the persistence failure.
            // Actual database rollback is the responsibility of the real transaction manager.
            Exception transactionFailure = null;
            _txMock.Setup(t => t.ExecuteAsync(It.IsAny<Func<Task>>(), default))
                .Returns<Func<Task>, System.Threading.CancellationToken>(async (action, ct) =>
                {
                    try { await action(); }
                    catch (Exception ex) { transactionFailure = ex; throw; }
                });
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => proc.ProcessSubmitAsync(submission.Id, new SubmitExamDto(), SubmitReason.Manual, null));
            Assert.Equal("DB error", error.Message);
            Assert.Same(error, transactionFailure);
            Assert.Empty(_grades);
            _gradeRepoMock.Verify(r => r.AddAsync(It.IsAny<Grade>()), Times.Once);
            _txMock.Verify(t => t.ExecuteAsync(It.IsAny<Func<Task>>(), default), Times.Once);
            _subRepoMock.Verify(r => r.UpdateAfterGradingAsync(It.IsAny<ExamSubmission>()), Times.Never);
            _auditRepoMock.Verify(r => r.AddAsync(It.IsAny<AuditEvent>()), Times.Never);
        }

        [Fact]
        public async Task DeadlineReached_NoNewAnswersButExistingProcessed()
        {
            var proc = CreateProcessor();
            var submission = new ExamSubmission { Id = Guid.NewGuid(), Answers = new List<SubmissionAnswer>(), DeadlineAt = Now.AddMinutes(-1) };
            ConfigureSubmission(submission);
            _examRepoMock.Setup(r => r.GetByIdAsync(submission.ExamId, true)).ReturnsAsync(new Exam { Name = "T", PassingPercent = 60m, Questions = new List<Question>() });
            _gradingMock.Setup(g => g.GradeClosedQuestions(It.IsAny<Exam>(), It.IsAny<IReadOnlyList<SubmissionAnswer>>())).Returns(new Domain.Results.ClosedGradingResult { ClosedScore = 0, OpenMaxPoints = 0, MaxScore = 10, PassingScore = 6 });
            _gradingMock.Setup(g => g.EvaluateAfterSubmit(It.IsAny<Exam>(), It.IsAny<Domain.Results.ClosedGradingResult>())).Returns(new Domain.Results.SubmissionEvaluation { Status = SubmissionStatus.Graded, TotalScore = 0m, Passed = false });

            _subRepoMock.Setup(r => r.GetByIdAsync(submission.Id, true)).ReturnsAsync(submission);

            var res = await proc.ProcessSubmitAsync(submission.Id, null, SubmitReason.TimeExpired, null);

            Assert.Equal(SubmissionStatus.Graded, res.Status);
            Assert.Equal(1m, res.Grade);
            Assert.False(res.Passed);
            VerifyPublished(submission);
            _subRepoMock.Verify(r => r.SaveAnswersAsync(It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<IReadOnlyList<SubmissionAnswer>>()), Times.Never);
            _gradeRepoMock.Verify(r => r.AddAsync(It.Is<Grade>(g => g.Value == 1m)), Times.Once);
        }

        [Fact]
        public async Task OpenAnswersEmpty_TreatedAsZeroPoints()
        {
            var proc = CreateProcessor();
            var open = new Question { Id = Guid.NewGuid(), Text = "Open question", Type = QuestionType.Open, Points = 5m };
            var answer = new SubmissionAnswer { Id = Guid.NewGuid(), QuestionId = open.Id, OpenText = "" };
            var exam = new Exam { Name = "T", PassingPercent = 60m, Questions = new List<Question> { open } };
            var submission = new ExamSubmission { Id = Guid.NewGuid(), Answers = new List<SubmissionAnswer> { answer }, DeadlineAt = Now.AddMinutes(-1) };
            ConfigureSubmission(submission);
            _examRepoMock.Setup(r => r.GetByIdAsync(submission.ExamId, true)).ReturnsAsync(exam);
            var grading = new SubmissionGradingService();
            _gradingMock.Setup(g => g.GradeClosedQuestions(exam, submission.Answers))
                .Returns((Exam e, IReadOnlyList<SubmissionAnswer> answers) => grading.GradeClosedQuestions(e, answers));
            _gradingMock.Setup(g => g.EvaluateAfterSubmit(exam, It.IsAny<ClosedGradingResult>()))
                .Returns((Exam e, ClosedGradingResult closed) => grading.EvaluateAfterSubmit(e, closed));

            var res = await proc.ProcessSubmitAsync(submission.Id, null, SubmitReason.TimeExpired, null);

            // Empty open text contributes zero points, but positive open points still require review.
            Assert.Equal(0m, grading.CalculateOpenScore(exam, submission.Answers));
            Assert.Equal(0m, submission.OpenScore);
            Assert.Equal(SubmissionStatus.PendingReview, res.Status);
            Assert.Null(res.TotalScore);
            Assert.Null(res.Grade);
            Assert.Null(res.Passed);
            VerifyPublished(submission);
            _subRepoMock.Verify(r => r.SaveAnswersAsync(It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<IReadOnlyList<SubmissionAnswer>>()), Times.Never);
            _gradeRepoMock.Verify(r => r.AddAsync(It.IsAny<Grade>()), Times.Never);
        }
    }
}
