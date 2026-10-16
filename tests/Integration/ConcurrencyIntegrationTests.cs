using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Infrastructure.Persistence;

namespace ElectronicRegisterAPI.Tests.Integration
{
    [Collection(IntegrationCollection.Name)]
    public class ConcurrencyIntegrationTests
    {
        private readonly IntegrationFixture _fixture;

        public ConcurrencyIntegrationTests(IntegrationFixture fixture)
        {
            _fixture = fixture;
        }

        [SkippableFact]
        public async Task ConcurrentSaveAnswers_HigherRevisionWins()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await _fixture.ResetScenarioAsync(IntegrationScenario.ConcurrentSaveAnswers);

            // Two independent scopes => two DbContexts working on the same submission
            using var scope1 = _fixture.ScopeFactory.CreateScope();
            using var scope2 = _fixture.ScopeFactory.CreateScope();

            var manager1 = scope1.ServiceProvider.GetRequiredService<IExamManager>();
            var manager2 = scope2.ServiceProvider.GetRequiredService<IExamManager>();

            var studentCaller = StudentCaller(ids);

            var dto1 = new SaveExamAnswersDto { ClientRevision = 1 };
            dto1.Answers.Add(new AnswerInputDto { QuestionId = ids.OpenQuestionId, OpenText = "v1" });

            var dto2 = new SaveExamAnswersDto { ClientRevision = 2 };
            dto2.Answers.Add(new AnswerInputDto { QuestionId = ids.OpenQuestionId, OpenText = "v2" });

            var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var t1 = Task.Run(async () => { await gate.Task; return await manager1.SaveAnswersAsync(ids.SubmissionId, dto1, studentCaller); });
            var t2 = Task.Run(async () => { await gate.Task; return await manager2.SaveAnswersAsync(ids.SubmissionId, dto2, studentCaller); });

            gate.SetResult(true);

            var results = await Task.WhenAll(t1, t2);
            Assert.Contains(results, r => r.Accepted && r.CurrentRevision == 2);

            // Verify final state: revision equals highest accepted revision and answers reflect it
            using var verifyScope = _fixture.ScopeFactory.CreateScope();
            var submission = await verifyScope.ServiceProvider.GetRequiredService<IExamSubmissionRepository>().GetByIdAsync(ids.SubmissionId, withAnswers: true);
            Assert.NotNull(submission);
            Assert.Equal(2, submission.Revision);
            var ans = Assert.Single(submission.Answers);
            Assert.Equal(ids.OpenQuestionId, ans.QuestionId);
            Assert.Equal("v2", ans.OpenText);
        }

        [SkippableFact]
        public async Task ConcurrentSubmit_OneDeliveryOnly()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await _fixture.ResetScenarioAsync(IntegrationScenario.ConcurrentSubmit);

            using var s1 = _fixture.ScopeFactory.CreateScope();
            using var s2 = _fixture.ScopeFactory.CreateScope();

            var m1 = s1.ServiceProvider.GetRequiredService<IExamManager>();
            var m2 = s2.ServiceProvider.GetRequiredService<IExamManager>();

            var studentCaller = StudentCaller(ids);

            // Submit re-saves the answers, so its revision must be greater than the persisted one (0)
            SubmitExamDto SubmitDto()
            {
                var dto = new SubmitExamDto { ClientRevision = 1 };
                dto.Answers.Add(new AnswerInputDto { QuestionId = ids.OpenQuestionId, OpenText = "consegna" });
                dto.Answers.Add(new AnswerInputDto { QuestionId = ids.SingleQuestionId, SelectedOptionIds = { ids.CorrectOptionId } });
                return dto;
            }

            var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            // The losing submit either returns the persisted result (idempotent) or is rejected because the
            // submission is no longer in progress; any other exception is a real failure and propagates.
            var t1 = Task.Run(async () => { await gate.Task; return await SubmitOrRejectedAsync(m1, ids.SubmissionId, SubmitDto(), studentCaller); });
            var t2 = Task.Run(async () => { await gate.Task; return await SubmitOrRejectedAsync(m2, ids.SubmissionId, SubmitDto(), studentCaller); });

            gate.SetResult(true);

            var results = await Task.WhenAll(t1, t2);
            var delivered = results.Where(r => r != null).Select(r => r!).ToList();
            Assert.NotEmpty(delivered);

            using var verifyScope = _fixture.ScopeFactory.CreateScope();
            var repo = verifyScope.ServiceProvider.GetRequiredService<IExamSubmissionRepository>();
            var updated = await repo.GetByIdAsync(ids.SubmissionId, withAnswers: true);
            Assert.NotNull(updated);
            Assert.NotNull(updated.SubmittedAt);
            Assert.Equal(SubmissionStatus.PendingReview, updated.Status);
            Assert.All(delivered, r => Assert.Equal(updated.SubmittedAt, r.SubmittedAt));

            // Exactly one submit was processed
            var ctx = verifyScope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
            var submitEvents = await ctx.AuditEvents.CountAsync(a => a.EntityId == ids.SubmissionId && a.Action == "submission.submit");
            Assert.Equal(1, submitEvents);

            // The exam has an open question, so the submit leaves it pending review without creating a grade
            var gradeRepo = verifyScope.ServiceProvider.GetRequiredService<IGradeRepository>();
            var grades = await gradeRepo.GetByStudentIdAsync(ids.StudentId);
            Assert.Empty(grades);
        }

        [SkippableFact]
        public async Task AutosaveAfterDeadline_NoWrite()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            // Seeded in progress with the deadline already in the past
            var ids = await _fixture.ResetScenarioAsync(IntegrationScenario.AutosaveAfterDeadline);

            using var s = _fixture.ScopeFactory.CreateScope();
            var manager = s.ServiceProvider.GetRequiredService<IExamManager>();

            var dto = new SaveExamAnswersDto { ClientRevision = 99 };
            dto.Answers.Add(new AnswerInputDto { QuestionId = ids.OpenQuestionId, OpenText = "late" });

            var res = await manager.SaveAnswersAsync(ids.SubmissionId, dto, StudentCaller(ids));
            // Should be rejected due to deadline
            Assert.False(res.Accepted);

            using var verifyScope = _fixture.ScopeFactory.CreateScope();
            var submission = await verifyScope.ServiceProvider.GetRequiredService<IExamSubmissionRepository>().GetByIdAsync(ids.SubmissionId, withAnswers: true);
            Assert.NotNull(submission);
            Assert.Equal(0, submission.Revision);
            Assert.Equal(SubmissionStatus.InProgress, submission.Status);
            var ans = submission.Answers.Single(a => a.QuestionId == ids.OpenQuestionId);
            Assert.Equal(SeedData.InitialOpenText, ans.OpenText);
        }

        [SkippableFact]
        public async Task AutosaveConcurrentToSubmit_NoAnswerSavedAfterSubmit()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await _fixture.ResetScenarioAsync(IntegrationScenario.AutosaveConcurrentToSubmit);

            using var scope1 = _fixture.ScopeFactory.CreateScope();
            using var scope2 = _fixture.ScopeFactory.CreateScope();

            var managerSave = scope1.ServiceProvider.GetRequiredService<IExamManager>();
            var managerSubmit = scope2.ServiceProvider.GetRequiredService<IExamManager>();

            var studentCaller = StudentCaller(ids);

            // The autosave carries an older revision than the submit: if it reaches the database after the
            // submit it must be discarded, if it gets there first the submit overwrites it.
            var saveDto = new SaveExamAnswersDto { ClientRevision = 1 };
            saveDto.Answers.Add(new AnswerInputDto { QuestionId = ids.OpenQuestionId, OpenText = "autosave" });

            var submitDto = new SubmitExamDto { ClientRevision = 2 };
            submitDto.Answers.Add(new AnswerInputDto { QuestionId = ids.OpenQuestionId, OpenText = "final" });

            var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var tSave = Task.Run(async () =>
            {
                await gate.Task;
                try { return (bool?)(await managerSave.SaveAnswersAsync(ids.SubmissionId, saveDto, studentCaller)).Accepted; }
                catch (BusinessRuleException) { return null; } // submission already submitted
            });
            var tSubmit = Task.Run(async () => { await gate.Task; return await managerSubmit.SubmitAsync(ids.SubmissionId, submitDto, studentCaller); });

            gate.SetResult(true);

            await Task.WhenAll(tSave, tSubmit);
            var submitResult = await tSubmit;
            Assert.NotNull(submitResult.SubmittedAt);

            using var verifyScope = _fixture.ScopeFactory.CreateScope();
            var repo = verifyScope.ServiceProvider.GetRequiredService<IExamSubmissionRepository>();
            var updated = await repo.GetByIdAsync(ids.SubmissionId, withAnswers: true);
            Assert.NotNull(updated);
            Assert.NotNull(updated.SubmittedAt);
            Assert.NotEqual(SubmissionStatus.InProgress, updated.Status);
            Assert.Equal(2, updated.Revision);

            // Whatever the interleaving, the persisted answer is the submitted one
            var ans = Assert.Single(updated.Answers);
            Assert.Equal(ids.OpenQuestionId, ans.QuestionId);
            Assert.Equal("final", ans.OpenText);
        }

        private static ClaimsContext StudentCaller(ScenarioIds ids) =>
            new ClaimsContext(Guid.NewGuid(), UserRole.Student, ids.StudentId, null);

        private static async Task<SubmissionResultDto?> SubmitOrRejectedAsync(IExamManager manager, Guid submissionId, SubmitExamDto dto, ClaimsContext caller)
        {
            try
            {
                return await manager.SubmitAsync(submissionId, dto, caller);
            }
            catch (BusinessRuleException)
            {
                return null;
            }
        }
    }
}
