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
    public class ExamSecurityTests
    {
        private readonly IntegrationFixture _fixture;

        public ExamSecurityTests(IntegrationFixture fixture)
        {
            _fixture = fixture;
        }

        private FixtureActor TeacherCaller(Guid teacherId) => new FixtureActor(teacherId == SeedData.TeacherId ? SeedData.TeacherUserId : SeedData.Id(0, 13), null, teacherId);
        private FixtureActor AdminCaller() => new FixtureActor(SeedData.AdminUserId, null, null);
        private FixtureActor StudentCaller(Guid studentId) => new FixtureActor(studentId, studentId, null);

        private Task<ScenarioIds> ResetAsync() => _fixture.ResetScenarioAsync(IntegrationScenario.ExamRegression);

        [SkippableFact]
        public async Task TeacherNonOwner_CannotListExamSubmissions()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await ResetAsync();

            using var scope = _fixture.ScopeFactory.CreateScope();
            var mgr = scope.ServiceProvider.GetRequiredService<FixtureSubmissionClient>();

            var caller = TeacherCaller(Guid.NewGuid()); // different teacher

            await Assert.ThrowsAsync<System.Collections.Generic.KeyNotFoundException>(async () => await mgr.GetByExamAsync(ids.ExamId, null, caller));
        }

        [SkippableFact]
        public async Task Student_CannotSeeCorrectOptions()
        {
            Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason);
            var ids = await ResetAsync();

            using var scope = _fixture.ScopeFactory.CreateScope();
            var mgr = scope.ServiceProvider.GetRequiredService<FixtureSubmissionClient>();

            var studentCaller = StudentCaller(ids.StudentId);
            var dto = await mgr.GetByIdAsync(ids.SubmissionId, studentCaller);

            // The correct option id in the scenario should not be revealed to the student
            var anyCorrectVisible = dto.Answers.SelectMany(a => a.Options).Any(o => o.Id == ids.CorrectOptionId && o.IsCorrect);
            Assert.False(anyCorrectVisible);

            // Teacher should see the correct option
            var teacherCaller = TeacherCaller(SeedData.TeacherId);
            var teacherDto = await mgr.GetByIdAsync(ids.SubmissionId, teacherCaller);
            var teacherSeesCorrect = teacherDto.Answers.SelectMany(a => a.Options).Any(o => o.Id == ids.CorrectOptionId && o.IsCorrect);
            Assert.True(teacherSeesCorrect);
        }
    }
}
