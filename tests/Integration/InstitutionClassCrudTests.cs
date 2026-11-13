using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;
namespace ElectronicRegisterAPI.Tests.Integration;

public sealed partial class InstitutionClassCrudTests
{
    private const string Classes = "/api/institution/classes";
    private static async Task Expect(HttpResponseMessage response, HttpStatusCode status) =>
        Assert.True(response.StatusCode == status, $"Expected {status}, got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    private async Task<Guid> CreateClass(string name = "Nuova", bool b = false)
    {
        var response = await Request(Classes, await Access(b: b), new { Name = name, BienniumStudyPathId = Id("bsp", b) }, "POST");
        await Expect(response, HttpStatusCode.Created);
        var id = await response.Content.ReadFromJsonAsync<Guid>();
        Assert.NotEqual(Guid.Empty, id);
        Assert.EndsWith(Classes + "/" + id, response.Headers.Location!.OriginalString);
        await Expect(await Request(response.Headers.Location.OriginalString, await Access(b: b)), HttpStatusCode.OK);
        return id;
    }
    private async Task<InstitutionClassDto> Detail(Guid id, bool b = false) =>
        (await (await Request(Classes + "/" + id, await Access(b: b))).Content.ReadFromJsonAsync<InstitutionClassDto>())!;

    [Fact]
    public async Task CreateListDetailRenameAndDeleteEmptyClassKeepPermanentAudit()
    {
        var token = await Access(); var id = await CreateClass("  Prima  ");
        var response = await Request(Classes + "/" + id, token);
        var first = (await response.Content.ReadFromJsonAsync<InstitutionClassDto>())!;
        Assert.Equal("Prima", first.Name); Assert.Null(first.CourseEditionId); Assert.Equal(1, first.Revision);
        Assert.Equal("\"1\"", response.Headers.ETag!.Tag);
        var page = (await (await Request(Classes, token)).Content.ReadFromJsonAsync<InstitutionClassPageDto>())!;
        Assert.Equal(1, page.PageNumber); Assert.Equal(20, page.PageSize); Assert.Equal(3, page.TotalCount);
        Assert.Contains(page.Items, c => c.Id == id && c.CourseEditionId is null);
        await Expect(await Request(Classes + "/" + id, token, new { Name = "Seconda" }, "PUT", etag: "\"1\""), HttpStatusCode.NoContent);
        Assert.Equal("Seconda", (await Detail(id)).Name);
        await Expect(await Request(Classes + "/" + id, token, method: "DELETE"), HttpStatusCode.NoContent);
        await Expect(await Request(Classes + "/" + id, token), HttpStatusCode.NotFound);
        await Db(async db =>
        {
            var events = await db.Set<InstitutionClassEvent>().AsNoTracking().Where(e => e.InstitutionId == _a && e.ClassId == id).OrderBy(e => e.Revision).ToListAsync();
            Assert.Equal(new[] { "class.created", "class.renamed", "class.deleted" }, events.Select(e => e.Action));
            Assert.Equal(new long[] { 1, 2, 3 }, events.Select(e => e.Revision));
            Assert.All(events, e => { Assert.Equal(_adminUser, e.ActorUserId); Assert.Equal(Id("admin-membership"), e.ActorMembershipId); });
            Assert.Null(events[0].BeforeJson); Assert.Null(events[2].AfterJson); Assert.Contains("Seconda", events[2].BeforeJson!);
            Assert.False(await db.Classes.AnyAsync(c => c.Id == id));
        });
    }
    [Theory]
    [InlineData("")] [InlineData("  \t  ")] [InlineData("123456789012345678901")]
    public async Task InvalidNamesDoNotCreateAnything(string name)
    {
        var before = await Digest(false);
        await Expect(await Request(Classes, await Access(), new { Name = name, BienniumStudyPathId = Id("bsp") }, "POST"), HttpStatusCode.BadRequest);
        Assert.Equal(before, await Digest(false));
        await Db(async db => Assert.Empty(await db.Set<InstitutionClassEvent>().Where(e => e.InstitutionId == _a).ToListAsync()));
    }
    [Theory]
    [InlineData("empty", 400)] [InlineData("missing", 404)] [InlineData("foreign", 404)]
    public async Task InvalidStudyPathsAreRejected(string kind, int code)
    {
        var path = kind == "empty" ? Guid.Empty : kind == "foreign" ? Id("bsp", true) : Guid.NewGuid();
        await Expect(await Request(Classes, await Access(), new { Name = "New", BienniumStudyPathId = path }, "POST"), (HttpStatusCode)code);
    }
    [Theory]
    [InlineData("POST", "InstitutionId")] [InlineData("POST", "Id")] [InlineData("POST", "CourseEditionId")]
    [InlineData("POST", "StudentId")] [InlineData("POST", "TeacherId")] [InlineData("POST", "ClassSubjectId")]
    [InlineData("POST", "CreatedAt")] [InlineData("POST", "SecurityGeneration")]
    [InlineData("PUT", "InstitutionId")] [InlineData("PUT", "Id")] [InlineData("PUT", "BienniumStudyPathId")]
    [InlineData("PUT", "CourseEditionId")] [InlineData("PUT", "Students")]
    public async Task UnknownOrStructuralBodyFieldsAreRejected(string method, string field)
    {
        var body = new JsonObject { ["name"] = "New" };
        if (method == "POST") body["bienniumStudyPathId"] = Id("bsp");
        body[field] = Guid.NewGuid();
        var id = Id("class"); var before = await Detail(id);
        await Expect(await Request(Classes + (method == "PUT" ? "/" + id : ""), await Access(), body, method, etag: "\"0\""), HttpStatusCode.BadRequest);
        Assert.Equal(before, await Detail(id));
    }
    [Theory]
    [InlineData("Classe", "classe")] [InlineData("Caf\u00e9", "CAFE")]
    public async Task DuplicateNamesRespectRealMySqlCollation(string first, string second)
    {
        await CreateClass(first);
        await Expect(await Request(Classes, await Access(), new { Name = second, BienniumStudyPathId = Id("bsp") }, "POST"), HttpStatusCode.Conflict);
    }
    [Fact]
    public async Task ConcurrentCreatesHaveOneWinnerAndOneAudit()
    {
        var token = await Access();
        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Request(Classes, token, new { Name = "Concurrent", BienniumStudyPathId = Id("bsp") }, "POST")));
        Assert.Single(responses.Where(r => r.StatusCode == HttpStatusCode.Created));
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        await Db(async db => Assert.Equal(1, await db.Set<InstitutionClassEvent>().CountAsync(e => e.InstitutionId == _a)));
    }
    [Fact]
    public async Task ConcurrentRenamesRejectStaleRevisionAndDoNotChangeAssociations()
    {
        var token = await Access(); var id = Id("class"); var before = await Detail(id); var digest = await Digest(false);
        var responses = await Task.WhenAll(new[] { "First", "Second" }.Select(name => Request(Classes + "/" + id, token, new { Name = name }, "PUT", etag: "\"0\"")));
        Assert.Single(responses.Where(r => r.StatusCode == HttpStatusCode.NoContent));
        Assert.Single(responses.Where(r => r.StatusCode == HttpStatusCode.Conflict));
        var after = await Detail(id); Assert.Equal(1, after.Revision); Assert.Contains(after.Name, new[] { "First", "Second" });
        Assert.Equal(before.BienniumStudyPathId, after.BienniumStudyPathId); Assert.Equal(before.CourseEditionId, after.CourseEditionId);
        Assert.Equal(digest, await Digest(false)); // Existing digest covers students/teachers/subjects/assignments/grades/profiles.
        await Expect(await Request(Classes + "/" + id, token, new { Name = "No header" }, "PUT"), (HttpStatusCode)428);
    }
    [Theory]
    [InlineData("student")] [InlineData("class-subject")]
    public async Task DeleteNeverCascadesOrDisconnectsSchoolData(string kind)
    {
        var id = await CreateClass();
        await Db(async db =>
        {
            if (kind == "student") (await db.Students.SingleAsync(s => s.Id == Id("student"))).ClassId = id;
            else
            {
                var cs = new ElectronicRegisterAPI.Infrastructure.Persistence.Entities.ClassSubject { Id = Guid.NewGuid(), ClassId = id, SubjectId = Id("subject"), TeacherId = Id("teacher") };
                db.Add(cs); db.Entry(cs).Property<Guid>("InstitutionId").CurrentValue = _a;
            }
            await db.SaveChangesAsync();
        });
        var digest = await Digest(false);
        await Expect(await Request(Classes + "/" + id, await Access(), method: "DELETE"), HttpStatusCode.Conflict);
        Assert.Equal(digest, await Digest(false)); Assert.Equal(id, (await Detail(id)).Id);
    }
    [Fact]
    public async Task EveryOperationIsTenantScopedAndForeignIdsLookMissing()
    {
        var a = await CreateClass(); var b = await CreateClass(b: true); var token = await Access(); var before = await Detail(b, true);
        foreach (var id in new[] { b, Guid.NewGuid() })
        {
            await Expect(await Request(Classes + "/" + id, token), HttpStatusCode.NotFound);
            await Expect(await Request(Classes + "/" + id, token, new { Name = "Attack" }, "PUT", etag: "\"0\""), HttpStatusCode.NotFound);
            await Expect(await Request(Classes + "/" + id, token, method: "DELETE"), HttpStatusCode.NotFound);
        }
        var page = (await (await Request(Classes, token)).Content.ReadFromJsonAsync<InstitutionClassPageDto>())!;
        Assert.Contains(page.Items, c => c.Id == a); Assert.DoesNotContain(page.Items, c => c.Id == b);
        Assert.Empty((await (await Request(Classes + "?bienniumStudyPathId=" + Id("bsp", true), token)).Content.ReadFromJsonAsync<InstitutionClassPageDto>())!.Items);
        Assert.Equal(before, await Detail(b, true));
    }
    [Theory]
    [InlineData("teacher")] [InlineData("student")]
    public async Task NonAdminRolesCannotUseAnyClassOperation(string role)
    {
        var token = await Access(role); var id = Id("class");
        foreach (var method in new[] { "GET", "POST", "PUT", "DELETE" })
        {
            var route = Classes + (method is "PUT" or "DELETE" ? "/" + id : "");
            object? body = method == "POST" ? new { Name = "No", BienniumStudyPathId = Id("bsp") } : method == "PUT" ? new { Name = "No" } : null;
            await Expect(await Request(route, token, body, method, etag: "\"0\""), HttpStatusCode.Forbidden);
        }
        await Expect(await Request(Classes + "/" + id, token), HttpStatusCode.Forbidden);
    }
    [Fact]
    public async Task RolloutDisabledAndRevokedGenerationDenyAccess()
    {
        var token = await Access();
        await using var disabled = await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString, _a, Array.Empty<Guid>(), enabled: false, school: false);
        await Expect(await Request(Classes, token, host: disabled), HttpStatusCode.Forbidden);
        await Execute("UPDATE institution_membership SET status='suspended' WHERE id=@m", ("@m", Id("admin-membership")));
        await Execute("UPDATE institution_membership SET status='active' WHERE id=@m", ("@m", Id("admin-membership")));
        await Expect(await Request(Classes, token), HttpStatusCode.Forbidden);
        await Expect(await Request(Classes, await Access()), HttpStatusCode.OK);
    }
    [Fact]
    public async Task PaginationFiltersOrderingAndPageSizeAreApplied()
    {
        await CreateClass("Z"); await CreateClass("A"); var token = await Access();
        var response = await Request(Classes + "?pageNumber=2&pageSize=1&bienniumStudyPathId=" + Id("bsp"), token);
        await Expect(response, HttpStatusCode.OK);
        var page = (await response.Content.ReadFromJsonAsync<InstitutionClassPageDto>())!;
        Assert.Equal(4, page.TotalCount); Assert.Single(page.Items); Assert.Equal("Class", page.Items[0].Name);
        foreach (var query in new[] { "pageNumber=0", "pageSize=101", "pageSize=0", "institutionId=" + _b })
            await Expect(await Request(Classes + "?" + query, token), HttpStatusCode.BadRequest);
    }
    [Fact]
    public async Task ClassAuditIsAppendOnlyEvenForDirectSqlAndSurvivesDeletion()
    {
        var id = await CreateClass();
        await Assert.ThrowsAsync<MySqlException>(() => Execute("UPDATE institution_class_event SET action='class.renamed' WHERE institution_id=@i AND class_id=@c", ("@i", _a), ("@c", id)));
        await Assert.ThrowsAsync<MySqlException>(() => Execute("DELETE FROM institution_class_event WHERE institution_id=@i AND class_id=@c", ("@i", _a), ("@c", id)));
        await Expect(await Request(Classes + "/" + id, await Access(), method: "DELETE"), HttpStatusCode.NoContent);
        await Db(async db => Assert.Equal(2, await db.Set<InstitutionClassEvent>().CountAsync(e => e.InstitutionId == _a && e.ClassId == id)));
    }
}
