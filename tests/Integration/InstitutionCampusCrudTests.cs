using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;
public sealed partial class InstitutionClassCrudTests
{
    private const string Campuses = "/api/institution/campuses";
    private async Task<Guid> CreateCampus(string name = "Sede nuova")
    {
        var response = await Request(Campuses, await Access(), new { Name = name }, "POST");
        await Expect(response, HttpStatusCode.Created);
        var id = await response.Content.ReadFromJsonAsync<Guid>();
        Assert.EndsWith(Campuses + "/" + id, response.Headers.Location!.OriginalString);
        await Expect(await Request(response.Headers.Location.OriginalString, await Access()), HttpStatusCode.OK);
        return id;
    }

    private static object CampusUpdate(string name = "Sede aggiornata", bool active = true) => new
    {
        Name = name,
        Address = "Via Roma",
        City = "Roma",
        Province = "RM",
        IsActive = active
    };
    [Fact]
    public async Task CampusCrudAndActivationRetainPermanentAudit()
    {
        var id = await CreateCampus("  Nuova  ");
        var token = await Access();
        var response = await Request(Campuses + "/" + id, token);
        var row = (await response.Content.ReadFromJsonAsync<InstitutionCampusDto>())!;
        Assert.Equal("Nuova", row.Name);
        Assert.True(row.IsActive);
        Assert.Equal("\"1\"", response.Headers.ETag!.Tag);
        await Expect(await Request(Campuses + "/" + id, token, CampusUpdate(active: false), "PUT", etag: "\"1\""), HttpStatusCode.NoContent);
        var inactive = await Request(Campuses + "?isActive=false&pageSize=1", token);
        await Expect(inactive, HttpStatusCode.OK);
        Assert.Equal("1", inactive.Headers.GetValues("X-Total-Count").Single());
        Assert.Single((await inactive.Content.ReadFromJsonAsync<JsonArray>())!);
        await Expect(await Request(Campuses + "/" + id, token, CampusUpdate(), "PUT", etag: "\"2\""), HttpStatusCode.NoContent);
        await Expect(await Request(Campuses + "/" + id, token, method: "DELETE"), HttpStatusCode.NoContent);
        await Expect(await Request(Campuses + "/" + id, token), HttpStatusCode.NotFound);
        await Db(async db => Assert.Equal(new[] { "created", "updated", "updated", "deleted" }, await db.Set<InstitutionAdministrationEvent>().Where(e => e.EntityId == id && e.InstitutionId == _a).OrderBy(e => e.Revision).Select(e => e.Action).ToArrayAsync()));
    }

    [Theory]
    [InlineData("Name", 201)]
    [InlineData("Address", 301)]
    [InlineData("City", 101)]
    [InlineData("Province", 101)]
    public async Task CampusDatabaseLengthsAreValidated(string field, int length)
    {
        var body = new JsonObject
        {
            ["Name"] = "Campus",
            [field] = new string ('x', length)
        };
        await Expect(await Request(Campuses, await Access(), body, "POST"), HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CampusEmptyNamesAreRejected(string name)
    {
        await Expect(await Request(Campuses, await Access(), new { Name = name }, "POST"), HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("InstitutionId")]
    [InlineData("Id")]
    [InlineData("CourseEditionId")]
    public async Task CampusUnknownFieldsAreRejected(string field)
    {
        await Expect(await Request(Campuses, await Access(), new JsonObject { ["Name"] = "Campus", [field] = Guid.NewGuid() }, "POST"), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CampusOwnershipAndOptimisticConcurrencyAreEnforced()
    {
        var id = await CreateCampus();
        var foreign = await Access(b: true);
        var token = await Access();
        await Expect(await Request(Campuses + "/" + id, foreign), HttpStatusCode.NotFound);
        await Expect(await Request(Campuses + "/" + id, foreign, CampusUpdate(), "PUT", etag: "\"1\""), HttpStatusCode.NotFound);
        await Expect(await Request(Campuses + "/" + id, foreign, method: "DELETE"), HttpStatusCode.NotFound);
        Assert.Empty((await (await Request(Campuses, foreign)).Content.ReadFromJsonAsync<JsonArray>())!);
        await Expect(await Request(Campuses + "?InstitutionId=" + _a, foreign), HttpStatusCode.BadRequest);
        await Expect(await Request(Campuses + "/" + id, token, CampusUpdate(), "PUT"), (HttpStatusCode)428);
        var edits = await Task.WhenAll(Request(Campuses + "/" + id, token, CampusUpdate("Uno"), "PUT", etag: "\"1\""), Request(Campuses + "/" + id, token, CampusUpdate("Due"), "PUT", etag: "\"1\""));
        Assert.Equal(1, edits.Count(r => r.StatusCode == HttpStatusCode.NoContent));
        Assert.Equal(1, edits.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }

    [Theory]
    [InlineData("teacher")]
    [InlineData("student")]
    public async Task CampusNonAdminsCannotReadOrMutate(string role)
    {
        var token = await Access(role);
        await Expect(await Request(Campuses, token), HttpStatusCode.Forbidden);
        await Expect(await Request(Campuses, token, new { Name = "Denied" }, "POST"), HttpStatusCode.Forbidden);
        await Expect(await Request(Campuses + "/" + Guid.NewGuid(), token, CampusUpdate(), "PUT", etag: "\"1\""), HttpStatusCode.Forbidden);
        await Expect(await Request(Campuses + "/" + Guid.NewGuid(), token, method: "DELETE"), HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CampusCourseEditionLinksAndHistorySurviveDeactivation()
    {
        var id = await CreateCampus();
        var edition = await Edition();
        var token = await Access();
        await Expect(await Request($"/api/institution/course-editions/{edition}/campuses", token, new { CampusId = id }, "POST"), HttpStatusCode.Created);
        await Expect(await Request(Campuses + "/" + id, token, CampusUpdate(active: false), "PUT", etag: "\"1\""), HttpStatusCode.NoContent);
        var links = (await (await Request($"/api/institution/course-editions/{edition}/campuses", token)).Content.ReadFromJsonAsync<JsonArray>())!;
        Assert.Equal(id, Assert.Single(links)!["campusId"]!.GetValue<Guid>());
        Assert.False(links[0]!["isActive"]!.GetValue<bool>());
        await Expect(await Request(Campuses + "/" + id, token, method: "DELETE"), HttpStatusCode.Conflict);
        await Expect(await Request(Campuses + "/" + id, token, CampusUpdate(), "PUT", etag: "\"2\""), HttpStatusCode.NoContent);
        await Db(async db =>
        {
            Assert.True(await db.CourseEditions.AnyAsync(e => e.Id == edition && e.InstitutionId == _a));
            Assert.True(await db.CourseAdministrationEvents.AnyAsync(e => e.CampusId == id && e.InstitutionId == _a));
        });
    }

    [Fact]
    public async Task CampusPutRequiresExplicitActivationAndPagingIsValidated()
    {
        var id = await CreateCampus();
        var token = await Access();
        await Expect(await Request(Campuses + "/" + id, token, new { Name = "Missing active" }, "PUT", etag: "\"1\""), HttpStatusCode.BadRequest);
        await Expect(await Request(Campuses + "?pageSize=101", token), HttpStatusCode.BadRequest);
        await Expect(await Request(Campuses + "?pageNumber=0", token), HttpStatusCode.BadRequest);
    }
}
