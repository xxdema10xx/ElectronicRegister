using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using E = ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

namespace ElectronicRegisterAPI.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public sealed class HistoricalExpiryBoundaryTests(IntegrationFixture fixture)
{
    [Fact]
    public async Task HistoricalOwnerRemainsTechnicalOnlyAndDeliveryIsIdempotent()
    {
        var studentActor = Phase3.Student(await fixture.ResetScenarioAsync(IntegrationScenario.ExamRegression));
        Guid owner, submission;
        using (var scope = fixture.ScopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
            owner = await db.Database.SqlQueryRaw<Guid>("SELECT institution_id AS Value FROM mt3a_legacy_scope WHERE scope='legacy'").SingleAsync();
            Assert.Equal("provisioning", (await db.Institutions.SingleAsync(i => i.Id == owner)).Status);
            await using var tx = await db.Database.BeginTransactionAsync();
            async Task Add(object entity)
            {
                var entry = db.Add(entity);
                entry.Property("InstitutionId").CurrentValue = owner;
                entry.Property("InstitutionId").IsTemporary = false;
                await db.SaveChangesAsync();
            }
            var start = new DateOnly(2150, 9, 1);
            while (await db.Set<E.Biennium>().AnyAsync(b => b.StartYear == start && EF.Property<Guid>(b, "InstitutionId") == owner)) start = start.AddYears(2);
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            var term = new E.Biennium { Id = Guid.NewGuid(), StartYear = start, EndYear = start.AddYears(2).AddDays(-1) };
            var area = new E.StudyArea { Id = Guid.NewGuid(), Name = "Historical expiry area " + suffix, Description = "Isolated historical fixture" };
            var path = new E.StudyPath { Id = Guid.NewGuid(), Name = "Historical expiry path " + suffix, Description = "Isolated historical fixture" };
            await Add(term); await Add(area); await Add(path);
            var termArea = new E.BienniumStudyArea { Id = Guid.NewGuid(), BienniumId = term.Id, StudyAreaId = area.Id };
            await Add(termArea);
            var termPath = new E.BienniumStudyPath { Id = Guid.NewGuid(), BienniumStudyAreaId = termArea.Id, StudyPathId = path.Id };
            await Add(termPath);
            var schoolClass = new E.Class { Id = Guid.NewGuid(), BienniumStudyPathId = termPath.Id, Name = suffix };
            await Add(schoolClass);
            var teacher = new E.Teacher { Id = Guid.NewGuid(), FirstName = "Historical", LastName = "Teacher" };
            var student = new E.Student { Id = Guid.NewGuid(), FirstName = "Historical", LastName = "Student", ClassId = schoolClass.Id };
            var subject = new E.Subject { Id = Guid.NewGuid(), Name = "Historical expiry subject " + suffix };
            await Add(teacher); await Add(student); await Add(subject);
            var assignment = new E.ClassSubject { Id = Guid.NewGuid(), ClassId = schoolClass.Id, TeacherId = teacher.Id, SubjectId = subject.Id };
            await Add(assignment);
            var exam = new E.Exam { Id = Guid.NewGuid(), ClassSubjectId = assignment.Id, Name = "Historical expiry exam", Status = "published", AvailableFrom = DateTime.UtcNow.AddDays(-2), AvailableUntil = DateTime.UtcNow.AddDays(-1), DurationMinutes = 60, PassingPercent = 60, TotalPoints = 10, PublishedAt = DateTime.UtcNow.AddDays(-2), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            await Add(exam);
            await Add(new E.ExamStudent { ExamId = exam.Id, StudentId = student.Id, AssignedAt = DateTime.UtcNow });
            var question = new E.Question { Id = Guid.NewGuid(), ExamId = exam.Id, Position = 1, Text = "Historical question", Type = "single", Points = 10, Required = false };
            await Add(question);
            await Add(new E.QuestionOption { Id = Guid.NewGuid(), QuestionId = question.Id, Position = 1, Text = "Answer", IsCorrect = true });
            submission = Guid.NewGuid();
            await Add(new E.ExamSubmission { Id = submission, ExamId = exam.Id, StudentId = student.Id, Status = "in_progress", StartedAt = DateTime.UtcNow.AddHours(-2), DeadlineAt = DateTime.UtcNow.AddHours(-1), MaxScore = 10, PassingScore = 6, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            await tx.CommitAsync();
        }

        await using var host = await Phase2C_HttpHost.StartAsync(fixture.ConnectionString);
        Assert.Equal(HttpStatusCode.NotFound, (await host.As(studentActor).GetAsync($"/api/institution/submissions/{submission}/work")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.As(Phase3.Teacher).GetAsync($"/api/institution/submissions/{submission}/review")).StatusCode);
        using (var scope = fixture.ScopeFactory.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<ISubmissionExpiryStore>();
            Assert.Null(await store.ResolveAsync(submission, SeedData.CoreInstitutionId, default));
            var work = await store.ResolveAsync(submission, owner, default);
            Assert.NotNull(work);
            var db = scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
            await db.Institutions.Where(i => i.Id == owner).ExecuteUpdateAsync(set => set.SetProperty(i => i.Status, "active"));
            try { Assert.Null(await store.ResolveAsync(submission, owner, default)); }
            finally { await db.Institutions.Where(i => i.Id == owner).ExecuteUpdateAsync(set => set.SetProperty(i => i.Status, "provisioning")); }
            var candidates = await store.SelectAsync(DateTime.UtcNow, null, 50, default);
            Assert.Contains(candidates, c => c.SubmissionId == submission && c.InstitutionId == owner);
        }
        async Task Deliver()
        {
            using var scope = fixture.ScopeFactory.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IInstitutionExpiryService>().ProcessAsync(new(submission, owner, DateTime.UtcNow), default);
        }
        await Task.WhenAll(Deliver(), Deliver());
        await Deliver();
        using (var scope = fixture.ScopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
            var row = await db.ExamSubmissions.AsNoTracking().SingleAsync(s => s.Id == submission);
            Assert.Equal("graded", row.Status);
            Assert.Equal("time_expired", row.SubmitReason);
            Assert.NotNull(row.GradeId);
            var grade = Assert.Single(await db.Grades.Where(g => g.StudentId == row.StudentId).ToListAsync());
            Assert.Equal(row.GradeId, grade.Id);
            Assert.Equal(owner, await db.ExamSubmissions.Where(s => s.Id == submission).Select(s => EF.Property<Guid>(s, "InstitutionId")).SingleAsync());
            Assert.Single(await db.AuditEvents.Where(a => a.EntityId == submission && a.Action == "submission.submit" && a.ActorUserId == null).ToListAsync());
            Assert.False(await db.InstitutionStudentProfiles.AnyAsync(p => p.StudentId == row.StudentId));
            Assert.Equal("provisioning", (await db.Institutions.SingleAsync(i => i.Id == owner)).Status);
        }
    }
}
