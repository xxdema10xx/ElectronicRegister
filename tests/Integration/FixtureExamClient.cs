using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Tests.Integration;

// Test client preserves scenario readability while invoking the real canonical
// application boundary, including live authorization and scoped repositories.
internal sealed class FixtureExamClient(IServiceProvider services)
{
    public Task<List<ExamSummaryDto>> GetAllAsync(FixtureActor actor) => FixtureTenant.For(services, actor.UserId).ExamsAsync();
    public Task<ExamDetailDto?> GetByIdAsync(Guid id, FixtureActor actor) => FixtureTenant.For(services, actor.UserId).ExamAsync(id);
    public Task<List<AssignedExamDto>> GetAssignedAsync(FixtureActor actor) => FixtureTenant.For(services, actor.UserId).AssignedAsync();
    public Task<Guid> AddAsync(CreateExamDto dto, FixtureActor actor) => FixtureTenant.For(services, actor.UserId).CreateAsync(dto);
    public Task UpdateAsync(Guid id, UpdateExamDto dto, FixtureActor actor) => FixtureTenant.For(services, actor.UserId).UpdateAsync(id, dto);
    public Task UpdateStudentsAsync(Guid id, UpdateExamStudentsDto dto, FixtureActor actor) => FixtureTenant.For(services, actor.UserId).AssignAsync(id, dto);
    public Task PublishAsync(Guid id, FixtureActor actor) => FixtureTenant.For(services, actor.UserId).PublishAsync(id);
    public Task CloseAsync(Guid id, FixtureActor actor) => FixtureTenant.For(services, actor.UserId).CloseAsync(id);
    public Task CancelAsync(Guid id, FixtureActor actor) => FixtureTenant.For(services, actor.UserId).CancelAsync(id);
    public Task DeleteAsync(Guid id, FixtureActor actor) => FixtureTenant.For(services, actor.UserId).DeleteAsync(id);
    public Task<StartExamDto> StartAsync(Guid id, FixtureActor actor) => FixtureTenant.For(services, actor.UserId).StartAsync(id);
    public Task<SaveAnswersResponseDto> SaveAnswersAsync(Guid id, SaveExamAnswersDto dto, FixtureActor actor) => FixtureTenant.For(services, actor.UserId).SaveAsync(id, dto);
    public Task<SubmissionResultDto> SubmitAsync(Guid id, SubmitExamDto dto, FixtureActor actor) => FixtureTenant.For(services, actor.UserId).SubmitAsync(id, dto);
    public Task<SubmissionResultDto> GetResultAsync(Guid id, FixtureActor actor) => FixtureTenant.For(services, actor.UserId).ResultAsync(id);
}
