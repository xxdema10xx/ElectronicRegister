using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

public partial class Phase4_ExamLifecycleTests
{
    [Fact]
    public async Task Audit_StudyAreaDependencyUsesStudyAreaForeignKey()
    {
        using var scope = fixture.ScopeFactory.CreateScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<ElectronicRegisterAPI.Domain.Interfaces.Repositories.IStudyAreaRepository>()
            .HasStudyPathsAsync(SeedData.StudyAreaId));
    }

    [Fact]
    public async Task Audit_BienniumUpdatePreservesDateValidation()
    {
        using var setup = fixture.ScopeFactory.CreateScope();
        var db = setup.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
        var original = await db.Biennia.AsNoTracking().SingleAsync(b => b.Id == SeedData.BienniumId);
        try
        {
            var response = await _host.As(Admin).PutAsJsonAsync($"/api/Biennium/update/{original.Id}", new UpdateBienniumDto
            { StartYear = original.StartYear, EndYear = original.StartYear.AddYears(-2) });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            using var verify = fixture.ScopeFactory.CreateScope();
            Assert.Equal(original.EndYear, await verify.ServiceProvider.GetRequiredService<ElectronicRegisterContext>().Biennia
                .Where(b => b.Id == original.Id).Select(b => b.EndYear).SingleAsync());
        }
        finally
        {
            await db.Biennia.Where(b => b.Id == original.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.EndYear, original.EndYear));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Audit_UserUpdateFaultIsAtomic(bool teacherProfile)
    {
        var id = Guid.NewGuid();
        var profile = Guid.NewGuid();
        var email = "audit-user-" + id.ToString("N") + "@example.test";
        using (var setup = fixture.ScopeFactory.CreateScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
            if (teacherProfile) db.Teachers.Add(new() { Id = profile, FirstName = "Audit", LastName = "Before" });
            else db.Students.Add(new() { Id = profile, FirstName = "Audit", LastName = "Before" });
            db.Users.Add(new() { Id = id, Email = email, PasswordHash = "", Role = teacherProfile ? "teacher" : "student",
                TeacherId = teacherProfile ? profile : null, StudentId = teacherProfile ? null : profile });
            await db.SaveChangesAsync();
        }
        var probe = new AuditUserUpdateProbe(id);
        try
        {
            await using (var provider = Phase3.Provider(fixture, null, probe))
            using (var scope = provider.CreateScope())
                await Assert.ThrowsAsync<Phase3.InjectedFailure>(() => scope.ServiceProvider.GetRequiredService<IUserManager>()
                    .UpdateAsync(id, new() { Email = "changed-" + email, FirstName = "Changed" }));
            Assert.True(probe.SawChangedEmail);
            using var verify = fixture.ScopeFactory.CreateScope();
            var db = verify.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
            Assert.Equal(email, await db.Users.Where(u => u.Id == id).Select(u => u.Email).SingleAsync());
            Assert.Equal("Audit", teacherProfile ? await db.Teachers.Where(t => t.Id == profile).Select(t => t.FirstName).SingleAsync() :
                await db.Students.Where(s => s.Id == profile).Select(s => s.FirstName).SingleAsync());
        }
        finally
        {
            using var cleanup = fixture.ScopeFactory.CreateScope();
            var db = cleanup.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
            await db.Users.Where(u => u.Id == id).ExecuteDeleteAsync();
            if (teacherProfile) await db.Teachers.Where(t => t.Id == profile).ExecuteDeleteAsync();
            else await db.Students.Where(s => s.Id == profile).ExecuteDeleteAsync();
        }
    }

    private sealed class AuditUserUpdateProbe(Guid id) : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
    {
        public bool SawChangedEmail;
        public override async ValueTask<int> SavedChangesAsync(Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesCompletedEventData data,
            int result, System.Threading.CancellationToken cancellationToken = default)
        {
            SawChangedEmail = await ((ElectronicRegisterContext)data.Context!).Users.AsNoTracking()
                .AnyAsync(u => u.Id == id && u.Email.StartsWith("changed-"));
            throw new Phase3.InjectedFailure();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Audit_GradeReadsCannotEscapeOwnership(bool otherStudent)
    {
        var other = await fixture.ResetScenarioAsync(IntegrationScenario.Phase4Unassigned);
        var exam = await CreateAsync(Draft());
        var submission = await CompleteAsync(exam, 7m);
        Guid grade;
        using (var scope = fixture.ScopeFactory.CreateScope())
            grade = (await scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>().ExamSubmissions
                .Where(s => s.Id == submission).Select(s => s.GradeId).SingleAsync())!.Value;
        var caller = otherStudent ? Phase3.Student(other) : Other;
        Assert.Equal(HttpStatusCode.NotFound, (await _host.As(caller).GetAsync($"/api/Grade/{grade}")).StatusCode);
        var all = await _host.As(caller).GetFromJsonAsync<List<GradeDto>>("/api/Grade");
        Assert.DoesNotContain(all!, g => g.Id == grade);
        var page = await _host.As(caller).GetFromJsonAsync<GradePageDto>($"/api/Grade/paged?studentId={_ids.StudentId}");
        Assert.DoesNotContain(page!.Items, g => g.Id == grade);
        var filters = await _host.As(caller).GetFromJsonAsync<GradeFiltersDto>("/api/Grade/filters");
        Assert.DoesNotContain(filters!.Students, s => s.Id == _ids.StudentId);
        Assert.Equal(HttpStatusCode.Forbidden, (await _host.As(Phase3.Student(other)).GetAsync($"/api/Grade/bystudentid/{_ids.StudentId}")).StatusCode);
    }

    [Fact]
    public async Task Audit_StudentTeacherSubjectsCannotBypassClassVisibility()
    {
        using (var setup = fixture.ScopeFactory.CreateScope())
            await setup.ServiceProvider.GetRequiredService<ElectronicRegisterContext>().Students.Where(s => s.Id == _ids.StudentId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ClassId, (Guid?)null));
        try
        {
            Assert.Equal(HttpStatusCode.NotFound, (await _host.As(Phase3.Student(_ids)).GetAsync("/api/Subject")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await _host.As(Phase3.Student(_ids)).GetAsync($"/api/Subject/byteacher/{SeedData.TeacherId}")).StatusCode);
        }
        finally
        {
            using var cleanup = fixture.ScopeFactory.CreateScope();
            await cleanup.ServiceProvider.GetRequiredService<ElectronicRegisterContext>().Students.Where(s => s.Id == _ids.StudentId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ClassId, (Guid?)SeedData.ClassId));
        }
    }

    [Fact]
    public async Task Audit_OrdinaryGradeRemainsEditableAndDeletable()
    {
        var response = await _host.As(Teacher).PostAsJsonAsync("/api/Grade", new CreateGradeDto
        { StudentId = _ids.StudentId, SubjectId = SeedData.SubjectId, Value = 7m, Date = DateOnly.FromDateTime(DateTime.UtcNow) });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = await response.Content.ReadFromJsonAsync<Guid>();
        try
        {
            Assert.Equal(HttpStatusCode.NoContent, (await _host.As(Teacher).PutAsJsonAsync($"/api/Grade/update/{id}", new UpdateGradeDto
            { SubjectId = SeedData.SubjectId, Value = 8m, Date = DateOnly.FromDateTime(DateTime.UtcNow) })).StatusCode);
            using (var verify = fixture.ScopeFactory.CreateScope())
                Assert.Equal(8m, await verify.ServiceProvider.GetRequiredService<ElectronicRegisterContext>().Grades.Where(g => g.Id == id).Select(g => g.Value).SingleAsync());
            Assert.Equal(HttpStatusCode.NoContent, (await _host.As(Admin).DeleteAsync($"/api/Grade/{id}")).StatusCode);
            using var final = fixture.ScopeFactory.CreateScope();
            Assert.False(await final.ServiceProvider.GetRequiredService<ElectronicRegisterContext>().Grades.AnyAsync(g => g.Id == id));
        }
        finally
        {
            using var cleanup = fixture.ScopeFactory.CreateScope();
            await cleanup.ServiceProvider.GetRequiredService<ElectronicRegisterContext>().Grades.Where(g => g.Id == id).ExecuteDeleteAsync();
        }
    }

    [Theory]
    [InlineData("update-teacher")]
    [InlineData("update-admin")]
    [InlineData("delete-admin")]
    public async Task Audit_ExamGradeCannotBypassFinalization(string operation)
    {
        var exam = await CreateAsync(Draft());
        var submission = await CompleteAsync(exam, 7m);
        Guid grade;
        using (var scope = fixture.ScopeFactory.CreateScope())
            grade = (await scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>().ExamSubmissions
                .Where(s => s.Id == submission).Select(s => s.GradeId).SingleAsync())!.Value;
        var before = await SubmissionJsonAsync(submission);
        var client = _host.As(operation == "update-teacher" ? Teacher : Admin);
        var response = operation.StartsWith("delete") ? await client.DeleteAsync($"/api/Grade/{grade}") :
            await client.PutAsJsonAsync($"/api/Grade/update/{grade}", new UpdateGradeDto
            { SubjectId = SeedData.SubjectId, Value = 2m, Date = DateOnly.FromDateTime(DateTime.UtcNow) });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(before, await SubmissionJsonAsync(submission));
        using var verify = fixture.ScopeFactory.CreateScope();
        Assert.Equal(7m, await verify.ServiceProvider.GetRequiredService<ElectronicRegisterContext>().Grades
            .Where(g => g.Id == grade).Select(g => g.Value).SingleAsync());
    }

    [Theory]
    [InlineData("count")]
    [InlineData("list")]
    [InlineData("id")]
    [InlineData("name")]
    public async Task Audit_TeacherWithoutAssignmentsCannotReadStudents(string operation)
    {
        var name = "";
        using (var scope = fixture.ScopeFactory.CreateScope())
            name = await scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>().Students
                .Where(s => s.Id == _ids.StudentId).Select(s => s.LastName).SingleAsync();
        var route = operation switch { "count" => "count", "list" => "", "id" => _ids.StudentId.ToString(), _ => "bylastname/" + name };
        var response = await _host.As(Other).GetAsync("/api/Student/" + route);
        Assert.Equal(operation == "count" ? HttpStatusCode.OK : HttpStatusCode.NotFound, response.StatusCode);
        if (operation == "count") Assert.Equal(0, await response.Content.ReadFromJsonAsync<int>());
        // Assigned owner and administrator retain access.
        Assert.Equal(HttpStatusCode.OK, (await _host.As(Teacher).GetAsync($"/api/Student/{_ids.StudentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _host.As(Admin).GetAsync($"/api/Student/{_ids.StudentId}")).StatusCode);
    }

    [Fact]
    public async Task Audit_LoginWithoutPasswordHashIsUnauthorized()
    {
        var response = await _host.As(null).PostAsJsonAsync("/api/Auth/login", new LoginDto
        { Email = "integration.teacher@example.test", Password = "WrongPassword!" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain("Salt", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Users")]
    [InlineData("Biennium")]
    public async Task Audit_CreateReturnsPersistedGuidAndUsableLocation(string resource)
    {
        var marker = Guid.NewGuid().ToString("N");
        object body = resource switch
        {
            "Student" => new CreateStudentDto { FirstName = "Audit", LastName = marker },
            "Users" => new CreateUserDto { Email = "audit-" + marker + "@example.test", Password = "AuditPassword!", Role = "admin" },
            _ => new CreateBienniumDto { StartYear = new DateOnly(2201, 9, 1), EndYear = new DateOnly(2203, 8, 31) }
        };
        try
        {
            var response = await _host.As(Admin).PostAsJsonAsync("/api/" + resource, body);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var id = await response.Content.ReadFromJsonAsync<Guid>();
            Assert.NotEqual(Guid.Empty, id);
            Assert.EndsWith(id.ToString(), response.Headers.Location!.ToString());
            Assert.Equal(HttpStatusCode.OK, (await _host.As(Admin).GetAsync(response.Headers.Location)).StatusCode);
            using var verify = fixture.ScopeFactory.CreateScope();
            var db = verify.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
            Assert.True(resource switch
            {
                "Student" => await db.Students.AnyAsync(s => s.Id == id),
                "Users" => await db.Users.AnyAsync(u => u.Id == id),
                _ => await db.Biennia.AnyAsync(b => b.Id == id)
            });
        }
        finally
        {
            using var cleanup = fixture.ScopeFactory.CreateScope();
            var db = cleanup.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
            if (resource == "Student") await db.Students.Where(s => s.LastName == marker).ExecuteDeleteAsync();
            if (resource == "Users") await db.Users.Where(u => u.Email == "audit-" + marker + "@example.test").ExecuteDeleteAsync();
            if (resource == "Biennium") await db.Biennia.Where(b => b.StartYear == new DateOnly(2201, 9, 1)).ExecuteDeleteAsync();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Audit_RegistrationFaultRollsBackProfileAndUser(bool adminFlow)
    {
        var marker = "audit_" + Guid.NewGuid().ToString("N");
        var email = "allievo_" + marker + "@itsumbria.it";
        var profileId = Guid.Empty;
        var userId = Guid.Empty;
        // Probe after real SQL: baseline has no transaction. Do not use SaveProbe, which requires one.
        var probe = new AuditRegistrationProbe(async ctx =>
        {
            profileId = await ctx.Students.Where(s => s.LastName == marker).Select(s => s.Id).SingleAsync();
            userId = await ctx.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
        });
        try
        {
            await using (var provider = Phase3.Provider(fixture, null, probe))
            using (var scope = provider.CreateScope())
            {
                var auth = scope.ServiceProvider.GetRequiredService<IAuthManager>();
                await Assert.ThrowsAsync<Phase3.InjectedFailure>(() => adminFlow ? auth.RegisterForAdminAsync(new()
                { Email = email, Password = "AuditPassword!", FirstName = "Audit", LastName = marker, Role = "student" }) : auth.RegisterAsync(new()
                { Email = email, Password = "AuditPassword!", FirstName = "Audit", LastName = marker }));
            }
            Assert.NotEqual(Guid.Empty, profileId);
            Assert.NotEqual(Guid.Empty, userId);
            using var verify = fixture.ScopeFactory.CreateScope();
            var db = verify.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
            Assert.False(await db.Users.AnyAsync(u => u.Id == userId));
            Assert.False(await db.Students.AnyAsync(s => s.Id == profileId));
        }
        finally
        {
            using var cleanup = fixture.ScopeFactory.CreateScope();
            var db = cleanup.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
            await db.Users.Where(u => u.Email == email).ExecuteDeleteAsync();
            await db.Students.Where(s => s.LastName == marker).ExecuteDeleteAsync();
        }
    }

    private sealed class AuditRegistrationProbe(Func<ElectronicRegisterContext, Task> inspect)
        : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
    {
        private int _saves;
        public override async ValueTask<int> SavedChangesAsync(Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesCompletedEventData data,
            int result, System.Threading.CancellationToken cancellationToken = default)
        {
            if (++_saves == 2) { await inspect((ElectronicRegisterContext)data.Context!); throw new Phase3.InjectedFailure(); }
            return result;
        }
    }
}

