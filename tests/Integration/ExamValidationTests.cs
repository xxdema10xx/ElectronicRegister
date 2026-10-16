using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;

namespace ElectronicRegisterAPI.Tests.Integration
{
    [Collection(IntegrationCollection.Name)]
    public class ExamValidationTests
    {
        private readonly IntegrationFixture _fixture;

        public ExamValidationTests(IntegrationFixture fixture)
        {
            _fixture = fixture;
        }

        private ClaimsContext StudentCaller(ScenarioIds ids) => new ClaimsContext(Guid.NewGuid(), UserRole.Student, ids.StudentId, null);
        private ClaimsContext TeacherCaller(Guid teacherId) => new ClaimsContext(Guid.NewGuid(), UserRole.Teacher, null, teacherId);

        [SkippableFact]
        public async Task Review_InProgress_IsRejected()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await _fixture.ResetScenarioAsync(IntegrationScenario.ExamRegression);

            using var scope = _fixture.ScopeFactory.CreateScope();
            var mgr = scope.ServiceProvider.GetRequiredService<IExamSubmissionManager>();
            var repo = scope.ServiceProvider.GetRequiredService<IExamSubmissionRepository>();

            var caller = TeacherCaller(SeedData.TeacherId);

            var subBefore = await repo.GetByIdAsync(ids.SubmissionId, withAnswers: true);

            var dto = new ReviewOpenAnswersDto();
            dto.Answers.Add(new OpenAnswerReviewInputDto { AnswerId = subBefore!.Answers.First(a => a.QuestionId == ids.OpenQuestionId).Id, AwardedPoints = 1m });

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await mgr.ReviewAsync(ids.SubmissionId, dto, caller));

            var subAfter = await repo.GetByIdAsync(ids.SubmissionId, withAnswers: true);
            Assert.NotNull(subAfter);
            // ensure nothing changed: awarded points remain null
            Assert.All(subAfter!.Answers, a => Assert.Null(a.AwardedPoints));
            Assert.Equal(subBefore.Revision, subAfter.Revision);
            Assert.Null(subAfter.SubmittedAt);
            Assert.Equal(SubmissionStatus.InProgress, subAfter.Status);
        }

        [SkippableFact]
        public async Task Review_Finalized_IsRejectedWithoutChanges()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await _fixture.ResetScenarioAsync(IntegrationScenario.ExamRegression);

            // Submit as student then finalize as teacher
            using var s1 = _fixture.ScopeFactory.CreateScope();
            var examMgr = s1.ServiceProvider.GetRequiredService<IExamManager>();
            var studentCaller = StudentCaller(ids);

            var submitDto = new SubmitExamDto { ClientRevision = 1 };
            submitDto.Answers.Add(new AnswerInputDto { QuestionId = ids.OpenQuestionId, OpenText = "finalize" });
            submitDto.Answers.Add(new AnswerInputDto { QuestionId = ids.SingleQuestionId, SelectedOptionIds = { ids.CorrectOptionId } });

            await examMgr.SubmitAsync(ids.SubmissionId, submitDto, studentCaller);

            using var s2 = _fixture.ScopeFactory.CreateScope();
            var mgr = s2.ServiceProvider.GetRequiredService<IExamSubmissionManager>();
            var repo = s2.ServiceProvider.GetRequiredService<IExamSubmissionRepository>();
            var gradeRepo = s2.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IGradeRepository>();
            var teacherCaller = TeacherCaller(SeedData.TeacherId);

            // Perform a valid review of open answers before finalizing so FinalizeAsync will succeed
            var currentBeforeReview = await repo.GetByIdAsync(ids.SubmissionId, withAnswers: true);
            var preReviewDto = new ReviewOpenAnswersDto();
            preReviewDto.Answers.Add(new OpenAnswerReviewInputDto { AnswerId = currentBeforeReview!.Answers.First(a => a.QuestionId == ids.OpenQuestionId).Id, AwardedPoints = 5m });
            await mgr.ReviewAsync(ids.SubmissionId, preReviewDto, teacherCaller);

            // Finalize
            await mgr.FinalizeAsync(ids.SubmissionId, new FinalizeSubmissionDto(), teacherCaller);

            var before = await repo.GetByIdAsync(ids.SubmissionId, withAnswers: true);

            var reviewDto = new ReviewOpenAnswersDto();
            // attempt to change open answer
            reviewDto.Answers.Add(new OpenAnswerReviewInputDto { AnswerId = before!.Answers.First(a => a.QuestionId == ids.OpenQuestionId).Id, AwardedPoints = 2m });

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await mgr.ReviewAsync(ids.SubmissionId, reviewDto, teacherCaller));

            var after = await repo.GetByIdAsync(ids.SubmissionId, withAnswers: true);
            Assert.NotNull(after);
            // ensure grade preserved and no awarded points changed by review
            Assert.NotNull(after!.GradeId);
            Assert.Equal(before.Answers.First(a => a.QuestionId == ids.OpenQuestionId).AwardedPoints, after.Answers.First(a => a.QuestionId == ids.OpenQuestionId).AwardedPoints);

            // Verify with a fresh DbContext that grade and submission persisted correctly
            using var verifyScope = _fixture.ScopeFactory.CreateScope();
            var verifyRepo = verifyScope.ServiceProvider.GetRequiredService<IExamSubmissionRepository>();
            var verifyGradeRepo = verifyScope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IGradeRepository>();
            var persisted = await verifyRepo.GetByIdAsync(ids.SubmissionId, withAnswers: true);
            Assert.Equal(SubmissionStatus.Finalized, persisted!.Status);
            Assert.NotNull(persisted.GradeId);
            var grades = await verifyGradeRepo.GetByStudentIdAsync(ids.StudentId);
            Assert.Contains(grades, g => g.Id == persisted.GradeId.Value);
        }

        [SkippableFact]
        public async Task Finalize_InProgress_IsRejectedWithoutGrade()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await _fixture.ResetScenarioAsync(IntegrationScenario.ExamRegression);

            using var scope = _fixture.ScopeFactory.CreateScope();
            var mgr = scope.ServiceProvider.GetRequiredService<IExamSubmissionManager>();
            var repo = scope.ServiceProvider.GetRequiredService<IExamSubmissionRepository>();
            var gradeRepo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IGradeRepository>();

            var caller = TeacherCaller(SeedData.TeacherId);

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await mgr.FinalizeAsync(ids.SubmissionId, new FinalizeSubmissionDto(), caller));

            var sub = await repo.GetByIdAsync(ids.SubmissionId, withAnswers: true);
            Assert.NotNull(sub);
            Assert.Null(sub.GradeId);
            var grades = await gradeRepo.GetByStudentIdAsync(ids.StudentId);
            Assert.Empty(grades);
        }

        [SkippableFact]
        public async Task Autosave_ForeignQuestion_IsRejected()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await _fixture.ResetScenarioAsync(IntegrationScenario.ExamRegression);

            using var scope = _fixture.ScopeFactory.CreateScope();
            var manager = scope.ServiceProvider.GetRequiredService<IExamManager>();
            var repo = scope.ServiceProvider.GetRequiredService<IExamSubmissionRepository>();

            var dto = new SaveExamAnswersDto { ClientRevision = 1 };
            dto.Answers.Add(new AnswerInputDto { QuestionId = Guid.NewGuid(), OpenText = "x" }); // foreign question

            var before = await repo.GetByIdAsync(ids.SubmissionId, withAnswers: true);

            await Assert.ThrowsAsync<KeyNotFoundException>(async () => await manager.SaveAnswersAsync(ids.SubmissionId, dto, StudentCaller(ids)));

            var after = await repo.GetByIdAsync(ids.SubmissionId, withAnswers: true);
            Assert.NotNull(after);
            Assert.Equal(before!.Revision, after.Revision);
            // ensure persisted answers unchanged
            Assert.Equal(before.Answers.Count, after.Answers.Count);
        }

        [SkippableFact]
        public async Task Autosave_ForeignOption_IsRejected()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await _fixture.ResetScenarioAsync(IntegrationScenario.ExamRegression);

            using var scope = _fixture.ScopeFactory.CreateScope();
            var manager = scope.ServiceProvider.GetRequiredService<IExamManager>();
            var repo = scope.ServiceProvider.GetRequiredService<IExamSubmissionRepository>();

            var dto = new SaveExamAnswersDto { ClientRevision = 1 };
            // select an option that does not belong to the open question (belongs to single question) but assign it to the open question -> foreign option
            dto.Answers.Add(new AnswerInputDto { QuestionId = ids.OpenQuestionId, SelectedOptionIds = { ids.CorrectOptionId } });

            var before = await repo.GetByIdAsync(ids.SubmissionId, withAnswers: true);

            await Assert.ThrowsAsync<KeyNotFoundException>(async () => await manager.SaveAnswersAsync(ids.SubmissionId, dto, StudentCaller(ids)));

            var after = await repo.GetByIdAsync(ids.SubmissionId, withAnswers: true);
            Assert.NotNull(after);
            Assert.Equal(before!.Revision, after.Revision);
            Assert.Equal(before.Answers.Count, after.Answers.Count);
        }

        [SkippableFact]
        public async Task Autosave_DuplicateQuestion_IsRejected()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await _fixture.ResetScenarioAsync(IntegrationScenario.ExamRegression);

            using var scope = _fixture.ScopeFactory.CreateScope();
            var manager = scope.ServiceProvider.GetRequiredService<IExamManager>();
            var repo = scope.ServiceProvider.GetRequiredService<IExamSubmissionRepository>();

            var dto = new SaveExamAnswersDto { ClientRevision = 1 };
            dto.Answers.Add(new AnswerInputDto { QuestionId = ids.OpenQuestionId, OpenText = "a" });
            dto.Answers.Add(new AnswerInputDto { QuestionId = ids.OpenQuestionId, OpenText = "b" }); // duplicate

            var before = await repo.GetByIdAsync(ids.SubmissionId, withAnswers: true);

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await manager.SaveAnswersAsync(ids.SubmissionId, dto, StudentCaller(ids)));

            var after = await repo.GetByIdAsync(ids.SubmissionId, withAnswers: true);
            Assert.NotNull(after);
            Assert.Equal(before!.Revision, after.Revision);
            Assert.Equal(before.Answers.Count, after.Answers.Count);
        }

        [SkippableFact]
        public async Task Submit_InvalidAnswerSnapshot_RollsBack()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await _fixture.ResetScenarioAsync(IntegrationScenario.ExamRegression);

            using var scope = _fixture.ScopeFactory.CreateScope();
            var manager = scope.ServiceProvider.GetRequiredService<IExamManager>();
            var repo = scope.ServiceProvider.GetRequiredService<IExamSubmissionRepository>();

            // Build submit DTO with invalid snapshot (foreign question)
            var submitDto = new SubmitExamDto { ClientRevision = 1 };
            submitDto.Answers.Add(new AnswerInputDto { QuestionId = Guid.NewGuid(), OpenText = "x" });

            var before = await repo.GetByIdAsync(ids.SubmissionId, withAnswers: true);

            await Assert.ThrowsAsync<KeyNotFoundException>(async () => await manager.SubmitAsync(ids.SubmissionId, submitDto, StudentCaller(ids)));

            var after = await repo.GetByIdAsync(ids.SubmissionId, withAnswers: true);
            Assert.NotNull(after);
            // ensure submission not marked submitted and answers unchanged
            Assert.Null(after.SubmittedAt);
            Assert.Equal(before!.Revision, after.Revision);
            Assert.Equal(before.Answers.Count, after.Answers.Count);
        }

        [SkippableFact]
        public async Task Review_ClosedQuestion_IsRejected()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await _fixture.ResetScenarioAsync(IntegrationScenario.ExamRegression);

            // Submit first so review is allowed for submitted submissions
            using var s = _fixture.ScopeFactory.CreateScope();
            var examMgr = s.ServiceProvider.GetRequiredService<IExamManager>();
            var studentCaller = StudentCaller(ids);

            var submit = new SubmitExamDto { ClientRevision = 1 };
            submit.Answers.Add(new AnswerInputDto { QuestionId = ids.OpenQuestionId, OpenText = "x" });
            submit.Answers.Add(new AnswerInputDto { QuestionId = ids.SingleQuestionId, SelectedOptionIds = { ids.CorrectOptionId } });
            await examMgr.SubmitAsync(ids.SubmissionId, submit, studentCaller);

            using var scope = _fixture.ScopeFactory.CreateScope();
            var mgr = scope.ServiceProvider.GetRequiredService<IExamSubmissionManager>();
            var repo = scope.ServiceProvider.GetRequiredService<IExamSubmissionRepository>();
            var teacherCaller = TeacherCaller(SeedData.TeacherId);

            var current = await repo.GetByIdAsync(ids.SubmissionId, withAnswers: true);
            var singleAnswer = current!.Answers.First(a => a.QuestionId == ids.SingleQuestionId);

            var reviewDto = new ReviewOpenAnswersDto();
            reviewDto.Answers.Add(new OpenAnswerReviewInputDto { AnswerId = singleAnswer.Id, AwardedPoints = 1m });

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await mgr.ReviewAsync(ids.SubmissionId, reviewDto, teacherCaller));

            var after = await repo.GetByIdAsync(ids.SubmissionId, withAnswers: true);
            Assert.NotNull(after);
            // ensure no awarded points changed for closed question
            Assert.Null(after!.Answers.First(a => a.QuestionId == ids.SingleQuestionId).AwardedPoints);
        }

        [SkippableFact]
        public async Task ConcurrentReviewAndFinalize_PreservesFinalizedGrade()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await _fixture.ResetScenarioAsync(IntegrationScenario.ConcurrentFinalize);

            using var s1 = _fixture.ScopeFactory.CreateScope();
            using var s2 = _fixture.ScopeFactory.CreateScope();

            var manager1 = s1.ServiceProvider.GetRequiredService<IExamSubmissionManager>();
            var manager2 = s2.ServiceProvider.GetRequiredService<IExamSubmissionManager>();
            var repo1 = s1.ServiceProvider.GetRequiredService<IExamSubmissionRepository>();
            var gradeRepo = s1.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IGradeRepository>();

            var teacherCaller = TeacherCaller(SeedData.TeacherId);

            var reviewDto = new ReviewOpenAnswersDto();
            reviewDto.Answers.Add(new OpenAnswerReviewInputDto { AnswerId = ids.OpenAnswerId, AwardedPoints = 5m });

            var t1 = Task.Run(async () =>
            {
                try { await manager1.ReviewAsync(ids.SubmissionId, reviewDto, teacherCaller); return (Exception?)null; }
                catch (Exception ex) { return ex; }
            });

            var t2 = Task.Run(async () =>
            {
                try { await manager2.FinalizeAsync(ids.SubmissionId, new FinalizeSubmissionDto(), teacherCaller); return (Exception?)null; }
                catch (Exception ex) { return ex; }
            });

            var results = await Task.WhenAll(t1, t2);
            // Both operations should either succeed or one succeed and the other return cleanly due to idempotency/guards

            using var verifyScope = _fixture.ScopeFactory.CreateScope();
            var final = await verifyScope.ServiceProvider.GetRequiredService<IExamSubmissionRepository>().GetByIdAsync(ids.SubmissionId, withAnswers: true);
            Assert.NotNull(final);
            Assert.Equal(SubmissionStatus.Finalized, final!.Status);
            Assert.NotNull(final.GradeId);

            var grades = await gradeRepo.GetByStudentIdAsync(ids.StudentId);
            // exactly one grade created
            Assert.Single(grades);
            Assert.Equal(final.GradeId, grades[0].Id);
        }
    }
}
