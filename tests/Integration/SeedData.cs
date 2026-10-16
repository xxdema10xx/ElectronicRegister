using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

namespace ElectronicRegisterAPI.Tests.Integration
{
    // One scenario per test: each owns its student, exam and submission, so no test observes another test's writes.
    public enum IntegrationScenario
    {
        ConcurrentSaveAnswers = 1,
        ConcurrentSubmit = 2,
        AutosaveAfterDeadline = 3,
        AutosaveConcurrentToSubmit = 4,
        ConcurrentFinalize = 5,
        ExamRegression = 6
        , ClosedOnly = 7
    }

    public sealed record ScenarioIds(
        Guid StudentId,
        Guid ExamId,
        Guid OpenQuestionId,
        Guid SingleQuestionId,
        Guid CorrectOptionId,
        Guid WrongOptionId,
        Guid SubmissionId,
        Guid OpenAnswerId,
        Guid SingleAnswerId)
    {
        public static ScenarioIds For(IntegrationScenario scenario)
        {
            var n = (int)scenario;
            return new ScenarioIds(
                SeedData.Id(n, 1), SeedData.Id(n, 2), SeedData.Id(n, 3), SeedData.Id(n, 4), SeedData.Id(n, 5),
                SeedData.Id(n, 6), SeedData.Id(n, 7), SeedData.Id(n, 8), SeedData.Id(n, 9));
        }
    }

    internal static class SeedData
    {
        public const string InitialOpenText = "Bozza iniziale";

        // Deterministic ids: 1e57SSSS-0000-0000-0000-NNNNNNNNNNNN, scenario 0 = shared reference data.
        public static Guid Id(int scenario, int n) => Guid.Parse($"1e57{scenario:D4}-0000-0000-0000-{n:D12}");

        // Shared reference data (never modified by tests). Names and biennium years are chosen to avoid
        // colliding with the unique indexes of real data.
        public static readonly Guid BienniumId = Id(0, 1);
        public static readonly Guid StudyAreaId = Id(0, 2);
        public static readonly Guid StudyPathId = Id(0, 3);
        public static readonly Guid BienniumStudyAreaId = Id(0, 4);
        public static readonly Guid BienniumStudyPathId = Id(0, 5);
        public static readonly Guid ClassId = Id(0, 6);
        public static readonly Guid TeacherId = Id(0, 7);
        public static readonly Guid OtherTeacherId = Id(0, 12);
        public static readonly Guid SubjectId = Id(0, 8);
        public static readonly Guid ClassSubjectId = Id(0, 9);
        // Persisted integration users (authenticated identities)
        public static readonly Guid TeacherUserId = Id(0, 10);
        public static readonly Guid AdminUserId = Id(0, 11);

        public static async Task EnsureBaseDataAsync(ElectronicRegisterContext ctx)
        {
            // Each row is checked by id (not by "any rows exist"), and an existing row must keep the expected references.
            await EnsureAsync(ctx, new Biennium { Id = BienniumId, StartYear = new DateOnly(2090, 9, 1), EndYear = new DateOnly(2092, 8, 31) });
            await EnsureAsync(ctx, new StudyArea { Id = StudyAreaId, Name = "IT Integration Area", Description = "Integration tests" });
            await EnsureAsync(ctx, new StudyPath { Id = StudyPathId, Name = "IT Integration Path", Description = "Integration tests" });
            await EnsureAsync(ctx, new BienniumStudyArea { Id = BienniumStudyAreaId, BienniumId = BienniumId, StudyAreaId = StudyAreaId },
                e => e.BienniumId == BienniumId && e.StudyAreaId == StudyAreaId);
            await EnsureAsync(ctx, new BienniumStudyPath { Id = BienniumStudyPathId, BienniumStudyAreaId = BienniumStudyAreaId, StudyPathId = StudyPathId },
                e => e.BienniumStudyAreaId == BienniumStudyAreaId && e.StudyPathId == StudyPathId);
            await EnsureAsync(ctx, new Class { Id = ClassId, BienniumStudyPathId = BienniumStudyPathId, Name = "IT-1A" },
                e => e.BienniumStudyPathId == BienniumStudyPathId);
            await EnsureAsync(ctx, new Teacher { Id = TeacherId, FirstName = "IT", LastName = "Teacher" });
            await EnsureAsync(ctx, new Teacher { Id = OtherTeacherId, FirstName = "Other", LastName = "Teacher" });
            await EnsureAsync(ctx, new Subject { Id = SubjectId, Name = "IT Integration Subject" });
            await EnsureAsync(ctx, new ClassSubject { Id = ClassSubjectId, ClassId = ClassId, SubjectId = SubjectId, TeacherId = TeacherId },
                e => e.ClassId == ClassId && e.SubjectId == SubjectId && e.TeacherId == TeacherId);

            await ctx.SaveChangesAsync();

            // Ensure persisted users that map to the seeded teacher/admin
            await EnsureAsync(ctx, new ElectronicRegisterAPI.Infrastructure.Persistence.Entities.User
            {
                Id = TeacherUserId,
                Email = "integration.teacher@example.test",
                PasswordHash = "",
                Role = "teacher",
                TeacherId = TeacherId
            }, e => e.TeacherId == TeacherId);

            await EnsureAsync(ctx, new ElectronicRegisterAPI.Infrastructure.Persistence.Entities.User
            {
                Id = Id(0, 13),
                Email = "integration.other.teacher@example.test",
                PasswordHash = "",
                Role = "teacher",
                TeacherId = OtherTeacherId
            }, e => e.TeacherId == OtherTeacherId);

            await EnsureAsync(ctx, new ElectronicRegisterAPI.Infrastructure.Persistence.Entities.User
            {
                Id = AdminUserId,
                Email = "integration.admin@example.test",
                PasswordHash = "",
                Role = "admin",
                TeacherId = null
            }, e => e.Role == "admin");

            await ctx.SaveChangesAsync();
        }

        public static async Task<ScenarioIds> ResetScenarioAsync(ElectronicRegisterContext ctx, IntegrationScenario scenario)
        {
            var ids = ScenarioIds.For(scenario);

            await using var transaction = await ctx.Database.BeginTransactionAsync();

            await DeleteScenarioAsync(ctx, ids);
            AddScenario(ctx, scenario, ids);
            await ctx.SaveChangesAsync();

            await transaction.CommitAsync();
            ctx.ChangeTracker.Clear();

            return ids;
        }

        // Removes only rows owned by the scenario (by id), including rows the application created during a previous run.
        private static async Task DeleteScenarioAsync(ElectronicRegisterContext ctx, ScenarioIds ids)
        {
            var submissionIds = await ctx.ExamSubmissions
                .Where(s => s.Id == ids.SubmissionId || s.StudentId == ids.StudentId || s.ExamId == ids.ExamId)
                .Select(s => s.Id)
                .ToListAsync();

            var answerIds = await ctx.SubmissionAnswers
                .Where(a => submissionIds.Contains(a.SubmissionId))
                .Select(a => a.Id)
                .ToListAsync();

            await ctx.Set<Dictionary<string, object>>("SubmissionAnswerOption")
                .Where(j => answerIds.Contains(EF.Property<Guid>(j, "SubmissionAnswerId")))
                .ExecuteDeleteAsync();
            await ctx.SubmissionAnswers.Where(a => submissionIds.Contains(a.SubmissionId)).ExecuteDeleteAsync();
            await ctx.ExamViolations.Where(v => submissionIds.Contains(v.SubmissionId)).ExecuteDeleteAsync();
            await ctx.AuditEvents.Where(a => a.EntityId.HasValue && submissionIds.Contains(a.EntityId.Value)).ExecuteDeleteAsync();
            await ctx.ExamSubmissions.Where(s => submissionIds.Contains(s.Id)).ExecuteDeleteAsync();
            await ctx.Grades.Where(g => g.StudentId == ids.StudentId).ExecuteDeleteAsync();
            await ctx.ExamStudents.Where(es => es.ExamId == ids.ExamId || es.StudentId == ids.StudentId).ExecuteDeleteAsync();

            var questionIds = await ctx.Questions.Where(q => q.ExamId == ids.ExamId).Select(q => q.Id).ToListAsync();
            await ctx.QuestionOptions.Where(o => questionIds.Contains(o.QuestionId)).ExecuteDeleteAsync();
            await ctx.Questions.Where(q => q.ExamId == ids.ExamId).ExecuteDeleteAsync();
            await ctx.Exams.Where(e => e.Id == ids.ExamId).ExecuteDeleteAsync();
            // Remove the scenario student and any User row that was created for that student
            await ctx.Students.Where(s => s.Id == ids.StudentId).ExecuteDeleteAsync();
            await ctx.Users.Where(u => u.Id == ids.StudentId).ExecuteDeleteAsync();
        }

        private static void AddScenario(ElectronicRegisterContext ctx, IntegrationScenario scenario, ScenarioIds ids)
        {
            var now = DateTime.UtcNow;
            var n = (int)scenario;

            ctx.Students.Add(new Student { Id = ids.StudentId, FirstName = "IT", LastName = $"Student{n}", ClassId = ClassId });
            // Also create a corresponding persisted User for the student so ClaimsContext.UserId references exist
            ctx.Users.Add(new ElectronicRegisterAPI.Infrastructure.Persistence.Entities.User
            {
                Id = ids.StudentId,
                Email = $"student{n}.integration@example.test",
                PasswordHash = string.Empty,
                Role = "student",
                StudentId = ids.StudentId
            });

            ctx.Exams.Add(new Exam
            {
                Id = ids.ExamId,
                ClassSubjectId = ClassSubjectId,
                Name = $"IT Exam {scenario}",
                AvailableFrom = now.AddHours(-2),
                AvailableUntil = now.AddHours(2),
                DurationMinutes = 60,
                PassingPercent = 60m,
                TotalPoints = 10m,
                Status = "published",
                PublishedAt = now.AddHours(-2),
                CreatedAt = now.AddHours(-3),
                UpdatedAt = now.AddHours(-2)
            });

            ctx.ExamStudents.Add(new ExamStudent { ExamId = ids.ExamId, StudentId = ids.StudentId, ExtraTimeMinutes = 0, AssignedAt = now.AddHours(-2) });

            var qOpen = new Question { Id = ids.OpenQuestionId, ExamId = ids.ExamId, Position = 1, Text = "Domanda aperta", Type = "open", Points = 5m, Required = false };
            var qSingle = new Question { Id = ids.SingleQuestionId, ExamId = ids.ExamId, Position = 2, Text = "Domanda a risposta singola", Type = "single", Points = 5m, Required = false };
            ctx.Questions.Add(qOpen);
            ctx.Questions.Add(qSingle);

            var correctOption = new QuestionOption { Id = ids.CorrectOptionId, QuestionId = ids.SingleQuestionId, Position = 1, Text = "Risposta corretta", IsCorrect = true };
            var wrongOption = new QuestionOption { Id = ids.WrongOptionId, QuestionId = ids.SingleQuestionId, Position = 2, Text = "Risposta errata", IsCorrect = false };
            ctx.QuestionOptions.Add(correctOption);
            ctx.QuestionOptions.Add(wrongOption);

            var submission = new ExamSubmission
            {
                Id = ids.SubmissionId,
                ExamId = ids.ExamId,
                StudentId = ids.StudentId,
                ViolationCount = 0,
                MaxScore = 10m,
                PassingScore = 6m,
                CreatedAt = now.AddMinutes(-70),
                UpdatedAt = now.AddMinutes(-10)
            };

            var openAnswer = new SubmissionAnswer { Id = ids.OpenAnswerId, SubmissionId = ids.SubmissionId, QuestionId = ids.OpenQuestionId, OpenText = InitialOpenText, LastSavedAt = now.AddMinutes(-10) };
            var singleAnswer = new SubmissionAnswer { Id = ids.SingleAnswerId, SubmissionId = ids.SubmissionId, QuestionId = ids.SingleQuestionId, LastSavedAt = now.AddMinutes(-10) };
            singleAnswer.Options.Add(correctOption);

            switch (scenario)
            {
                case IntegrationScenario.AutosaveAfterDeadline:
                    // Still in progress but already past its deadline.
                    submission.Status = "in_progress";
                    submission.StartedAt = now.AddMinutes(-70);
                    submission.DeadlineAt = now.AddMinutes(-10);
                    submission.LastClientRevision = 0UL;
                    break;

                case IntegrationScenario.ConcurrentFinalize:
                    // Submitted, closed part graded, open answer already reviewed by the teacher, no grade yet.
                    submission.Status = "pending_review";
                    submission.StartedAt = now.AddMinutes(-70);
                    submission.DeadlineAt = now.AddMinutes(-10);
                    submission.SubmittedAt = now.AddMinutes(-15);
                    submission.SubmitReason = "manual";
                    submission.LastClientRevision = 1UL;
                    submission.ClosedScore = 5m;
                    submission.OpenMaxPoints = 5m;
                    openAnswer.AwardedPoints = 4m;
                    openAnswer.TeacherComment = "Revisionata";
                    break;

                default:
                    submission.Status = "in_progress";
                    submission.StartedAt = now.AddMinutes(-10);
                    submission.DeadlineAt = now.AddMinutes(50);
                    submission.LastClientRevision = 0UL;
                    break;

                case IntegrationScenario.ClosedOnly:
                    // Exam with only closed questions: mark the open question as single and move an option to it
                    qOpen.Type = "single";

                    // adjust points so that closed-only scenario yields 60% when student has one correct and one incorrect
                    // make single question worth 6 and the other worth 4 so one correct yields total 6/10
                    qOpen.Points = 4m;
                    qSingle.Points = 6m;

                    // move wrongOption to become an option for the former open question but KEEP it incorrect
                    wrongOption.QuestionId = ids.OpenQuestionId;
                    wrongOption.IsCorrect = false;

                    // ensure the open answer becomes a closed answer selecting the (incorrect) option
                    openAnswer.OpenText = null;
                    openAnswer.Options.Add(wrongOption);

                    // ensure submission metadata
                    submission.Status = "in_progress";
                    submission.StartedAt = now.AddMinutes(-10);
                    submission.DeadlineAt = now.AddMinutes(50);
                    submission.LastClientRevision = 0UL;
                    break;
            }

            ctx.ExamSubmissions.Add(submission);
            ctx.SubmissionAnswers.Add(openAnswer);
            ctx.SubmissionAnswers.Add(singleAnswer);
        }

        private static async Task EnsureAsync<T>(ElectronicRegisterContext ctx, T expected, Func<T, bool>? isConsistent = null) where T : class
        {
            var id = ctx.Entry(expected).Property<Guid>("Id").CurrentValue;
            var existing = await ctx.Set<T>().FindAsync(id);

            if (existing is null)
            {
                ctx.Set<T>().Add(expected);
                return;
            }

            if (isConsistent != null && !isConsistent(existing))
                throw new InvalidOperationException(
                    $"Il record {typeof(T).Name} {id} esiste nel database di test ma con riferimenti diversi dal seed atteso.");
        }
    }
}
