using System;
using System.Collections.Generic;
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

        private ExamSubmissionProcessor CreateProcessor()
        {
            _txMock.Setup(t => t.ExecuteAsync(It.IsAny<Func<Task>>(), default)).Returns<Func<Task>, System.Threading.CancellationToken>(async (f, ct) => await f());

            var timeMock = new Mock<ITimeProvider>();
            timeMock.Setup(t => t.UtcNow).Returns(DateTime.UtcNow);

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

            var submission = new ExamSubmission { Id = Guid.NewGuid(), Answers = new List<SubmissionAnswer>(), DeadlineAt = DateTime.UtcNow.AddMinutes(10) };
            _subRepoMock.Setup(r => r.GetByIdForUpdateAsync(submission.Id, true)).ReturnsAsync(submission);
            _examRepoMock.Setup(r => r.GetByIdAsync(submission.ExamId, true)).ReturnsAsync(new Exam { Name = "T", Questions = new List<Question>() });
            _gradingMock.Setup(g => g.GradeClosedQuestions(It.IsAny<Exam>(), It.IsAny<IReadOnlyList<SubmissionAnswer>>())).Returns(new Domain.Results.ClosedGradingResult { ClosedScore = 10, OpenMaxPoints = 0, MaxScore = 10, PassingScore = 6 });
            _gradingMock.Setup(g => g.EvaluateAfterSubmit(It.IsAny<Exam>(), It.IsAny<Domain.Results.ClosedGradingResult>())).Returns(new Domain.Results.SubmissionEvaluation { Status = SubmissionStatus.Graded, TotalScore = 10m, Passed = true });

            _subRepoMock.Setup(r => r.GetByIdAsync(submission.Id, true)).ReturnsAsync(submission);

            var res = await proc.ProcessSubmitAsync(submission.Id, new SubmitExamDto(), SubmitReason.Manual, null);

            _gradeRepoMock.Verify(g => g.AddAsync(It.IsAny<Grade>()), Times.Once);
            Assert.Equal(SubmissionStatus.Graded, res.Status);
        }

        // Additional tests for other scenarios should be added similarly; due to space add three representative tests.

        [Fact]
        public async Task OpenQuestions_PendingReview_NoGradeCreated()
        {
            var proc = CreateProcessor();
            var submission = new ExamSubmission { Id = Guid.NewGuid(), Answers = new List<SubmissionAnswer>(), DeadlineAt = DateTime.UtcNow.AddMinutes(10) };
            _subRepoMock.Setup(r => r.GetByIdForUpdateAsync(submission.Id, true)).ReturnsAsync(submission);
            _examRepoMock.Setup(r => r.GetByIdAsync(submission.ExamId, true)).ReturnsAsync(new Exam { Name = "T", Questions = new List<Question> { new Question { Id = Guid.NewGuid(), Type = QuestionType.Open, Points = 5, Text = "q" } } });
            _gradingMock.Setup(g => g.GradeClosedQuestions(It.IsAny<Exam>(), It.IsAny<IReadOnlyList<SubmissionAnswer>>())).Returns(new Domain.Results.ClosedGradingResult { ClosedScore = 0, OpenMaxPoints = 5, MaxScore = 5, PassingScore = 3 });
            _gradingMock.Setup(g => g.EvaluateAfterSubmit(It.IsAny<Exam>(), It.IsAny<Domain.Results.ClosedGradingResult>())).Returns(new Domain.Results.SubmissionEvaluation { Status = SubmissionStatus.PendingReview });

            _subRepoMock.Setup(r => r.GetByIdAsync(submission.Id, true)).ReturnsAsync(submission);

            var res = await proc.ProcessSubmitAsync(submission.Id, new SubmitExamDto(), SubmitReason.Manual, null);

            _gradeRepoMock.Verify(g => g.AddAsync(It.IsAny<Grade>()), Times.Never);
            Assert.Equal(SubmissionStatus.PendingReview, res.Status);
        }

        [Fact]
        public async Task FailedAuto_NoGradeCreated()
        {
            var proc = CreateProcessor();
            var submission = new ExamSubmission { Id = Guid.NewGuid(), Answers = new List<SubmissionAnswer>(), DeadlineAt = DateTime.UtcNow.AddMinutes(10) };
            _subRepoMock.Setup(r => r.GetByIdForUpdateAsync(submission.Id, true)).ReturnsAsync(submission);
            _examRepoMock.Setup(r => r.GetByIdAsync(submission.ExamId, true)).ReturnsAsync(new Exam { Name = "T", Questions = new List<Question>() });
            _gradingMock.Setup(g => g.GradeClosedQuestions(It.IsAny<Exam>(), It.IsAny<IReadOnlyList<SubmissionAnswer>>())).Returns(new Domain.Results.ClosedGradingResult { ClosedScore = 0, OpenMaxPoints = 0, MaxScore = 0, PassingScore = 1 });
            _gradingMock.Setup(g => g.EvaluateAfterSubmit(It.IsAny<Exam>(), It.IsAny<Domain.Results.ClosedGradingResult>())).Returns(new Domain.Results.SubmissionEvaluation { Status = SubmissionStatus.FailedAuto, TotalScore = 0m, Passed = false });

            _subRepoMock.Setup(r => r.GetByIdAsync(submission.Id, true)).ReturnsAsync(submission);

            var res = await proc.ProcessSubmitAsync(submission.Id, new SubmitExamDto(), SubmitReason.Manual, null);

            _gradeRepoMock.Verify(g => g.AddAsync(It.IsAny<Grade>()), Times.Never);
            Assert.Equal(SubmissionStatus.FailedAuto, res.Status);
        }

        [Fact]
        public async Task DoubleSubmit_NoDuplicateGradeCreated()
        {
            var proc = CreateProcessor();
            var submission = new ExamSubmission { Id = Guid.NewGuid(), Answers = new List<SubmissionAnswer>(), SubmittedAt = DateTime.UtcNow };
            submission.GradeId = Guid.NewGuid();

            _subRepoMock.Setup(r => r.GetByIdForUpdateAsync(submission.Id, true)).ReturnsAsync(submission);
            _subRepoMock.Setup(r => r.GetByIdAsync(submission.Id, true)).ReturnsAsync(submission);

            var res = await proc.ProcessSubmitAsync(submission.Id, null, SubmitReason.Manual, null);

            _gradeRepoMock.Verify(g => g.AddAsync(It.IsAny<Grade>()), Times.Never);
            Assert.NotNull(res);
        }

        [Fact]
        public async Task SubmitWithDifferentReason_NoReprocess()
        {
            var proc = CreateProcessor();
            var submission = new ExamSubmission { Id = Guid.NewGuid(), Answers = new List<SubmissionAnswer>(), SubmittedAt = DateTime.UtcNow };
            submission.SubmitReason = SubmitReason.Manual;

            _subRepoMock.Setup(r => r.GetByIdForUpdateAsync(submission.Id, true)).ReturnsAsync(submission);
            _subRepoMock.Setup(r => r.GetByIdAsync(submission.Id, true)).ReturnsAsync(submission);

            var res = await proc.ProcessSubmitAsync(submission.Id, null, SubmitReason.TimeExpired, null);

            _gradeRepoMock.Verify(g => g.AddAsync(It.IsAny<Grade>()), Times.Never);
            Assert.NotNull(res);
        }

        [Fact]
        public async Task RequiredMissing_ManualRejected()
        {
            var proc = CreateProcessor();

            var q1 = new Question { Id = Guid.NewGuid(), Required = true, Text = "q1" };
            var exam = new Exam { Name = "T", Questions = new List<Question> { q1 } };
            var submission = new ExamSubmission { Id = Guid.NewGuid(), Answers = new List<SubmissionAnswer>(), DeadlineAt = DateTime.UtcNow.AddMinutes(10) };

            _subRepoMock.Setup(r => r.GetByIdForUpdateAsync(submission.Id, true)).ReturnsAsync(submission);
            _examRepoMock.Setup(r => r.GetByIdAsync(submission.ExamId, true)).ReturnsAsync(exam);

            await Assert.ThrowsAsync<InvalidOperationException>(() => proc.ProcessSubmitAsync(submission.Id, new SubmitExamDto(), SubmitReason.Manual, null));
        }

        [Fact]
        public async Task RequiredMissing_AutoAllowed()
        {
            var proc = CreateProcessor();

            var q1 = new Question { Id = Guid.NewGuid(), Required = true, Text = "q1" };
            var exam = new Exam { Name = "T", Questions = new List<Question> { q1 } };
            var submission = new ExamSubmission { Id = Guid.NewGuid(), Answers = new List<SubmissionAnswer>() };

            _subRepoMock.Setup(r => r.GetByIdForUpdateAsync(submission.Id, true)).ReturnsAsync(submission);
            _examRepoMock.Setup(r => r.GetByIdAsync(submission.ExamId, true)).ReturnsAsync(exam);
            _gradingMock.Setup(g => g.GradeClosedQuestions(It.IsAny<Exam>(), It.IsAny<IReadOnlyList<SubmissionAnswer>>())).Returns(new Domain.Results.ClosedGradingResult { ClosedScore = 0, OpenMaxPoints = 0, MaxScore = 0, PassingScore = 0 });
            _gradingMock.Setup(g => g.EvaluateAfterSubmit(It.IsAny<Exam>(), It.IsAny<Domain.Results.ClosedGradingResult>())).Returns(new Domain.Results.SubmissionEvaluation { Status = SubmissionStatus.Graded, TotalScore = 0m, Passed = false });

            _subRepoMock.Setup(r => r.GetByIdAsync(submission.Id, true)).ReturnsAsync(submission);

            var res = await proc.ProcessSubmitAsync(submission.Id, null, SubmitReason.TimeExpired, null);

            Assert.Equal(SubmissionStatus.Graded, res.Status);
        }

        [Fact]
        public async Task AutoSubmit_NoDto_NoCaller_UsesPersistedAnswers()
        {
            var proc = CreateProcessor();
            var ans = new SubmissionAnswer { Id = Guid.NewGuid(), QuestionId = Guid.NewGuid() };
            var submission = new ExamSubmission { Id = Guid.NewGuid(), Answers = new List<SubmissionAnswer> { ans } };
            _subRepoMock.Setup(r => r.GetByIdForUpdateAsync(submission.Id, true)).ReturnsAsync(submission);
            _examRepoMock.Setup(r => r.GetByIdAsync(submission.ExamId, true)).ReturnsAsync(new Exam { Name = "T", Questions = new List<Question>() });
            _gradingMock.Setup(g => g.GradeClosedQuestions(It.IsAny<Exam>(), It.IsAny<IReadOnlyList<SubmissionAnswer>>())).Returns(new Domain.Results.ClosedGradingResult { ClosedScore = 0, OpenMaxPoints = 0, MaxScore = 0, PassingScore = 0 });
            _gradingMock.Setup(g => g.EvaluateAfterSubmit(It.IsAny<Exam>(), It.IsAny<Domain.Results.ClosedGradingResult>())).Returns(new Domain.Results.SubmissionEvaluation { Status = SubmissionStatus.Graded, TotalScore = 0m, Passed = false });

            _subRepoMock.Setup(r => r.GetByIdAsync(submission.Id, true)).ReturnsAsync(submission);

            var res = await proc.ProcessSubmitAsync(submission.Id, null, SubmitReason.TimeExpired, null);

            Assert.Equal(SubmissionStatus.Graded, res.Status);
        }

        [Fact]
        public async Task PersistenceError_RollsBack_AndThrows()
        {
            var proc = CreateProcessor();
            var submission = new ExamSubmission { Id = Guid.NewGuid(), Answers = new List<SubmissionAnswer>() };
            _subRepoMock.Setup(r => r.GetByIdForUpdateAsync(submission.Id, true)).ReturnsAsync(submission);
            _examRepoMock.Setup(r => r.GetByIdAsync(submission.ExamId, true)).ReturnsAsync(new Exam { Name = "T", Questions = new List<Question>() });
            _gradingMock.Setup(g => g.GradeClosedQuestions(It.IsAny<Exam>(), It.IsAny<IReadOnlyList<SubmissionAnswer>>())).Returns(new Domain.Results.ClosedGradingResult { ClosedScore = 10, OpenMaxPoints = 0, MaxScore = 10, PassingScore = 6 });
            _gradingMock.Setup(g => g.EvaluateAfterSubmit(It.IsAny<Exam>(), It.IsAny<Domain.Results.ClosedGradingResult>())).Returns(new Domain.Results.SubmissionEvaluation { Status = SubmissionStatus.Graded, TotalScore = 10m, Passed = true });

            _gradeRepoMock.Setup(g => g.AddAsync(It.IsAny<Grade>())).ThrowsAsync(new InvalidOperationException("DB error"));

            await Assert.ThrowsAsync<InvalidOperationException>(() => proc.ProcessSubmitAsync(submission.Id, new SubmitExamDto(), SubmitReason.Manual, null));
        }

        [Fact]
        public async Task DeadlineReached_NoNewAnswersButExistingProcessed()
        {
            var proc = CreateProcessor();
            var submission = new ExamSubmission { Id = Guid.NewGuid(), Answers = new List<SubmissionAnswer>(), DeadlineAt = DateTime.UtcNow.AddMinutes(-1) };
            _subRepoMock.Setup(r => r.GetByIdForUpdateAsync(submission.Id, true)).ReturnsAsync(submission);
            _examRepoMock.Setup(r => r.GetByIdAsync(submission.ExamId, true)).ReturnsAsync(new Exam { Name = "T", Questions = new List<Question>() });
            _gradingMock.Setup(g => g.GradeClosedQuestions(It.IsAny<Exam>(), It.IsAny<IReadOnlyList<SubmissionAnswer>>())).Returns(new Domain.Results.ClosedGradingResult { ClosedScore = 0, OpenMaxPoints = 0, MaxScore = 0, PassingScore = 0 });
            _gradingMock.Setup(g => g.EvaluateAfterSubmit(It.IsAny<Exam>(), It.IsAny<Domain.Results.ClosedGradingResult>())).Returns(new Domain.Results.SubmissionEvaluation { Status = SubmissionStatus.Graded, TotalScore = 0m, Passed = false });

            _subRepoMock.Setup(r => r.GetByIdAsync(submission.Id, true)).ReturnsAsync(submission);

            var res = await proc.ProcessSubmitAsync(submission.Id, null, SubmitReason.TimeExpired, null);

            Assert.Equal(SubmissionStatus.Graded, res.Status);
        }

        [Fact]
        public async Task OpenAnswersEmpty_TreatedAsZeroPoints()
        {
            var proc = CreateProcessor();
            var submission = new ExamSubmission { Id = Guid.NewGuid(), Answers = new List<SubmissionAnswer>() };
            _subRepoMock.Setup(r => r.GetByIdForUpdateAsync(submission.Id, true)).ReturnsAsync(submission);
            _examRepoMock.Setup(r => r.GetByIdAsync(submission.ExamId, true)).ReturnsAsync(new Exam { Name = "T", Questions = new List<Question>() });
            _gradingMock.Setup(g => g.GradeClosedQuestions(It.IsAny<Exam>(), It.IsAny<IReadOnlyList<SubmissionAnswer>>())).Returns(new Domain.Results.ClosedGradingResult { ClosedScore = 0, OpenMaxPoints = 0, MaxScore = 0, PassingScore = 0 });
            _gradingMock.Setup(g => g.EvaluateAfterSubmit(It.IsAny<Exam>(), It.IsAny<Domain.Results.ClosedGradingResult>())).Returns(new Domain.Results.SubmissionEvaluation { Status = SubmissionStatus.Graded, TotalScore = 0m, Passed = false });

            _subRepoMock.Setup(r => r.GetByIdAsync(submission.Id, true)).ReturnsAsync(submission);

            var res = await proc.ProcessSubmitAsync(submission.Id, null, SubmitReason.TimeExpired, null);

            Assert.Equal(SubmissionStatus.Graded, res.Status);
            Assert.Equal(0m, res.TotalScore);
        }
    }
}
