using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Linq;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;
public sealed partial class InstitutionClassCrudTests
{
    [Theory]
    [InlineData("Create")]
    [InlineData("Update")]
    [InlineData("Delete")]
    public async Task AssignmentFaultAfterAuditRollsBackEveryMutation(string operation)
    {
        var id = await CreateAssignment();
        var token = await Access();
        await using var host = await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString, _a, Array.Empty<Guid>(), enabled: false, school: true, exam: true, configureServices: services =>
        {
            var d = services.Last(s => s.ServiceType == typeof(IInstitutionClassSubjectRepository));
            services.AddScoped<IInstitutionClassSubjectRepository>(sp => new AssignmentFault((IInstitutionClassSubjectRepository)ActivatorUtilities.CreateInstance(sp, d.ImplementationType!), operation + "Async"));
        });
        var method = operation == "Create" ? "POST" : operation == "Update" ? "PUT" : "DELETE";
        object? body = method == "POST" ? AssignmentInput(Id("class-other"), Id("subject-other")) : method == "PUT" ? new
        {
            TeacherId = Id("teacher-other")
        }

        : null;
        // The alternative create pair must be genuinely unused.
        if (method == "POST")
            body = AssignmentInput(Id("class"), Id("subject"));
        if (method == "POST")
        {
            var response = await Request("/api/institution/subjects", token, new { Name = "Rollback subject" }, "POST");
            await Expect(response, HttpStatusCode.Created);
            var subject = await System.Net.Http.Json.HttpContentJsonExtensions.ReadFromJsonAsync<Guid>(response.Content);
            body = AssignmentInput(subjectId: subject);
        }

        await Expect(await Request(Assignments + (method == "POST" ? "" : "/" + id), token, body, method, host: host, etag: "\"1\""), HttpStatusCode.InternalServerError);
        await Db(async db =>
        {
            Assert.Equal(Id("teacher"), (await db.ClassSubjects.SingleAsync(r => r.Id == id)).TeacherId);
            Assert.Equal(1, await db.Set<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.InstitutionAdministrationEvent>().CountAsync(e => e.InstitutionId == _a && e.EntityType == "class_subject"));
            Assert.Equal(4, await db.ClassSubjects.CountAsync(r => EF.Property<Guid>(r, "InstitutionId") == _a));
        });
    }

    private sealed class AssignmentFault(IInstitutionClassSubjectRepository inner, string fail) : IInstitutionClassSubjectRepository
    {
        public async Task<InstitutionAcademicPage<InstitutionClassSubjectDto>> ListAsync(TenantContext t, int page, int size, Guid? classId, Guid? subjectId, Guid? teacherId, CancellationToken ct)
        {
            var result = await inner.ListAsync(t, page, size, classId, subjectId, teacherId, ct);
            if (fail == nameof(ListAsync))
                throw new InvalidOperationException("Injected after audit");
            return result;
        }

        public async Task<InstitutionClassSubjectDto?> GetAsync(TenantContext t, Guid id, CancellationToken ct)
        {
            var result = await inner.GetAsync(t, id, ct);
            if (fail == nameof(GetAsync))
                throw new InvalidOperationException("Injected after audit");
            return result;
        }

        public async Task<Guid> CreateAsync(TenantContext t, CreateInstitutionClassSubjectDto dto, CancellationToken ct)
        {
            var result = await inner.CreateAsync(t, dto, ct);
            if (fail == nameof(CreateAsync))
                throw new InvalidOperationException("Injected after audit");
            return result;
        }

        public async Task UpdateAsync(TenantContext t, Guid id, UpdateInstitutionClassSubjectDto dto, long revision, CancellationToken ct)
        {
            await inner.UpdateAsync(t, id, dto, revision, ct);
            if (fail == nameof(UpdateAsync))
                throw new InvalidOperationException("Injected after audit");
        }

        public async Task DeleteAsync(TenantContext t, Guid id, CancellationToken ct)
        {
            await inner.DeleteAsync(t, id, ct);
            if (fail == nameof(DeleteAsync))
                throw new InvalidOperationException("Injected after audit");
        }
    }
}
