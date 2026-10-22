using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;
public sealed partial class InstitutionClassCrudTests
{
    private static string Academic(string kind) => "/api/institution/" + kind;
    private static object AcademicInput(string kind, string name = "Nuovo catalogo") => kind switch
    {
        "study-areas" => new
        {
            Name = name,
            Description = "Descrizione"
        },
        "study-paths" => new
        {
            Name = name,
            Description = (string? )null
        },
        _ => new
        {
            StartYear = "2030-01-01",
            EndYear = "2032-01-01"
        }
    };
    private async Task<Guid> AcademicCreate(string kind, object body)
    {
        var response = await Request(Academic(kind), await Access(), body, "POST");
        await Expect(response, HttpStatusCode.Created);
        var id = await response.Content.ReadFromJsonAsync<Guid>();
        Assert.EndsWith(Academic(kind) + "/" + id, response.Headers.Location!.OriginalString);
        await Expect(await Request(response.Headers.Location.OriginalString, await Access()), HttpStatusCode.OK);
        return id;
    }

    [Theory]
    [InlineData("study-areas")]
    [InlineData("study-paths")]
    [InlineData("biennia")]
    public async Task AcademicCrudPersistsRevisionsAndImmutableHistory(string kind)
    {
        var token = await Access();
        var id = await AcademicCreate(kind, AcademicInput(kind));
        var detail = await Request(Academic(kind) + "/" + id, token);
        Assert.Equal("\"1\"", detail.Headers.ETag!.Tag);
        var list = await Request(Academic(kind) + "?pageNumber=1&pageSize=1", token);
        await Expect(list, HttpStatusCode.OK);
        Assert.Single((await list.Content.ReadFromJsonAsync<JsonObject>())!["items"]!.AsArray());
        object update = kind == "biennia" ? new
        {
            StartYear = "2031-01-01",
            EndYear = "2033-01-01"
        }

        : AcademicInput(kind, "Modificato");
        await Expect(await Request(Academic(kind) + "/" + id, token, update, "PUT"), (HttpStatusCode)428);
        await Expect(await Request(Academic(kind) + "/" + id, token, update, "PUT", etag: "\"1\""), HttpStatusCode.NoContent);
        await Expect(await Request(Academic(kind) + "/" + id, token, update, "PUT", etag: "\"1\""), HttpStatusCode.Conflict);
        await Expect(await Request(Academic(kind) + "/" + id, token, method: "DELETE"), HttpStatusCode.NoContent);
        await Expect(await Request(Academic(kind) + "/" + id, token), HttpStatusCode.NotFound);
        await Db(async db =>
        {
            var events = await db.Set<InstitutionAdministrationEvent>().Where(e => e.InstitutionId == _a && e.EntityId == id).OrderBy(e => e.Revision).ToListAsync();
            Assert.Equal(new[] { "created", "updated", "deleted" }, events.Select(e => e.Action));
            Assert.All(events, e =>
            {
                Assert.Equal(_adminUser, e.ActorUserId);
                Assert.Equal(Id("admin-membership"), e.ActorMembershipId);
            });
            Assert.Null(events[0].BeforeJson);
            Assert.Null(events[2].AfterJson);
        });
    }

    [Fact]
    public async Task AcademicHttpConfigurationBuildsClassAndBlocksAllStructuralDeletes()
    {
        var token = await Access();
        var area = await AcademicCreate("study-areas", AcademicInput("study-areas"));
        var path = await AcademicCreate("study-paths", AcademicInput("study-paths"));
        var biennium = await AcademicCreate("biennia", AcademicInput("biennia"));
        var bsa = await AcademicCreate("biennium-study-areas", new { BienniumId = biennium, StudyAreaId = area });
        var bsp = await AcademicCreate("biennium-study-paths", new { BienniumStudyAreaId = bsa, StudyPathId = path });
        var response = await Request(Classes, token, new { Name = "API chain", BienniumStudyPathId = bsp }, "POST");
        await Expect(response, HttpStatusCode.Created);
        var classId = await response.Content.ReadFromJsonAsync<Guid>();
        foreach (var(kind, id)in new[]
        {
            ("study-areas", area),
            ("study-paths", path),
            ("biennia", biennium),
            ("biennium-study-areas", bsa),
            ("biennium-study-paths", bsp)
        }

        )
            await Expect(await Request(Academic(kind) + "/" + id, token, method: "DELETE"), HttpStatusCode.Conflict);
        await Expect(await Request(Classes + "/" + classId, token, method: "DELETE"), HttpStatusCode.NoContent);
        foreach (var(kind, id)in new[]
        {
            ("biennium-study-paths", bsp),
            ("biennium-study-areas", bsa),
            ("study-areas", area),
            ("study-paths", path),
            ("biennia", biennium)
        }

        )
            await Expect(await Request(Academic(kind) + "/" + id, token, method: "DELETE"), HttpStatusCode.NoContent);
    }

    [Theory]
    [InlineData("study-areas")]
    [InlineData("study-paths")]
    public async Task AcademicMySqlCollationAndConcurrentDuplicatesAreAuthoritative(string kind)
    {
        var token = await Access();
        var results = await Task.WhenAll(Request(Academic(kind), token, AcademicInput(kind, "Caffè"), "POST"), Request(Academic(kind), token, AcademicInput(kind, "CAFFE"), "POST"));
        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        var id = await results.Single(r => r.StatusCode == HttpStatusCode.Created).Content.ReadFromJsonAsync<Guid>();
        var edits = await Task.WhenAll(Request(Academic(kind) + "/" + id, token, AcademicInput(kind, "Uno"), "PUT", etag: "\"1\""), Request(Academic(kind) + "/" + id, token, AcademicInput(kind, "Due"), "PUT", etag: "\"1\""));
        Assert.Equal(1, edits.Count(r => r.StatusCode == HttpStatusCode.NoContent));
        Assert.Equal(1, edits.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }

    [Theory]
    [InlineData("study-areas")]
    [InlineData("study-paths")]
    [InlineData("biennia")]
    public async Task AcademicForeignTenantIsIndistinguishableAndUnknownOwnerRejected(string kind)
    {
        var id = await AcademicCreate(kind, AcademicInput(kind));
        var foreign = await Access(b: true);
        await Expect(await Request(Academic(kind) + "/" + id, foreign), HttpStatusCode.NotFound);
        await Expect(await Request(Academic(kind) + "/" + id, foreign, AcademicInput(kind), "PUT", etag: "\"1\""), HttpStatusCode.NotFound);
        await Expect(await Request(Academic(kind) + "/" + id, foreign, method: "DELETE"), HttpStatusCode.NotFound);
        var body = System.Text.Json.JsonSerializer.SerializeToNode(AcademicInput(kind))!.AsObject();
        body["InstitutionId"] = _b;
        await Expect(await Request(Academic(kind), await Access(), body, "POST"), HttpStatusCode.BadRequest);
        await Expect(await Request(Academic(kind) + "?InstitutionId=" + _a, foreign), HttpStatusCode.BadRequest);
        var page = (await (await Request(Academic(kind), foreign)).Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.DoesNotContain(page["items"]!.AsArray(), item => RowId(item) == id);
    }

    [Theory]
    [InlineData("study-areas", "teacher")]
    [InlineData("study-areas", "student")]
    [InlineData("study-paths", "teacher")]
    [InlineData("biennia", "student")]
    [InlineData("biennium-study-areas", "teacher")]
    [InlineData("biennium-study-paths", "student")]
    public async Task AcademicAdministrationRequiresCurrentAdmin(string kind, string role)
    {
        var token = await Access(role);
        await Expect(await Request(Academic(kind), token), HttpStatusCode.Forbidden);
        await Expect(await Request(Academic(kind), token, AcademicInput(kind), "POST"), HttpStatusCode.Forbidden);
        await Expect(await Request(Academic(kind) + "/" + Guid.NewGuid(), token, method: "DELETE"), HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("study-areas", "")]
    [InlineData("study-paths", "  ")]
    [InlineData("study-areas", "long")]
    [InlineData("study-paths", "long")]
    public async Task AcademicInvalidNamesAreRejected(string kind, string name)
    {
        await Expect(await Request(Academic(kind), await Access(), AcademicInput(kind, name == "long" ? new string ('x', 101) : name), "POST"), HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("0001-01-01", "2032-01-01")]
    [InlineData("2032-01-01", "2031-01-01")]
    public async Task AcademicInvalidDatesAreRejected(string start, string end)
    {
        await Expect(await Request(Academic("biennia"), await Access(), new { StartYear = start, EndYear = end }, "POST"), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AcademicNullRequiredDescriptionAndForeignAssociationAreRejected()
    {
        var token = await Access();
        await Expect(await Request(Academic("study-areas"), token, new { Name = "Test", Description = (string? )null }, "POST"), HttpStatusCode.BadRequest);
        await Expect(await Request(Academic("biennium-study-areas"), token, new { BienniumId = Id("biennium"), StudyAreaId = Id("area", true) }, "POST"), HttpStatusCode.NotFound);
        await Expect(await Request(Academic("biennium-study-paths"), token, new { BienniumStudyAreaId = Id("bsa"), StudyPathId = Id("path", true) }, "POST"), HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("biennium-study-areas")]
    [InlineData("biennium-study-paths")]
    public async Task AcademicAssociationDuplicatesAndUnknownStructuralFieldsAreRejected(string kind)
    {
        object body = kind == "biennium-study-areas" ? new
        {
            BienniumId = Id("biennium"),
            StudyAreaId = Id("area")
        }

        : new
        {
            BienniumStudyAreaId = Id("bsa"),
            StudyPathId = Id("path")
        };
        await Expect(await Request(Academic(kind), await Access(), body, "POST"), HttpStatusCode.Conflict);
        var malicious = System.Text.Json.JsonSerializer.SerializeToNode(body)!.AsObject();
        malicious["InstitutionId"] = _b;
        await Expect(await Request(Academic(kind), await Access(), malicious, "POST"), HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("study-areas")]
    [InlineData("study-paths")]
    [InlineData("biennia")]
    [InlineData("biennium-study-areas")]
    [InlineData("biennium-study-paths")]
    public async Task AcademicPaginationAndAuthenticationFailClosed(string kind)
    {
        await Expect(await Request(Academic(kind)), HttpStatusCode.Unauthorized);
        await Expect(await Request(Academic(kind) + "?pageSize=101", await Access()), HttpStatusCode.BadRequest);
        await Expect(await Request(Academic(kind) + "?pageNumber=0", await Access()), HttpStatusCode.BadRequest);
        await Expect(await Request(Academic(kind) + "/" + Guid.NewGuid(), await Access()), HttpStatusCode.NotFound);
    }
}
