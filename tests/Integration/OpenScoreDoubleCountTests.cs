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
    public class OpenScoreDoubleCountTests
    {
        private readonly IntegrationFixture _fixture;

        public OpenScoreDoubleCountTests(IntegrationFixture fixture)
        {
            _fixture = fixture;
        }

        private Task<ScenarioIds> ResetToPendingReviewAsync() => _fixture.PreparePendingReviewScenarioAsync(IntegrationScenario.ExamRegression);

        [SkippableFact]
        public async Task OpenScore_IgnoresClosedAnswerAwardedPoints()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await ResetToPendingReviewAsync();

            using var scope = _fixture.ScopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IExamSubmissionRepository>();
            var mgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamSubmissionManager>();
            var gradeRepo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IGradeRepository>();

            var caller = new ClaimsContext(SeedData.TeacherUserId, UserRole.Teacher, null, SeedData.TeacherId);

            // At this scenario: ClosedScore = 4, openAnswer.AwardedPoints = 4 (intentional), open actual review will set open answer to 2
            // Ensure DB has ClosedScore = 4 already (seed sets it in ConcurrentFinalize scenario, but we use PreparePendingReview which sets open awarded points differently)

            // Force closed score to 4 in DB and set a closed answer AwardedPoints to 4 (simulate incorrect double count)
            using (var ctxScope = _fixture.ScopeFactory.CreateScope())
            {
                var ctx = ctxScope.ServiceProvider.GetRequiredService<ElectronicRegisterAPI.Infrastructure.Persistence.ElectronicRegisterContext>();
                var entity = await ctx.ExamSubmissions.FindAsync(ids.SubmissionId);
                entity.ClosedScore = 4m;
                await ctx.SaveChangesAsync();
            }

            // Now award points to the open answer
            var current = await repo.GetByIdAsync(ids.SubmissionId, withAnswers: true);
            var openAns = current!.Answers.First(a => a.QuestionId == ids.OpenQuestionId);

            var reviewDto = new ReviewOpenAnswersDto();
            reviewDto.Answers.Add(new OpenAnswerReviewInputDto { AnswerId = openAns.Id, AwardedPoints = 2m });
            await mgr.ReviewAsync(ids.SubmissionId, reviewDto, caller);

            var after = await repo.GetByIdAsync(ids.SubmissionId, withAnswers: true);
            Assert.Equal(2m, after!.OpenScore);
            Assert.Equal(6m, after.TotalScore); // 4 closed + 2 open
        }
    }
}
