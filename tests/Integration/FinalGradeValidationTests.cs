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
    public class FinalGradeValidationTests
    {
        private readonly IntegrationFixture _fixture;

        public FinalGradeValidationTests(IntegrationFixture fixture)
        {
            _fixture = fixture;
        }

        private Task<ScenarioIds> ResetToPendingReviewAsync() => _fixture.PreparePendingReviewScenarioAsync(IntegrationScenario.ExamRegression);

        [SkippableFact]
        public async Task Finalize_WithFinalGradeZero_IsRejectedAndNoPersistence()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await ResetToPendingReviewAsync();

            using var scope = _fixture.ScopeFactory.CreateScope();
            var mgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamSubmissionManager>();
            var repo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IExamSubmissionRepository>();
            var gradeRepo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IGradeRepository>();
            var caller = new ClaimsContext(Guid.NewGuid(), UserRole.Teacher, null, SeedData.TeacherId);

            var before = await repo.GetByIdAsync(ids.SubmissionId, withAnswers: true);
            var beforeGrades = await gradeRepo.GetByStudentIdAsync(ids.StudentId);

            // Ensure open answers are reviewed before finalization
            var reviewDto = new ReviewOpenAnswersDto();
            foreach (var a in before!.Answers.Where(x => x.OpenText != null))
            {
                reviewDto.Answers.Add(new OpenAnswerReviewInputDto { AnswerId = a.Id, AwardedPoints = 5m });
            }
            if (reviewDto.Answers.Count > 0) await mgr.ReviewAsync(ids.SubmissionId, reviewDto, caller);

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await mgr.FinalizeAsync(ids.SubmissionId, new FinalizeSubmissionDto { FinalGrade = 0m }, caller));

            // Verify no partial persistence
            using var verifyScope = _fixture.ScopeFactory.CreateScope();
            var verifyRepo = verifyScope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IExamSubmissionRepository>();
            var verifyGradeRepo = verifyScope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IGradeRepository>();
            var persisted = await verifyRepo.GetByIdAsync(ids.SubmissionId, withAnswers: true);
            Assert.Null(persisted!.GradeId);
            var afterGrades = await verifyGradeRepo.GetByStudentIdAsync(ids.StudentId);
            Assert.Equal(beforeGrades.Count, afterGrades.Count);
            Assert.Equal(before.Status, persisted.Status);
            Assert.Null(persisted.FinalizedAt);
        }

        [SkippableFact]
        public async Task Finalize_WithFinalGradeNegative_IsRejectedAndNoPersistence()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await ResetToPendingReviewAsync();

            using var scope = _fixture.ScopeFactory.CreateScope();
            var mgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamSubmissionManager>();
            var repo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IExamSubmissionRepository>();
            var gradeRepo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IGradeRepository>();
            var caller = new ClaimsContext(Guid.NewGuid(), UserRole.Teacher, null, SeedData.TeacherId);

            var before = await repo.GetByIdAsync(ids.SubmissionId, withAnswers: true);
            var beforeGrades = await gradeRepo.GetByStudentIdAsync(ids.StudentId);

            var reviewDto = new ReviewOpenAnswersDto();
            foreach (var a in before!.Answers.Where(x => x.OpenText != null))
            {
                reviewDto.Answers.Add(new OpenAnswerReviewInputDto { AnswerId = a.Id, AwardedPoints = 5m });
            }
            if (reviewDto.Answers.Count > 0) await mgr.ReviewAsync(ids.SubmissionId, reviewDto, caller);

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await mgr.FinalizeAsync(ids.SubmissionId, new FinalizeSubmissionDto { FinalGrade = -1m }, caller));

            using var verifyScope = _fixture.ScopeFactory.CreateScope();
            var verifyRepo = verifyScope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IExamSubmissionRepository>();
            var verifyGradeRepo = verifyScope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IGradeRepository>();
            var persisted = await verifyRepo.GetByIdAsync(ids.SubmissionId, withAnswers: true);
            Assert.Null(persisted!.GradeId);
            var afterGrades = await verifyGradeRepo.GetByStudentIdAsync(ids.StudentId);
            Assert.Equal(beforeGrades.Count, afterGrades.Count);
            Assert.Equal(before.Status, persisted.Status);
            Assert.Null(persisted.FinalizedAt);
        }

        [SkippableFact]
        public async Task Finalize_WithFinalGradeTooLarge_IsRejectedAndNoPersistence()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await ResetToPendingReviewAsync();

            using var scope = _fixture.ScopeFactory.CreateScope();
            var mgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamSubmissionManager>();
            var repo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IExamSubmissionRepository>();
            var gradeRepo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IGradeRepository>();
            var caller = new ClaimsContext(Guid.NewGuid(), UserRole.Teacher, null, SeedData.TeacherId);

            var before = await repo.GetByIdAsync(ids.SubmissionId, withAnswers: true);
            var beforeGrades = await gradeRepo.GetByStudentIdAsync(ids.StudentId);

            var reviewDto = new ReviewOpenAnswersDto();
            foreach (var a in before!.Answers.Where(x => x.OpenText != null))
            {
                reviewDto.Answers.Add(new OpenAnswerReviewInputDto { AnswerId = a.Id, AwardedPoints = 5m });
            }
            if (reviewDto.Answers.Count > 0) await mgr.ReviewAsync(ids.SubmissionId, reviewDto, caller);

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await mgr.FinalizeAsync(ids.SubmissionId, new FinalizeSubmissionDto { FinalGrade = 10.01m }, caller));

            using var verifyScope = _fixture.ScopeFactory.CreateScope();
            var verifyRepo = verifyScope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IExamSubmissionRepository>();
            var verifyGradeRepo = verifyScope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IGradeRepository>();
            var persisted = await verifyRepo.GetByIdAsync(ids.SubmissionId, withAnswers: true);
            Assert.Null(persisted!.GradeId);
            var afterGrades = await verifyGradeRepo.GetByStudentIdAsync(ids.StudentId);
            Assert.Equal(beforeGrades.Count, afterGrades.Count);
            Assert.Equal(before.Status, persisted.Status);
            Assert.Null(persisted.FinalizedAt);
        }

        [SkippableFact]
        public async Task Finalize_WithFinalGradeAtLowerBound_Succeeds()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await ResetToPendingReviewAsync();

            using var scope = _fixture.ScopeFactory.CreateScope();
            var mgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamSubmissionManager>();
            var repo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IExamSubmissionRepository>();
            var gradeRepo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IGradeRepository>();
            var caller = new ClaimsContext(Guid.NewGuid(), UserRole.Teacher, null, SeedData.TeacherId);

            // Ensure open answers reviewed
            var before = await repo.GetByIdAsync(ids.SubmissionId, withAnswers: true);
            var reviewDto = new ReviewOpenAnswersDto();
            foreach (var a in before!.Answers.Where(x => x.OpenText != null))
            {
                reviewDto.Answers.Add(new OpenAnswerReviewInputDto { AnswerId = a.Id, AwardedPoints = 5m });
            }
            if (reviewDto.Answers.Count > 0) await mgr.ReviewAsync(ids.SubmissionId, reviewDto, caller);

            await mgr.FinalizeAsync(ids.SubmissionId, new FinalizeSubmissionDto { FinalGrade = 1m }, caller);

            using var verifyScope = _fixture.ScopeFactory.CreateScope();
            var verifyRepo = verifyScope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IExamSubmissionRepository>();
            var verifyGradeRepo = verifyScope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IGradeRepository>();
            var persisted = await verifyRepo.GetByIdAsync(ids.SubmissionId, withAnswers: true);
            Assert.NotNull(persisted!.GradeId);
            var grades = await verifyGradeRepo.GetByStudentIdAsync(ids.StudentId);
            Assert.Contains(grades, g => g.Id == persisted.GradeId.Value);
        }

        [SkippableFact]
        public async Task Finalize_WithFinalGradeAtUpperBound_Succeeds()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await ResetToPendingReviewAsync();

            using var scope = _fixture.ScopeFactory.CreateScope();
            var mgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamSubmissionManager>();
            var repo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IExamSubmissionRepository>();
            var gradeRepo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IGradeRepository>();
            var caller = new ClaimsContext(Guid.NewGuid(), UserRole.Teacher, null, SeedData.TeacherId);

            var before = await repo.GetByIdAsync(ids.SubmissionId, withAnswers: true);
            var reviewDto = new ReviewOpenAnswersDto();
            foreach (var a in before!.Answers.Where(x => x.OpenText != null))
            {
                reviewDto.Answers.Add(new OpenAnswerReviewInputDto { AnswerId = a.Id, AwardedPoints = 5m });
            }
            if (reviewDto.Answers.Count > 0) await mgr.ReviewAsync(ids.SubmissionId, reviewDto, caller);

            await mgr.FinalizeAsync(ids.SubmissionId, new FinalizeSubmissionDto { FinalGrade = 10m }, caller);

            using var verifyScope = _fixture.ScopeFactory.CreateScope();
            var verifyRepo = verifyScope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IExamSubmissionRepository>();
            var verifyGradeRepo = verifyScope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IGradeRepository>();
            var persisted = await verifyRepo.GetByIdAsync(ids.SubmissionId, withAnswers: true);
            Assert.NotNull(persisted!.GradeId);
            var grades = await verifyGradeRepo.GetByStudentIdAsync(ids.StudentId);
            Assert.Contains(grades, g => g.Id == persisted.GradeId.Value);
        }
    }
}
