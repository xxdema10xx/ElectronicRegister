using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Tests.Integration
{
    [Collection(IntegrationCollection.Name)]
    public class ExamRegressionTests
    {
        private readonly IntegrationFixture _fixture;

        public ExamRegressionTests(IntegrationFixture fixture)
        {
            _fixture = fixture;
        }

        private ClaimsContext TeacherCaller(Guid teacherId) => new ClaimsContext(Guid.NewGuid(), UserRole.Teacher, null, teacherId);
        private ClaimsContext AdminCaller() => new ClaimsContext(Guid.NewGuid(), UserRole.Admin, null, null);

        // Every test starts from a fresh in-progress submission (one open + one single-choice answer).
        // A new submission cannot be added next to it: (exam_id, student_id) is unique.
        private Task<ScenarioIds> ResetAsync() => _fixture.ResetScenarioAsync(IntegrationScenario.ExamRegression);
        private Task<ScenarioIds> ResetToPendingReviewAsync() => _fixture.PreparePendingReviewScenarioAsync(IntegrationScenario.ExamRegression);

        [SkippableFact]
        public async Task TeacherNonOwner_CannotReview()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await ResetToPendingReviewAsync();
            var subId = ids.SubmissionId;

            using var scope = _fixture.ScopeFactory.CreateScope();
            var mgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamSubmissionManager>();
            var repo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IExamSubmissionRepository>();
            var gradeRepo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IGradeRepository>();

            var caller = TeacherCaller(Guid.NewGuid()); // different teacher

            var beforeGrades = await gradeRepo.GetByStudentIdAsync(ids.StudentId);

            var current = await repo.GetByIdAsync(subId, withAnswers: true);
            var dto = new ReviewOpenAnswersDto();
            dto.Answers.Add(new OpenAnswerReviewInputDto { AnswerId = current!.Answers.First(a => a.QuestionId == ids.OpenQuestionId).Id, AwardedPoints = 1m });

            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await mgr.ReviewAsync(subId, dto, caller));

            var after = await repo.GetByIdAsync(subId, withAnswers: true);
            Assert.NotNull(after);
            // Ensure awarded points unchanged (null)
            Assert.All(after.Answers, a => Assert.Null(a.AwardedPoints));

            var afterGrades = await gradeRepo.GetByStudentIdAsync(ids.StudentId);
            Assert.Equal(beforeGrades.Count, afterGrades.Count);
        }

        [SkippableFact]
        public async Task TeacherNonOwner_CannotFinalize()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await ResetToPendingReviewAsync();
            var subId = ids.SubmissionId;

            using var scope = _fixture.ScopeFactory.CreateScope();
            var mgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamSubmissionManager>();
            var repo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IExamSubmissionRepository>();
            var gradeRepo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IGradeRepository>();

            var caller = TeacherCaller(Guid.NewGuid());

            var beforeGrades = await gradeRepo.GetByStudentIdAsync(ids.StudentId);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await mgr.FinalizeAsync(subId, new FinalizeSubmissionDto(), caller));

            var after = await repo.GetByIdAsync(subId, withAnswers: true);
            Assert.NotNull(after);
            Assert.Null(after.GradeId);

            var afterGrades = await gradeRepo.GetByStudentIdAsync(ids.StudentId);
            Assert.Equal(beforeGrades.Count, afterGrades.Count);
        }

        [SkippableFact]
        public async Task TeacherOwner_CanReviewAndFinalize()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await ResetToPendingReviewAsync();
            var subId = ids.SubmissionId;

            using var scope = _fixture.ScopeFactory.CreateScope();
            var mgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamSubmissionManager>();
            var repo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IExamSubmissionRepository>();
            var gradeRepo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IGradeRepository>();

            var caller = TeacherCaller(SeedData.TeacherId);

            // Review: award points to open answer
            var current = await repo.GetByIdAsync(subId, withAnswers: true);
            var openAns = current!.Answers.First(a => a.QuestionId == ids.OpenQuestionId);

            var reviewDto = new ReviewOpenAnswersDto();
            reviewDto.Answers.Add(new OpenAnswerReviewInputDto { AnswerId = openAns.Id, AwardedPoints = 4m });

            await mgr.ReviewAsync(subId, reviewDto, caller);

            var afterReview = await repo.GetByIdAsync(subId, withAnswers: true);
            Assert.NotNull(afterReview);
            var updatedOpen = afterReview!.Answers.First(a => a.QuestionId == ids.OpenQuestionId);
            Assert.Equal(4m, updatedOpen.AwardedPoints);

            // Finalize
            var beforeGrades = await gradeRepo.GetByStudentIdAsync(ids.StudentId);
            await mgr.FinalizeAsync(subId, new FinalizeSubmissionDto(), caller);

            var after = await repo.GetByIdAsync(subId, withAnswers: true);
            Assert.Equal(SubmissionStatus.Finalized, after!.Status);
            Assert.NotNull(after.GradeId);

            var afterGrades = await gradeRepo.GetByStudentIdAsync(ids.StudentId);
            Assert.True(afterGrades.Count >= beforeGrades.Count + 1);
        }

        [SkippableFact]
        public async Task Admin_CanReviewAndFinalize()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await ResetToPendingReviewAsync();
            var subId = ids.SubmissionId;

            using var scope = _fixture.ScopeFactory.CreateScope();
            var mgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamSubmissionManager>();
            var repo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IExamSubmissionRepository>();
            var gradeRepo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IGradeRepository>();

            var caller = AdminCaller();

            var current = await repo.GetByIdAsync(subId, withAnswers: true);
            var openAns = current!.Answers.First(a => a.QuestionId == ids.OpenQuestionId);

            var reviewDto = new ReviewOpenAnswersDto();
            reviewDto.Answers.Add(new OpenAnswerReviewInputDto { AnswerId = openAns.Id, AwardedPoints = 3m });

            await mgr.ReviewAsync(subId, reviewDto, caller);
            await mgr.FinalizeAsync(subId, new FinalizeSubmissionDto(), caller);

            var after = await repo.GetByIdAsync(subId, withAnswers: true);
            Assert.Equal(SubmissionStatus.Finalized, after!.Status);
            Assert.NotNull(after.GradeId);
            var grades = await gradeRepo.GetByStudentIdAsync(ids.StudentId);
            Assert.Contains(grades, g => g.Id == after.GradeId.Value);
        }

        [SkippableFact]
        public async Task Review_DoesNotFinalizeWithoutGrade()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await ResetToPendingReviewAsync();
            var subId = ids.SubmissionId;

            using var scope = _fixture.ScopeFactory.CreateScope();
            var mgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamSubmissionManager>();
            var repo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IExamSubmissionRepository>();

            var caller = TeacherCaller(SeedData.TeacherId);

            var current = await repo.GetByIdAsync(subId, withAnswers: true);
            var openAns = current!.Answers.First(a => a.QuestionId == ids.OpenQuestionId);

            var reviewDto = new ReviewOpenAnswersDto();
            reviewDto.Answers.Add(new OpenAnswerReviewInputDto { AnswerId = openAns.Id, AwardedPoints = 2m });

            await mgr.ReviewAsync(subId, reviewDto, caller);

            var after = await repo.GetByIdAsync(subId, withAnswers: true);
            // Business rule expected: Review should not implicitly finalize the submission
            Assert.NotEqual(SubmissionStatus.Finalized, after!.Status);
            // We also expect no Grade created yet
            Assert.Null(after.GradeId);
        }

        [SkippableFact]
        public async Task ReviewThenFinalize_CreatesExactlyOneGrade()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await ResetToPendingReviewAsync();
            var subId = ids.SubmissionId;

            using var scope = _fixture.ScopeFactory.CreateScope();
            var mgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamSubmissionManager>();
            var repo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IExamSubmissionRepository>();
            var gradeRepo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IGradeRepository>();

            var caller = TeacherCaller(SeedData.TeacherId);

            var current = await repo.GetByIdAsync(subId, withAnswers: true);
            var openAns = current!.Answers.First(a => a.QuestionId == ids.OpenQuestionId);

            var reviewDto = new ReviewOpenAnswersDto();
            reviewDto.Answers.Add(new OpenAnswerReviewInputDto { AnswerId = openAns.Id, AwardedPoints = 5m });
            await mgr.ReviewAsync(subId, reviewDto, caller);

            var beforeGrades = await gradeRepo.GetByStudentIdAsync(ids.StudentId);
            await mgr.FinalizeAsync(subId, new FinalizeSubmissionDto(), caller);
            await mgr.FinalizeAsync(subId, new FinalizeSubmissionDto(), caller);

            var afterGrades = await gradeRepo.GetByStudentIdAsync(ids.StudentId);
            var newGrades = afterGrades.Except(beforeGrades, new GuidComparer()).ToList();
            Assert.Single(newGrades);
        }

        [SkippableFact]
        public async Task FinalizeTwice_RemainsIdempotent()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await ResetToPendingReviewAsync();
            var subId = ids.SubmissionId;

            using var scope = _fixture.ScopeFactory.CreateScope();
            var mgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamSubmissionManager>();
            var repo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IExamSubmissionRepository>();
            var gradeRepo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IGradeRepository>();

            var caller = TeacherCaller(SeedData.TeacherId);

            var current = await repo.GetByIdAsync(subId, withAnswers: true);
            var openAns = current!.Answers.First(a => a.QuestionId == ids.OpenQuestionId);

            var reviewDto = new ReviewOpenAnswersDto();
            reviewDto.Answers.Add(new OpenAnswerReviewInputDto { AnswerId = openAns.Id, AwardedPoints = 5m });
            await mgr.ReviewAsync(subId, reviewDto, caller);

            await mgr.FinalizeAsync(subId, new FinalizeSubmissionDto(), caller);
            await mgr.FinalizeAsync(subId, new FinalizeSubmissionDto(), caller);

            var after = await repo.GetByIdAsync(subId, withAnswers: true);
            var grades = await gradeRepo.GetByStudentIdAsync(ids.StudentId);
            Assert.NotNull(after!.GradeId);
            Assert.True(grades.Count(g => g.Id == after.GradeId.Value) == 1);
        }

        [SkippableFact]
        public async Task CalculateOpenScore_OnlyOpenQuestionsCounted()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await ResetToPendingReviewAsync();
            var subId = ids.SubmissionId;

            using var scope = _fixture.ScopeFactory.CreateScope();
            var mgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamSubmissionManager>();
            var repo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IExamSubmissionRepository>();

            var caller = TeacherCaller(SeedData.TeacherId);

            // award points to the open answer only; the closed one must not contribute to OpenScore
            var current = await repo.GetByIdAsync(subId, withAnswers: true);
            var openAns = current!.Answers.First(a => a.QuestionId == ids.OpenQuestionId);

            var reviewDto = new ReviewOpenAnswersDto();
            reviewDto.Answers.Add(new OpenAnswerReviewInputDto { AnswerId = openAns.Id, AwardedPoints = 3m });
            await mgr.ReviewAsync(subId, reviewDto, caller);

            var after = await repo.GetByIdAsync(subId, withAnswers: true);
            Assert.Equal(3m, after!.OpenScore);
            Assert.Equal((after.ClosedScore ?? 0m) + (after.OpenScore ?? 0m), after.TotalScore);
        }

        [SkippableFact]
        public async Task TotalScore_DoesNotDoubleCountClosedAnswers()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await ResetToPendingReviewAsync();
            var subId = ids.SubmissionId;

            using var scope = _fixture.ScopeFactory.CreateScope();
            var mgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamSubmissionManager>();
            var repo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IExamSubmissionRepository>();

            var caller = TeacherCaller(SeedData.TeacherId);

            // set closed score directly via DB to simulate closed grading
            var ctx = scope.ServiceProvider.GetRequiredService<ElectronicRegisterAPI.Infrastructure.Persistence.ElectronicRegisterContext>();
            var entity = await ctx.ExamSubmissions.FindAsync(subId);
            entity!.ClosedScore = 4m;
            await ctx.SaveChangesAsync();

            // award open points
            var current = await repo.GetByIdAsync(subId, withAnswers: true);
            var openAns = current!.Answers.First(a => a.QuestionId == ids.OpenQuestionId);
            var reviewDto = new ReviewOpenAnswersDto();
            reviewDto.Answers.Add(new OpenAnswerReviewInputDto { AnswerId = openAns.Id, AwardedPoints = 2m });
            await mgr.ReviewAsync(subId, reviewDto, caller);

            var after = await repo.GetByIdAsync(subId, withAnswers: true);
            Assert.Equal(2m, after!.OpenScore);
            Assert.Equal(6m, after.TotalScore); // 4 closed + 2 open
        }

        [SkippableFact]
        public async Task FullReplace_NoUniqueConstraintViolation()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await ResetAsync();
            var subId = ids.SubmissionId;

            using var scope = _fixture.ScopeFactory.CreateScope();
            var mgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamManager>();
            var repo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IExamSubmissionRepository>();

            var studentCaller = new ClaimsContext(Guid.NewGuid(), UserRole.Student, ids.StudentId, null);

            var dto = new SaveExamAnswersDto { ClientRevision = 1 };
            dto.Answers.Add(new AnswerInputDto { QuestionId = ids.OpenQuestionId, OpenText = "x" });
            dto.Answers.Add(new AnswerInputDto { QuestionId = ids.SingleQuestionId, OpenText = "y" });

            var res = await mgr.SaveAnswersAsync(subId, dto, studentCaller);
            Assert.True(res.Accepted);

            var after = await repo.GetByIdAsync(subId, withAnswers: true);
            Assert.Equal(2, after!.Answers.Count);
        }

        [SkippableFact]
        public async Task FailedReplace_RollsBackAllChanges()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await ResetAsync();
            var subId = ids.SubmissionId;

            using var scope = _fixture.ScopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IExamSubmissionRepository>();

            // simulate client sending stale revision -> SaveAnswersAsync should return Accepted = false and not modify data
            var studentCaller = new ClaimsContext(Guid.NewGuid(), UserRole.Student, ids.StudentId, null);
            var mgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamManager>();

            // First, perform an initial save to increment revision
            var dtoInit = new SaveExamAnswersDto { ClientRevision = 1 };
            dtoInit.Answers.Add(new AnswerInputDto { QuestionId = ids.OpenQuestionId, OpenText = "init" });
            var resInit = await mgr.SaveAnswersAsync(subId, dtoInit, studentCaller);
            Assert.True(resInit.Accepted);

            // Now attempt with stale revision 0
            var dtoFail = new SaveExamAnswersDto { ClientRevision = 0 };
            dtoFail.Answers.Add(new AnswerInputDto { QuestionId = ids.OpenQuestionId, OpenText = "stale" });
            var resFail = await mgr.SaveAnswersAsync(subId, dtoFail, studentCaller);
            Assert.False(resFail.Accepted);

            // verify that persisted answers are still the initial ones
            var after = await repo.GetByIdAsync(subId, withAnswers: true);
            Assert.Contains(after!.Answers, a => a.OpenText == "init");
        }

        // small comparer for Guid lists
        private class GuidComparer : System.Collections.Generic.IEqualityComparer<Domain.Models.Grade>
        {
            public bool Equals(Domain.Models.Grade? x, Domain.Models.Grade? y)
            {
                if (x is null || y is null) return false;
                return x.Id == y.Id;
            }

            public int GetHashCode(Domain.Models.Grade obj) => obj.Id.GetHashCode();
        }
    }
}
