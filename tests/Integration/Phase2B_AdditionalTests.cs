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
    public class Phase2B_AdditionalTests
    {
        private readonly IntegrationFixture _fixture;

        public Phase2B_AdditionalTests(IntegrationFixture fixture)
        {
            _fixture = fixture;
        }

        // Helpers -----------------------------------------------------------------
        private ClaimsContext TeacherCaller() => new ClaimsContext(SeedData.TeacherUserId, UserRole.Teacher, null, SeedData.TeacherId);
        private ClaimsContext OtherTeacherCaller() => new ClaimsContext(SeedData.Id(0,13), UserRole.Teacher, null, SeedData.Id(0,12));
        private ClaimsContext AdminCaller() => new ClaimsContext(SeedData.AdminUserId, UserRole.Admin, null, null);
        private ClaimsContext StudentCaller(ScenarioIds ids) => new ClaimsContext(ids.StudentId, UserRole.Student, ids.StudentId, null);

        private async Task EnsureAllOpenReviewedAsync(ScenarioIds ids, ClaimsContext teacher)
        {
            using var scope = _fixture.ScopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IExamSubmissionRepository>();
            var submissionMgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamSubmissionManager>();

            var sub = await repo.GetByIdAsync(ids.SubmissionId, withAnswers: true);
            var reviewDto = new ReviewOpenAnswersDto();
            foreach (var a in sub!.Answers.Where(x => x.OpenText != null))
            {
                reviewDto.Answers.Add(new OpenAnswerReviewInputDto { AnswerId = a.Id, AwardedPoints = 0m });
            }

            if (reviewDto.Answers.Count > 0)
            {
                await submissionMgr.ReviewAsync(ids.SubmissionId, reviewDto, teacher);
            }
        }

        // A. Conversion and persistence ------------------------------------------
        [SkippableFact]
        public async Task ClosedOnly_70Percent_CreatesSuggestedAndOfficialSeven()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await _fixture.ResetScenarioAsync(IntegrationScenario.ClosedOnly);

            using var scope = _fixture.ScopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IExamSubmissionRepository>();
            var examMgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamManager>();

            // adjust seed so that student gets 70% (7/10)
            using (var ctxWrite = scope.ServiceProvider.GetRequiredService<ElectronicRegisterAPI.Infrastructure.Persistence.ElectronicRegisterContext>())
            {
                var q1 = await ctxWrite.Questions.FindAsync(ids.OpenQuestionId);
                var q2 = await ctxWrite.Questions.FindAsync(ids.SingleQuestionId);
                q1.Points = 3m; q2.Points = 7m; // 3 + 7 = 10, student correct on q2 only => 7/10
                await ctxWrite.SaveChangesAsync();
            }

            // submit using persisted answers from seed
            var cur = await repo.GetByIdAsync(ids.SubmissionId, withAnswers: true);
            var submit = new SubmitExamDto { ClientRevision = cur!.Revision + 1 };
            foreach (var a in cur.Answers)
            {
                if (a.OpenText != null) submit.Answers.Add(new AnswerInputDto { QuestionId = a.QuestionId, OpenText = a.OpenText });
                else if (a.SelectedOptionIds?.Any() == true) { var ai = new AnswerInputDto { QuestionId = a.QuestionId }; foreach (var oid in a.SelectedOptionIds) ai.SelectedOptionIds.Add(oid); submit.Answers.Add(ai); }
            }

            var student = StudentCaller(ids);
            await examMgr.SubmitAsync(ids.SubmissionId, submit, student);

            using var verify = _fixture.ScopeFactory.CreateScope();
            var ctxv = verify.ServiceProvider.GetRequiredService<ElectronicRegisterAPI.Infrastructure.Persistence.ElectronicRegisterContext>();
            var grades = await ctxv.Grades.Where(g => g.StudentId == ids.StudentId).ToListAsync();
            Assert.NotEmpty(grades);
            var g = grades.First();
            Assert.Equal(7.00m, g.Value);
            Assert.True(g.Value >= 6m);
        }

        [SkippableFact]
        public async Task ClosedOnly_BelowThreshold_NeverPasses()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await _fixture.ResetScenarioAsync(IntegrationScenario.ClosedOnly);

            using var scope = _fixture.ScopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IExamSubmissionRepository>();
            var examMgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamManager>();

            // adjust seed so student gets 5/10 (below 60)
            using (var ctxWrite = scope.ServiceProvider.GetRequiredService<ElectronicRegisterAPI.Infrastructure.Persistence.ElectronicRegisterContext>())
            {
                var q1 = await ctxWrite.Questions.FindAsync(ids.OpenQuestionId);
                var q2 = await ctxWrite.Questions.FindAsync(ids.SingleQuestionId);
                q1.Points = 2.5m; q2.Points = 7.5m; // assume student gets only q2 correct -> 7.5/10 => 75% (not below)
                // instead force student to select incorrect existing option for q2
                var ans = await ctxWrite.SubmissionAnswers.Include(a => a.Options).FirstAsync(a => a.SubmissionId == ids.SubmissionId && a.QuestionId == ids.SingleQuestionId);
                ans.Options.Clear();
                var wrongOption = await ctxWrite.QuestionOptions.FindAsync(ids.WrongOptionId);
                if (wrongOption == null) throw new InvalidOperationException("Expected wrong option to exist in seed data.");
                ans.Options.Add(wrongOption);
                await ctxWrite.SaveChangesAsync();
            }

            var cur = await repo.GetByIdAsync(ids.SubmissionId, withAnswers: true);
            var submit = new SubmitExamDto { ClientRevision = cur!.Revision + 1 };
            foreach (var a in cur.Answers)
            {
                if (a.OpenText != null) submit.Answers.Add(new AnswerInputDto { QuestionId = a.QuestionId, OpenText = a.OpenText });
                else if (a.SelectedOptionIds?.Any() == true) { var ai = new AnswerInputDto { QuestionId = a.QuestionId }; foreach (var oid in a.SelectedOptionIds) ai.SelectedOptionIds.Add(oid); submit.Answers.Add(ai); }
            }

            var student = StudentCaller(ids);
            await examMgr.SubmitAsync(ids.SubmissionId, submit, student);

            using var verify = _fixture.ScopeFactory.CreateScope();
            var ctxv = verify.ServiceProvider.GetRequiredService<ElectronicRegisterAPI.Infrastructure.Persistence.ElectronicRegisterContext>();
            var grades = await ctxv.Grades.Where(g => g.StudentId == ids.StudentId).ToListAsync();
            Assert.NotEmpty(grades);
            var g = grades.First();
            Assert.True(g.Value < 6m);
            Assert.False(g.Value >= 6m);
        }

        [SkippableFact]
        public async Task DifferentMaxScore_UsesPassingPercentNotPassingScore()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await _fixture.ResetScenarioAsync(IntegrationScenario.ClosedOnly);

            using var scope = _fixture.ScopeFactory.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ElectronicRegisterAPI.Infrastructure.Persistence.ElectronicRegisterContext>();
            var repo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IExamSubmissionRepository>();
            var examMgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamManager>();

            // make exam out of 20 points and student gets 12 => passingPercent 60 -> grade 6
            var exam = await ctx.Exams.FindAsync(ids.ExamId);
            exam.TotalPoints = 20m;
            var q1 = await ctx.Questions.FindAsync(ids.OpenQuestionId);
            var q2 = await ctx.Questions.FindAsync(ids.SingleQuestionId);
            q1.Points = 8m; q2.Points = 12m; // total 20, assume student correct on q2
            await ctx.SaveChangesAsync();

            var cur = await repo.GetByIdAsync(ids.SubmissionId, withAnswers: true);
            var submit = new SubmitExamDto { ClientRevision = cur!.Revision + 1 };
            foreach (var a in cur.Answers)
            {
                if (a.OpenText != null) submit.Answers.Add(new AnswerInputDto { QuestionId = a.QuestionId, OpenText = a.OpenText });
                else if (a.SelectedOptionIds?.Any() == true) { var ai = new AnswerInputDto { QuestionId = a.QuestionId }; foreach (var oid in a.SelectedOptionIds) ai.SelectedOptionIds.Add(oid); submit.Answers.Add(ai); }
            }

            var student = StudentCaller(ids);
            await examMgr.SubmitAsync(ids.SubmissionId, submit, student);

            using var verify = _fixture.ScopeFactory.CreateScope();
            var ctxv = verify.ServiceProvider.GetRequiredService<ElectronicRegisterAPI.Infrastructure.Persistence.ElectronicRegisterContext>();
            var grades = await ctxv.Grades.Where(g => g.StudentId == ids.StudentId).ToListAsync();
            Assert.NotEmpty(grades);
            var g = grades.First();
            Assert.Equal(6.00m, g.Value);
        }

        // B. Lifecycle of submissions -------------------------------------------
        [SkippableFact]
        public async Task PendingReview_DoesNotPublishOfficialGrade()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await _fixture.ResetScenarioAsync(IntegrationScenario.ConcurrentFinalize);

            using var scope = _fixture.ScopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IExamSubmissionRepository>();
            var mgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamManager>();

            var result = await mgr.GetResultAsync(ids.SubmissionId, TeacherCaller());
            Assert.NotNull(result);
            Assert.Null(result.Grade);
            var sub = await repo.GetByIdAsync(ids.SubmissionId, withAnswers: true);
            Assert.Equal(SubmissionStatus.PendingReview, sub!.Status);
        }

        [SkippableFact]
        public async Task FinalizeWithoutOverride_UsesSuggestedGrade()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await _fixture.PreparePendingReviewScenarioAsync(IntegrationScenario.ExamRegression);

            using var scope = _fixture.ScopeFactory.CreateScope();
            var mgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamSubmissionManager>();
            var examMgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamManager>();
            var repo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IExamSubmissionRepository>();

            var toReview = await repo.GetByIdAsync(ids.SubmissionId, withAnswers: true);
            var reviewDto = new ReviewOpenAnswersDto();
            foreach (var a in toReview!.Answers.Where(x => x.OpenText != null))
                reviewDto.Answers.Add(new OpenAnswerReviewInputDto { AnswerId = a.Id, AwardedPoints = 5m });
            if (reviewDto.Answers.Count > 0) await mgr.ReviewAsync(ids.SubmissionId, reviewDto, TeacherCaller());

            // Finalize without explicit FinalGrade
            await mgr.FinalizeAsync(ids.SubmissionId, new FinalizeSubmissionDto(), TeacherCaller());

            using var verify = _fixture.ScopeFactory.CreateScope();
            var em = verify.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamManager>();
            var res = await em.GetResultAsync(ids.SubmissionId, TeacherCaller());
            Assert.NotNull(res);
            Assert.NotNull(res.Grade);
            // SuggestedGrade preserved: read from DB and compare to returned Grade
            var ctx = verify.ServiceProvider.GetRequiredService<ElectronicRegisterAPI.Infrastructure.Persistence.ElectronicRegisterContext>();
            var subEntity = await ctx.ExamSubmissions.FindAsync(ids.SubmissionId);
            Assert.NotNull(subEntity!.SuggestedGrade);
            Assert.Equal(subEntity.SuggestedGrade.Value, res.Grade);
        }

        // C. Teacher override --------------------------------------------------
        [SkippableFact]
        public async Task TeacherOverride_SevenToFive_MarksFailed()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await _fixture.PreparePendingReviewScenarioAsync(IntegrationScenario.ExamRegression);

            using var scope = _fixture.ScopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IExamSubmissionRepository>();
            var mgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamSubmissionManager>();
            var examMgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamManager>();

            // ensure suggested grade is 7 by adjusting closed score
            using (var ctxWrite = scope.ServiceProvider.GetRequiredService<ElectronicRegisterAPI.Infrastructure.Persistence.ElectronicRegisterContext>())
            {
                var sub = await ctxWrite.ExamSubmissions.FindAsync(ids.SubmissionId);
                sub.ClosedScore = 7m;
                sub.OpenMaxPoints = 0m;
                sub.MaxScore = 10m;
                sub.PassingScore = 6m;
                await ctxWrite.SaveChangesAsync();
            }

            // review complete if needed
            var toReview = await repo.GetByIdAsync(ids.SubmissionId, withAnswers: true);
            var reviewDto = new ReviewOpenAnswersDto();
            foreach (var a in toReview!.Answers.Where(x => x.OpenText != null)) reviewDto.Answers.Add(new OpenAnswerReviewInputDto { AnswerId = a.Id, AwardedPoints = 0m });
            if (reviewDto.Answers.Count > 0) await mgr.ReviewAsync(ids.SubmissionId, reviewDto, TeacherCaller());

            // perform override
            var dto = new FinalizeSubmissionDto { FinalGrade = 5m, OverrideReason = "Teacher adjusted due to behavior" };
            await mgr.FinalizeAsync(ids.SubmissionId, dto, TeacherCaller());

            using var verify = _fixture.ScopeFactory.CreateScope();
            var em = verify.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamManager>();
            var res = await em.GetResultAsync(ids.SubmissionId, TeacherCaller());
            var ctxVerify = verify.ServiceProvider.GetRequiredService<ElectronicRegisterAPI.Infrastructure.Persistence.ElectronicRegisterContext>();
            var subEntity = await ctxVerify.ExamSubmissions.FindAsync(ids.SubmissionId);
            Assert.Equal(7m, subEntity!.SuggestedGrade);
            Assert.Equal(5m, res.Grade);
            Assert.False(res.Passed.Value);
        }

        [SkippableFact]
        public async Task TeacherOverride_FiveToSeven_MarksPassed()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await _fixture.PreparePendingReviewScenarioAsync(IntegrationScenario.ExamRegression);

            using var scope = _fixture.ScopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IExamSubmissionRepository>();
            var mgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamSubmissionManager>();

            using (var ctxWrite = scope.ServiceProvider.GetRequiredService<ElectronicRegisterAPI.Infrastructure.Persistence.ElectronicRegisterContext>())
            {
                var sub = await ctxWrite.ExamSubmissions.FindAsync(ids.SubmissionId);
                sub.ClosedScore = 5m; sub.OpenMaxPoints = 0m; sub.MaxScore = 10m; sub.PassingScore = 6m; await ctxWrite.SaveChangesAsync();
            }

            var dto = new FinalizeSubmissionDto { FinalGrade = 7m, OverrideReason = "Teacher adjusted upward" };
            await mgr.FinalizeAsync(ids.SubmissionId, dto, TeacherCaller());

            using var verify = _fixture.ScopeFactory.CreateScope();
            var em = verify.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamManager>();
            var res = await em.GetResultAsync(ids.SubmissionId, TeacherCaller());
            var ctxVerify = verify.ServiceProvider.GetRequiredService<ElectronicRegisterAPI.Infrastructure.Persistence.ElectronicRegisterContext>();
            var subEntity = await ctxVerify.ExamSubmissions.FindAsync(ids.SubmissionId);
            Assert.Equal(5m, subEntity!.SuggestedGrade);
            Assert.Equal(7m, res.Grade);
            Assert.True(res.Passed.Value);
        }

        [SkippableFact]
        public async Task OverrideWithoutReason_IsRejected()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await _fixture.PreparePendingReviewScenarioAsync(IntegrationScenario.ExamRegression);

            using var scope = _fixture.ScopeFactory.CreateScope();
            var mgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamSubmissionManager>();
            await EnsureAllOpenReviewedAsync(ids, TeacherCaller());

            var dto = new FinalizeSubmissionDto { FinalGrade = 5m };
            await Assert.ThrowsAsync<ArgumentException>(async () => await mgr.FinalizeAsync(ids.SubmissionId, dto, TeacherCaller()));

            using var verify = _fixture.ScopeFactory.CreateScope();
            var ctx = verify.ServiceProvider.GetRequiredService<ElectronicRegisterAPI.Infrastructure.Persistence.ElectronicRegisterContext>();
            var grades = await ctx.Grades.Where(g => g.StudentId == ids.StudentId).ToListAsync();
            // No grade persisted
            Assert.Empty(grades);
        }

        [SkippableFact]
        public async Task OverrideWithWhitespaceReason_IsRejected()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await _fixture.PreparePendingReviewScenarioAsync(IntegrationScenario.ExamRegression);

            using var scope = _fixture.ScopeFactory.CreateScope();
            var mgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamSubmissionManager>();
            await EnsureAllOpenReviewedAsync(ids, TeacherCaller());

            var dto = new FinalizeSubmissionDto { FinalGrade = 5m, OverrideReason = "   " };
            await Assert.ThrowsAsync<ArgumentException>(async () => await mgr.FinalizeAsync(ids.SubmissionId, dto, TeacherCaller()));
        }

        [SkippableFact]
        public async Task OverrideWithInvalidGrade_IsRejectedWithoutChanges()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await _fixture.PreparePendingReviewScenarioAsync(IntegrationScenario.ExamRegression);

            using var scope = _fixture.ScopeFactory.CreateScope();
            var mgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamSubmissionManager>();
            // Ensure all open answers are reviewed before Finalize
            await EnsureAllOpenReviewedAsync(ids, TeacherCaller());

            var dto = new FinalizeSubmissionDto { FinalGrade = 0m, OverrideReason = "Reason" };
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await mgr.FinalizeAsync(ids.SubmissionId, dto, TeacherCaller()));

            using var verify = _fixture.ScopeFactory.CreateScope();
            var ctx = verify.ServiceProvider.GetRequiredService<ElectronicRegisterAPI.Infrastructure.Persistence.ElectronicRegisterContext>();
            var grades = await ctx.Grades.Where(g => g.StudentId == ids.StudentId).ToListAsync();
            Assert.Empty(grades);
        }

        [SkippableFact]
        public async Task TeacherNonOwner_CannotOverride()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await _fixture.PreparePendingReviewScenarioAsync(IntegrationScenario.ExamRegression);

            using var scope = _fixture.ScopeFactory.CreateScope();
            var mgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamSubmissionManager>();

            var dto = new FinalizeSubmissionDto { FinalGrade = 5m, OverrideReason = "Reason" };
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await mgr.FinalizeAsync(ids.SubmissionId, dto, OtherTeacherCaller()));
        }

        [SkippableFact]
        public async Task Admin_CanOverride()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await _fixture.PreparePendingReviewScenarioAsync(IntegrationScenario.ExamRegression);

            using var scope = _fixture.ScopeFactory.CreateScope();
            var mgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamSubmissionManager>();

            await EnsureAllOpenReviewedAsync(ids, TeacherCaller());
            var dto = new FinalizeSubmissionDto { FinalGrade = 5m, OverrideReason = "Admin override" };
            await mgr.FinalizeAsync(ids.SubmissionId, dto, AdminCaller());

            using var verify = _fixture.ScopeFactory.CreateScope();
            var em = verify.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamManager>();
            var res = await em.GetResultAsync(ids.SubmissionId, TeacherCaller());
            Assert.Equal(5m, res.Grade);
        }

        // D. Grade already exists ------------------------------------------------
        [SkippableFact]
        public async Task ClosedOnly_Override_UpdatesExistingGradeWithoutDuplication()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await _fixture.ResetScenarioAsync(IntegrationScenario.ClosedOnly);

            using var scope = _fixture.ScopeFactory.CreateScope();
            var examMgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamManager>();
            var subMgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamSubmissionManager>();

            var cur = await examMgr.GetResultAsync(ids.SubmissionId, TeacherCaller());
            // ensure a grade exists by submitting
            var repo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IExamSubmissionRepository>();
            var submission = await repo.GetByIdAsync(ids.SubmissionId, withAnswers: true);
            var submit = new SubmitExamDto { ClientRevision = submission!.Revision + 1 };
            foreach (var a in submission.Answers)
            {
                if (a.OpenText != null) submit.Answers.Add(new AnswerInputDto { QuestionId = a.QuestionId, OpenText = a.OpenText });
                else if (a.SelectedOptionIds?.Any() == true) { var ai = new AnswerInputDto { QuestionId = a.QuestionId }; foreach (var oid in a.SelectedOptionIds) ai.SelectedOptionIds.Add(oid); submit.Answers.Add(ai); }
            }
            await examMgr.SubmitAsync(ids.SubmissionId, submit, StudentCaller(ids));

            using var verify = _fixture.ScopeFactory.CreateScope();
            var ctx = verify.ServiceProvider.GetRequiredService<ElectronicRegisterAPI.Infrastructure.Persistence.ElectronicRegisterContext>();
            var gradesBefore = await ctx.Grades.Where(g => g.StudentId == ids.StudentId).ToListAsync();
            Assert.NotEmpty(gradesBefore);
            var gid = gradesBefore.First().Id;

            // teacher overrides
            var dto = new FinalizeSubmissionDto { FinalGrade = 6m, OverrideReason = "Teacher override" };
            await subMgr.FinalizeAsync(ids.SubmissionId, dto, TeacherCaller());

            using var verify2 = _fixture.ScopeFactory.CreateScope();
            var ctx2 = verify2.ServiceProvider.GetRequiredService<ElectronicRegisterAPI.Infrastructure.Persistence.ElectronicRegisterContext>();
            var gradesAfter = await ctx2.Grades.Where(g => g.StudentId == ids.StudentId).ToListAsync();
            Assert.Single(gradesAfter);
            Assert.Equal(gid, gradesAfter.First().Id);
            Assert.Equal(6m, gradesAfter.First().Value);
        }

        // E. Audit and traceability ----------------------------------------------
        [SkippableFact]
        public async Task Override_PersistsReasonActorAndTimestamp()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await _fixture.PreparePendingReviewScenarioAsync(IntegrationScenario.ExamRegression);

            using var scope = _fixture.ScopeFactory.CreateScope();
            var subMgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamSubmissionManager>();
            var dto = new FinalizeSubmissionDto { FinalGrade = 5m, OverrideReason = "Reason" };
            // Ensure all open answers are reviewed before Finalize
            await EnsureAllOpenReviewedAsync(ids, TeacherCaller());
            await subMgr.FinalizeAsync(ids.SubmissionId, dto, TeacherCaller());

            using var verify = _fixture.ScopeFactory.CreateScope();
            var ctx = verify.ServiceProvider.GetRequiredService<ElectronicRegisterAPI.Infrastructure.Persistence.ElectronicRegisterContext>();
            var grade = await ctx.Grades.Where(g => g.StudentId == ids.StudentId).FirstAsync();
            Assert.NotNull(grade);
            var sub = await ctx.ExamSubmissions.FindAsync(ids.SubmissionId);
            Assert.NotNull(sub.ReviewedByUserId);
            Assert.NotNull(sub.FinalizedAt);
        }

        [SkippableFact]
        public async Task Override_CreatesAuditEventInSameTransaction()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await _fixture.PreparePendingReviewScenarioAsync(IntegrationScenario.ExamRegression);

            using var scope = _fixture.ScopeFactory.CreateScope();
            var subMgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamSubmissionManager>();
            var dto = new FinalizeSubmissionDto { FinalGrade = 5m, OverrideReason = "Reason" };
            await EnsureAllOpenReviewedAsync(ids, TeacherCaller());
            await subMgr.FinalizeAsync(ids.SubmissionId, dto, TeacherCaller());

            using var verify = _fixture.ScopeFactory.CreateScope();
            var ctx = verify.ServiceProvider.GetRequiredService<ElectronicRegisterAPI.Infrastructure.Persistence.ElectronicRegisterContext>();
            var audit = await ctx.AuditEvents.Where(a => a.EntityId == ids.SubmissionId && a.Action.Contains("finalize")).ToListAsync();
            Assert.NotEmpty(audit);
            var ev = audit.First();
            Assert.Equal(SeedData.TeacherUserId, ev.ActorUserId);
            Assert.Contains("override", ev.Action);
        }

        // F. Idempotence and concurrency ----------------------------------------
        [SkippableFact]
        public async Task FinalizeRetry_IsIdempotentAndDoesNotDuplicateAudit()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await _fixture.PreparePendingReviewScenarioAsync(IntegrationScenario.ExamRegression);

            using var scope = _fixture.ScopeFactory.CreateScope();
            var subMgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamSubmissionManager>();

            await EnsureAllOpenReviewedAsync(ids, TeacherCaller());
            var dto = new FinalizeSubmissionDto { FinalGrade = 6m, OverrideReason = "Reason" };
            await subMgr.FinalizeAsync(ids.SubmissionId, dto, TeacherCaller());
            await subMgr.FinalizeAsync(ids.SubmissionId, dto, TeacherCaller());

            using var verify = _fixture.ScopeFactory.CreateScope();
            var ctx = verify.ServiceProvider.GetRequiredService<ElectronicRegisterAPI.Infrastructure.Persistence.ElectronicRegisterContext>();
            var grades = await ctx.Grades.Where(g => g.StudentId == ids.StudentId).ToListAsync();
            Assert.Single(grades);
            var audits = await ctx.AuditEvents.Where(a => a.EntityId == ids.SubmissionId && a.Action.Contains("finalize")).ToListAsync();
            Assert.Single(audits);
        }

        [SkippableFact]
        public async Task ConcurrentDifferentOverrides_OnlyOneWins()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await _fixture.PreparePendingReviewScenarioAsync(IntegrationScenario.ExamRegression);

            using var s1 = _fixture.ScopeFactory.CreateScope();
            using var s2 = _fixture.ScopeFactory.CreateScope();

            var mgr1 = s1.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamSubmissionManager>();
            var mgr2 = s2.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamSubmissionManager>();

            var dto1 = new FinalizeSubmissionDto { FinalGrade = 6m, OverrideReason = "First" };
            var dto2 = new FinalizeSubmissionDto { FinalGrade = 8m, OverrideReason = "Second" };

            var t1 = Task.Run(async () => { try { await mgr1.FinalizeAsync(ids.SubmissionId, dto1, TeacherCaller()); return (Exception?)null; } catch (Exception ex) { return ex; } });
            var t2 = Task.Run(async () => { try { await mgr2.FinalizeAsync(ids.SubmissionId, dto2, TeacherCaller()); return (Exception?)null; } catch (Exception ex) { return ex; } });

            var results = await Task.WhenAll(t1, t2);
            // ensure at least one succeeded (the losing request may be rejected due to finalization)
            Assert.True(results.Any(r => r == null));

            using var verify = _fixture.ScopeFactory.CreateScope();
            var ctx = verify.ServiceProvider.GetRequiredService<ElectronicRegisterAPI.Infrastructure.Persistence.ElectronicRegisterContext>();
            var grades = await ctx.Grades.Where(g => g.StudentId == ids.StudentId).ToListAsync();
            Assert.Single(grades);
        }

        [SkippableFact]
        public async Task AfterOverride_ResultReturnsOfficialGradeAndPassed()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await _fixture.PreparePendingReviewScenarioAsync(IntegrationScenario.ExamRegression);

            using var scope = _fixture.ScopeFactory.CreateScope();
            var mgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamManager>();
            var subMgr = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Managers.IExamSubmissionManager>();

            await EnsureAllOpenReviewedAsync(ids, TeacherCaller());
            var dto = new FinalizeSubmissionDto { FinalGrade = 5m, OverrideReason = "Reason" };
            await subMgr.FinalizeAsync(ids.SubmissionId, dto, TeacherCaller());

            var res = await mgr.GetResultAsync(ids.SubmissionId, TeacherCaller());
            Assert.Equal(5m, res.Grade);
            Assert.False(res.Passed.Value);
            using var verify = _fixture.ScopeFactory.CreateScope();
            var ctx = verify.ServiceProvider.GetRequiredService<ElectronicRegisterAPI.Infrastructure.Persistence.ElectronicRegisterContext>();
            var subEntity = await ctx.ExamSubmissions.FindAsync(ids.SubmissionId);
            Assert.NotNull(subEntity!.SuggestedGrade);
        }
    }
}
