using System;
using System.Threading.Tasks;
using Xunit;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Tests.Integration
{
    [Collection(IntegrationCollection.Name)]
    public class FinalizeConcurrencyTests
    {
        private readonly IntegrationFixture _fixture;

        public FinalizeConcurrencyTests(IntegrationFixture fixture)
        {
            _fixture = fixture;
        }

        [SkippableFact]
        public async Task ConcurrentFinalize_CreatesSingleGrade()
        {
            // Two tasks call FinalizeAsync concurrently on the same submission; only one grade may be created.

            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);

            // Dedicated submission: pending review, open answer already reviewed, no grade yet
            var ids = await _fixture.ResetScenarioAsync(IntegrationScenario.ConcurrentFinalize);

            // Create two separate scopes and resolve managers independently to ensure separate DbContexts
            using var scope1 = _fixture.ScopeFactory.CreateScope();
            using var scope2 = _fixture.ScopeFactory.CreateScope();

            var manager1 = scope1.ServiceProvider.GetRequiredService<IExamSubmissionManager>();
            var manager2 = scope2.ServiceProvider.GetRequiredService<IExamSubmissionManager>();

            // Use seeded teacher id for caller
            var caller = new ClaimsContext(Guid.NewGuid(), Domain.Enums.UserRole.Teacher, null, SeedData.TeacherId);

            // Synchronize the start of both tasks
            var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var t1 = Task.Run(async () =>
            {
                // wait for signal
                await gate.Task;
                try { await manager1.FinalizeAsync(ids.SubmissionId, new FinalizeSubmissionDto(), caller); } catch (Exception ex) { return ex; }
                return (Exception?)null;
            });

            var t2 = Task.Run(async () =>
            {
                await gate.Task;
                try { await manager2.FinalizeAsync(ids.SubmissionId, new FinalizeSubmissionDto(), caller); } catch (Exception ex) { return ex; }
                return (Exception?)null;
            });

            // release both tasks
            gate.SetResult(true);

            var results = await Task.WhenAll(t1, t2);

            // Finalize is idempotent: the second call must find the submission already finalized, not fail
            var exceptions = results.Where(r => r != null).ToList();
            Assert.True(exceptions.Count == 0, "FinalizeAsync ha sollevato eccezioni: " + string.Join(" | ", exceptions.Select(e => e!.ToString())));

            // Verify final state via a fresh scope/context
            using var verifyScope = _fixture.ScopeFactory.CreateScope();
            var verifyRepo = verifyScope.ServiceProvider.GetRequiredService<IExamSubmissionRepository>();
            var updated = await verifyRepo.GetByIdAsync(ids.SubmissionId, withAnswers: true);
            Assert.NotNull(updated);
            Assert.Equal(Domain.Enums.SubmissionStatus.Finalized, updated.Status);
            Assert.NotNull(updated.GradeId);

            // Verify grade exists and there is exactly one grade for the scenario student
            var gradeRepo = verifyScope.ServiceProvider.GetRequiredService<IGradeRepository>();
            var gradesForStudent = await gradeRepo.GetByStudentIdAsync(ids.StudentId);
            Assert.Single(gradesForStudent);
            Assert.Equal(updated.GradeId!.Value, gradesForStudent.First().Id);

            // Only one finalization was actually processed
            var ctx = verifyScope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
            var finalizeEvents = await ctx.AuditEvents.CountAsync(a => a.EntityId == ids.SubmissionId && a.Action == "submission.finalize");
            Assert.Equal(1, finalizeEvents);
        }
    }
}
