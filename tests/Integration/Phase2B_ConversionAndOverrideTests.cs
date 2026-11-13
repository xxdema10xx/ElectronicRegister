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
    public class Phase2B_ConversionAndOverrideTests
    {
        private readonly IntegrationFixture _fixture;
        public Phase2B_ConversionAndOverrideTests(IntegrationFixture fixture) { _fixture = fixture; }

        [SkippableFact]
        public async Task ClosedOnly_60Percent_CreatesGradeSix()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await _fixture.ResetScenarioAsync(IntegrationScenario.ClosedOnly);

            using var scope = _fixture.ScopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IExamSubmissionRepository>();
            var examMgr = scope.ServiceProvider.GetRequiredService<FixtureExamClient>();
            var teacher = new FixtureActor(SeedData.TeacherUserId, null, SeedData.TeacherId);
            var student = new FixtureActor(ids.StudentId, ids.StudentId, null);

            // Submit as student
            var current = await repo.GetByIdAsync(ids.SubmissionId, withAnswers: true);
            var submit = new SubmitExamDto { ClientRevision = current!.Revision + 1 };
            foreach (var a in current.Answers)
            {
                if (a.OpenText != null) submit.Answers.Add(new AnswerInputDto { QuestionId = a.QuestionId, OpenText = a.OpenText });
                else if (a.SelectedOptionIds?.Any() == true)
                {
                    var ai = new AnswerInputDto { QuestionId = a.QuestionId };
                    foreach (var oid in a.SelectedOptionIds) ai.SelectedOptionIds.Add(oid);
                    submit.Answers.Add(ai);
                }
            }
            var res = await examMgr.SubmitAsync(ids.SubmissionId, submit, student);
            Assert.NotNull(res);

            // Inspect persisted grade
            using var verify = _fixture.ScopeFactory.CreateScope();
            var ctx = verify.ServiceProvider.GetRequiredService<ElectronicRegisterAPI.Infrastructure.Persistence.ElectronicRegisterContext>();
            var grades = await ctx.Grades.Where(g => g.StudentId == ids.StudentId).ToListAsync();
            Assert.NotEmpty(grades);
            var g = grades.First();
            Assert.Equal(6.00m, g.Value);
        }

        [SkippableFact]
        public async Task FullyReviewed_CalculatesAndPersistsSuggestedGrade()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await _fixture.PreparePendingReviewScenarioAsync(IntegrationScenario.ExamRegression);

            using var scope = _fixture.ScopeFactory.CreateScope();
            var mgr = scope.ServiceProvider.GetRequiredService<FixtureSubmissionClient>();
            var repo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IExamSubmissionRepository>();
            var teacher = new FixtureActor(SeedData.TeacherUserId, null, SeedData.TeacherId);

            var before = await repo.GetByIdAsync(ids.SubmissionId, withAnswers: true);
            var review = new ReviewOpenAnswersDto();
            foreach (var a in before!.Answers.Where(x => x.OpenText != null))
                review.Answers.Add(new OpenAnswerReviewInputDto { AnswerId = a.Id, AwardedPoints = a.QuestionId == Guid.Empty ? 0m : 5m });

            if (review.Answers.Count > 0) await mgr.ReviewAsync(ids.SubmissionId, review, teacher);

            using var verify = _fixture.ScopeFactory.CreateScope();
            var vrepo = verify.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IExamSubmissionRepository>();
            var persisted = await vrepo.GetByIdAsync(ids.SubmissionId, withAnswers: true);
            Assert.NotNull(persisted!.SuggestedGrade);
        }

    }
}
