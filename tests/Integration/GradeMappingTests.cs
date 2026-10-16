using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ElectronicRegisterAPI.Domain.DTOs;
using Microsoft.EntityFrameworkCore;
using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Tests.Integration
{
    [Collection(IntegrationCollection.Name)]
    public class GradeMappingTests
    {
        private readonly IntegrationFixture _fixture;

        public GradeMappingTests(IntegrationFixture fixture)
        {
            _fixture = fixture;
        }

        private Task<ScenarioIds> ResetAsync() => _fixture.ResetScenarioAsync(IntegrationScenario.ExamRegression);
        private Task<ScenarioIds> ResetToPendingReviewAsync() => _fixture.PreparePendingReviewScenarioAsync(IntegrationScenario.ExamRegression);
        private Task<ScenarioIds> ResetToClosedOnlyAsync() => _fixture.ResetScenarioAsync(IntegrationScenario.ClosedOnly);

        [SkippableFact]
        public async Task Result_ClosedOnly_ReturnsPersistedOfficialGrade()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await ResetToClosedOnlyAsync();

            using var scope = _fixture.ScopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IExamSubmissionRepository>();
            var gradeRepo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IGradeRepository>();
            var mgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamManager>();

            // Finalize as teacher to create Grade from persisted scores
            var teacherCaller = new ClaimsContext(Guid.NewGuid(), UserRole.Teacher, null, SeedData.TeacherId);
            // Submit answers as student, then allow automatic grading to create Grade
            var examMgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamManager>();
            var studentCaller = new ClaimsContext(Guid.NewGuid(), UserRole.Student, ids.StudentId, null);
            var submit = new SubmitExamDto { ClientRevision = 1 };
            // populate answers from seed
            var current = await repo.GetByIdAsync(ids.SubmissionId, withAnswers: true);
            foreach (var a in current!.Answers)
            {
                if (a.OpenText != null)
                {
                    submit.Answers.Add(new AnswerInputDto { QuestionId = a.QuestionId, OpenText = a.OpenText });
                }
                else if (a.SelectedOptionIds != null && a.SelectedOptionIds.Count > 0)
                {
                    var ai = new AnswerInputDto { QuestionId = a.QuestionId };
                    foreach (var oid in a.SelectedOptionIds) ai.SelectedOptionIds.Add(oid);
                    submit.Answers.Add(ai);
                }
            }

            await examMgr.SubmitAsync(ids.SubmissionId, submit, studentCaller);

            // call GetResult and compare DTO Grade with value read from fresh DbContext
            var result = await mgr.GetResultAsync(ids.SubmissionId, teacherCaller);
            Assert.NotNull(result);

            using var verifyScope = _fixture.ScopeFactory.CreateScope();
            var ctx = verifyScope.ServiceProvider.GetRequiredService<ElectronicRegisterAPI.Infrastructure.Persistence.ElectronicRegisterContext>();
            var persistedGrades = await ctx.Grades.Where(g => g.StudentId == ids.StudentId).ToListAsync();
            Assert.NotEmpty(persistedGrades);
            var persisted = persistedGrades.FirstOrDefault(g => g.Value == result.Grade);
            Assert.NotNull(persisted);
        }

        [SkippableFact]
        public async Task Result_ManualFinalization_ReturnsPersistedOfficialGrade()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await ResetToPendingReviewAsync();

            using var scope = _fixture.ScopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IExamSubmissionRepository>();
            var mgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamManager>();

            var teacherCaller = new ClaimsContext(Guid.NewGuid(), UserRole.Teacher, null, SeedData.TeacherId);

            // Finalize with explicit dto.FinalGrade
            var dto = new FinalizeSubmissionDto { FinalGrade = 7.5m };
            var submissionMgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamSubmissionManager>();
            // Ensure all open answers are reviewed before finalization
            var toReview = await repo.GetByIdAsync(ids.SubmissionId, withAnswers: true);
            var reviewDto = new ReviewOpenAnswersDto();
            foreach (var a in toReview!.Answers.Where(x => x.OpenText != null))
            {
                reviewDto.Answers.Add(new OpenAnswerReviewInputDto { AnswerId = a.Id, AwardedPoints = 5m });
            }
            if (reviewDto.Answers.Count > 0)
                await submissionMgr.ReviewAsync(ids.SubmissionId, reviewDto, new ClaimsContext(Guid.NewGuid(), UserRole.Teacher, null, SeedData.TeacherId));

            await submissionMgr.FinalizeAsync(ids.SubmissionId, dto, new ClaimsContext(Guid.NewGuid(), UserRole.Teacher, null, SeedData.TeacherId));

            var result = await mgr.GetResultAsync(ids.SubmissionId, teacherCaller);
            Assert.NotNull(result);

            using var verifyScope = _fixture.ScopeFactory.CreateScope();
            var ctx = verifyScope.ServiceProvider.GetRequiredService<ElectronicRegisterAPI.Infrastructure.Persistence.ElectronicRegisterContext>();
            var persistedGrades = await ctx.Grades.Where(g => g.StudentId == ids.StudentId).ToListAsync();
            Assert.NotEmpty(persistedGrades);
            var persisted = persistedGrades.FirstOrDefault(g => g.Value == result.Grade);
            Assert.NotNull(persisted);
        }

        [SkippableFact]
        public async Task Result_PendingReview_HasNoOfficialGrade()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await ResetToPendingReviewAsync();

            using var scope = _fixture.ScopeFactory.CreateScope();
            var mgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamManager>();
            var caller = new ClaimsContext(Guid.NewGuid(), UserRole.Teacher, null, SeedData.TeacherId);

            var result = await mgr.GetResultAsync(ids.SubmissionId, caller);
            Assert.NotNull(result);
            Assert.Null(result.Grade);
        }

        [SkippableFact]
        public async Task Submit_Idempotent_ReturnsPersistedOfficialGrade()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await ResetToPendingReviewAsync();

            using var scope = _fixture.ScopeFactory.CreateScope();
            var mgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamManager>();
            var submissionProcessor = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Services.IExamSubmissionProcessor>();
            var repo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IExamSubmissionRepository>();
            var caller = new ClaimsContext(Guid.NewGuid(), UserRole.Student, ids.StudentId, null);

            // perform initial submit (idempotent path should create grade if no open questions remain)
            var submissionMgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamSubmissionManager>();

            // Ensure open answers are reviewed before finalization so FinalizeAsync won't throw
            var toReview2 = await repo.GetByIdAsync(ids.SubmissionId, withAnswers: true);
            var reviewDto2 = new ReviewOpenAnswersDto();
            foreach (var a in toReview2!.Answers.Where(x => x.OpenText != null))
            {
                reviewDto2.Answers.Add(new OpenAnswerReviewInputDto { AnswerId = a.Id, AwardedPoints = 5m });
            }
            if (reviewDto2.Answers.Count > 0)
                await submissionMgr.ReviewAsync(ids.SubmissionId, reviewDto2, new ClaimsContext(Guid.NewGuid(), UserRole.Teacher, null, SeedData.TeacherId));

            await submissionMgr.FinalizeAsync(ids.SubmissionId, new FinalizeSubmissionDto(), new ClaimsContext(Guid.NewGuid(), UserRole.Teacher, null, SeedData.TeacherId));

            // Now call processor.ProcessSubmitAsync which should return persisted grade value when submission already submitted
            var res = await submissionProcessor.ProcessSubmitAsync(ids.SubmissionId, null, Domain.Enums.SubmitReason.TimeExpired, caller);
            Assert.NotNull(res);

            using var verifyScope = _fixture.ScopeFactory.CreateScope();
            var ctx = verifyScope.ServiceProvider.GetRequiredService<ElectronicRegisterAPI.Infrastructure.Persistence.ElectronicRegisterContext>();
            var persistedGrades = await ctx.Grades.Where(g => g.StudentId == ids.StudentId).ToListAsync();
            Assert.NotEmpty(persistedGrades);
            var persisted = persistedGrades.FirstOrDefault(g => g.Value == res.Grade);
            Assert.NotNull(persisted);
        }
    }
}
