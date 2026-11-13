using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Tests.Integration;

internal sealed class FixtureSubmissionClient(IServiceProvider services)
{
    public Task<List<SubmissionSummaryDto>> GetByExamAsync(Guid id, SubmissionStatus? status, FixtureActor actor) => FixtureTenant.For(services, actor.UserId).SubmissionsAsync(id, status);
    public Task<SubmissionReviewDto> GetByIdAsync(Guid id, FixtureActor actor) => FixtureTenant.Read(services, actor.UserId).Roles.Contains(InstitutionRole.Student) ? FixtureTenant.For(services, actor.UserId).OwnWorkAsync(id) : FixtureTenant.For(services, actor.UserId).ReviewAsync(id);
    public Task ReviewAsync(Guid id, ReviewOpenAnswersDto dto, FixtureActor actor) => FixtureTenant.For(services, actor.UserId).ReviewAsync(id, dto);
    public Task FinalizeAsync(Guid id, FinalizeSubmissionDto dto, FixtureActor actor) => FixtureTenant.For(services, actor.UserId).FinalizeAsync(id, dto);
}
