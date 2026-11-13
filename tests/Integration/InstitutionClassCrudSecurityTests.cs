using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Api.Security;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
using ElectronicRegisterAPI.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace ElectronicRegisterAPI.Tests.Integration;

public sealed partial class InstitutionClassCrudTests
{
    private async Task<Guid> Edition()
    {
        var response = await Request("/api/institution/course-editions", await Access(), new
        { StudyPathId = Id("path"), Code = "CLASS", Name = "Edizione", Status = "planned" }, "POST");
        await Expect(response, HttpStatusCode.Created);
        return await response.Content.ReadFromJsonAsync<Guid>();
    }
    [Fact]
    public async Task NewClassWorksWithStudentCrudAndDedicatedCourseEditionAssignment()
    {
        var id = await CreateClass(); var token = await Access(); var edition = await Edition();
        var student = await Request("/api/institution/students?classId=" + id, token,
            new { FirstName = "New", LastName = "Student" }, "POST");
        await Expect(student, HttpStatusCode.Created);
        var studentId = await student.Content.ReadFromJsonAsync<Guid>();
        await Expect(await Request($"/api/institution/course-editions/{edition}/classes/{id}", token, method: "POST"), HttpStatusCode.NoContent);
        await Db(async db => Assert.Equal(id, (await db.Students.SingleAsync(s => s.Id == studentId)).ClassId));
        Assert.Equal(edition, (await Detail(id)).CourseEditionId);
        await Expect(await Request(Classes + "/" + id, token, new { Name = "Renamed" }, "PUT", etag: "\"1\""), HttpStatusCode.NoContent);
        Assert.Equal(edition, (await Detail(id)).CourseEditionId);
        var filtered = (await (await Request(Classes + "?courseEditionId=" + edition, token)).Content.ReadFromJsonAsync<InstitutionClassPageDto>())!;
        Assert.Equal(id, Assert.Single(filtered.Items).Id);
        await Expect(await Request(Classes + "/" + id, token, method: "DELETE"), HttpStatusCode.Conflict);
    }
    [Fact]
    public async Task DeleteWithEditionAndAdministrativeHistoryPreservesEveryReference()
    {
        var id = await CreateClass(); var edition = await Edition(); var token = await Access();
        await Expect(await Request($"/api/institution/course-editions/{edition}/classes/{id}", token, method: "POST"), HttpStatusCode.NoContent);
        await Db(async db => Assert.True(await db.CourseAdministrationEvents.AnyAsync(e => e.InstitutionId == _a && e.ClassId == id)));
        var before = await Detail(id);
        await Expect(await Request(Classes + "/" + id, token, method: "DELETE"), HttpStatusCode.Conflict);
        Assert.Equal(before, await Detail(id));
        await Db(async db => Assert.True(await db.CourseAdministrationEvents.AnyAsync(e => e.InstitutionId == _a && e.ClassId == id)));
    }
    [Fact]
    public async Task ClassWithExamsSubmissionsAndGradesCannotBeDeletedOrMutatedIndirectly()
    {
        // Existing assigned class: create a genuine exam and attempt through the institutional API.
        var token = await Access();
        var examResponse = await Request(Exams, token, new CreateExamDto
        {
            ClassSubjectId = Id("assignment"), AvailableFrom = DateTime.UtcNow.AddMinutes(-5), AvailableUntil = DateTime.UtcNow.AddHours(1),
            DurationMinutes = 30, PassingPercent = 60, AssignedStudents = new() { new() { StudentId = Id("student") } },
            Questions = new() { new() { Position = 0, Text = "Question", Type = ElectronicRegisterAPI.Domain.Enums.QuestionType.Single,
                Points = 10, Required = true, Options = new() { new() { Position = 0, Text = "Yes", IsCorrect = true }, new() { Position = 1, Text = "No", IsCorrect = false } } } }
        }, "POST");
        await Expect(examResponse, HttpStatusCode.Created); var examId = await examResponse.Content.ReadFromJsonAsync<Guid>();
        await Expect(await Request(Exams + "/" + examId + "/publish", token, method: "POST"), HttpStatusCode.NoContent);
        var startResponse = await Request(Exams + "/" + examId + "/start", await Access("student"), method: "POST");
        await Expect(startResponse, HttpStatusCode.OK); var start = (await startResponse.Content.ReadFromJsonAsync<StartExamDto>())!;
        Guid correct = default;
        await Db(async db => correct = await db.QuestionOptions.Where(o => o.Question.ExamId == examId && o.IsCorrect).Select(o => o.Id).SingleAsync());
        await Expect(await Request(Submission(start.SubmissionId, "submit"), await Access("student"), new SubmitExamDto
        { ClientRevision = 1, Answers = new() { new() { QuestionId = start.Exam.Questions[0].Id, SelectedOptionIds = new() { correct } } } }, "POST"), HttpStatusCode.OK);
        var before = await Digest(false);
        await Expect(await Request(Classes + "/" + Id("class"), token, method: "DELETE"), HttpStatusCode.Conflict);
        await Expect(await Request(Classes + "/" + Id("class"), token, new { Name = "Renamed safely" }, "PUT", etag: "\"0\""), HttpStatusCode.NoContent);
        Assert.Equal(before, await Digest(false));
        Assert.Equal("Matematica", start.Exam.Name);
    }
    [Theory]
    [InlineData("legacy")] [InlineData("platform")]
    public async Task OtherTokenPurposesCannotEnterTheInstitutionClassSurface(string kind)
    {
        var pieces = (await Access()).Split('.');
        var payload = JsonNode.Parse(Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Decode(pieces[1]))!.AsObject();
        var header = JsonNode.Parse(Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Decode(pieces[0]))!.AsObject();
        if (kind == "legacy")
        {
            header["typ"] = "JWT"; payload["aud"] = "mt2-client"; payload["role"] = "admin";
            foreach (var key in new[] { "purpose", "institution_id", "membership_id", "institution_role", "security_generation" }) payload.Remove(key);
        }
        else payload["institution_role"] = "PlatformOperator";
        pieces[0] = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(header.ToJsonString());
        pieces[1] = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(payload.ToJsonString());
        var input = pieces[0] + "." + pieces[1];
        var signed = input + "." + Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(System.Security.Cryptography.HMACSHA256.HashData(Encoding.UTF8.GetBytes(Mt3cHosting.Key), Encoding.UTF8.GetBytes(input)));
        await Expect(await Request(Classes, signed), HttpStatusCode.Unauthorized);
    }
    [Fact]
    public async Task ExactClassRouteInventoryUsesVerifiedInstitutionPolicyAndAdminOnly()
    {
        var endpoints = _host.Host.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText!.StartsWith("api/institution/classes")).ToArray();
        Assert.Equal(5, endpoints.Length);
        Assert.Equal(new[] { "DELETE", "GET", "GET", "POST", "PUT" }, endpoints.Select(e => Assert.Single(e.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods)).OrderBy(x => x));
        foreach (var endpoint in endpoints)
        {
            Assert.NotNull(endpoint.Metadata.GetMetadata<InstitutionSchoolEndpointAttribute>());
            Assert.Null(endpoint.Metadata.GetMetadata<IAllowAnonymous>());
            var auth = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
            Assert.Equal(InstitutionSchoolEndpointAttribute.PolicyName, Assert.Single(auth.Where(a => a.Policy is not null)).Policy);
            Assert.Equal("InstitutionAdmin", Assert.Single(auth.Where(a => a.Roles is not null)).Roles);
            var action = endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>()!;
            Assert.Equal(new[] { typeof(ElectronicRegisterAPI.Domain.Interfaces.Managers.IInstitutionClassManager) }, action.ControllerTypeInfo.DeclaredConstructors.Single().GetParameters().Select(p => p.ParameterType));
        }
        var policy = await _host.Host.Services.GetRequiredService<IAuthorizationPolicyProvider>().GetPolicyAsync(InstitutionSchoolEndpointAttribute.PolicyName);
        Assert.Equal(new[] { InstitutionTokenContract.AccessScheme }, policy!.AuthenticationSchemes);
    }
    [Theory]
    [InlineData("POST")] [InlineData("PUT")] [InlineData("DELETE")]
    public async Task FailureAfterAuditRollsBackMutationAndLedgerTogether(string method)
    {
        var id = await CreateClass(); var before = await Detail(id); var token = await Access();
        await using var host = await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString, _a, Array.Empty<Guid>(), enabled: false, school: true,
            configureServices: services =>
            {
                var original = services.Last(s => s.ServiceType == typeof(IInstitutionClassRepository));
                services.AddScoped<IInstitutionClassRepository>(sp => new FailAfterAudit((IInstitutionClassRepository)ActivatorUtilities.CreateInstance(sp, original.ImplementationType!)));
            });
        object? body = method == "POST" ? new { Name = "Rollback", BienniumStudyPathId = Id("bsp") } : method == "PUT" ? new { Name = "Rollback" } : null;
        var response = await Request(Classes + (method == "POST" ? "" : "/" + id), token, body, method, host: host, etag: "\"1\"");
        await Expect(response, HttpStatusCode.InternalServerError);
        Assert.DoesNotContain("Injected", await response.Content.ReadAsStringAsync());
        Assert.Equal(before, await Detail(id));
        await Db(async db =>
        {
            Assert.False(await db.Classes.AnyAsync(c => EF.Property<Guid>(c, "InstitutionId") == _a && c.Name == "Rollback"));
            Assert.Equal(1, await db.Set<InstitutionClassEvent>().CountAsync(e => e.InstitutionId == _a));
        });
    }
    private sealed class FailAfterAudit(IInstitutionClassRepository inner) : IInstitutionClassRepository
    {
        public Task<InstitutionClassPageDto> GetAllAsync(TenantContext t, int p, int s, Guid? path, Guid? edition, CancellationToken ct) => inner.GetAllAsync(t,p,s,path,edition,ct);
        public Task<InstitutionClassDto?> GetByIdAsync(TenantContext t, Guid id, CancellationToken ct) => inner.GetByIdAsync(t,id,ct);
        public async Task AddAsync(TenantContext t, Guid id, string name, Guid path, CancellationToken ct) { await inner.AddAsync(t,id,name,path,ct); throw new InvalidOperationException("Injected after audit"); }
        public async Task UpdateAsync(TenantContext t, Guid id, string name, long revision, CancellationToken ct) { await inner.UpdateAsync(t,id,name,revision,ct); throw new InvalidOperationException("Injected after audit"); }
        public async Task DeleteAsync(TenantContext t, Guid id, CancellationToken ct) { await inner.DeleteAsync(t,id,ct); throw new InvalidOperationException("Injected after audit"); }
    }
}
