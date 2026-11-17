using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

// 409 of POST /admin/members/{id}/mutations: unchanged Italian title, ProblemDetails "code" telling
// the client which conflict occurred (LastAdministrator is covered by the rule tests: over HTTP the
// acting admin is always another active admin, since self-administration is forbidden).
public sealed partial class InstitutionAccountProvisioningTests
{
    private static async Task<string> ConflictCode(HttpResponseMessage response)
    {
        Assert.True(response.StatusCode == HttpStatusCode.Conflict, response.StatusCode + " " + await response.Content.ReadAsStringAsync());
        var body = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("Conflitto sulla membership istituzionale", body["title"]!.GetValue<string>());
        Assert.Equal(409, body["status"]!.GetValue<int>());
        return body["code"]!.GetValue<string>();
    }

    [Fact]
    public async Task MutationConflictsCarryADistinctCode()
    {
        await using var host = await StartPlatform();
        var created = await Created(_a.Admin, Body(new[] { "Teacher" }));
        var revision = (await MemberDetail(host, _a.Admin, created.MembershipId)).Revision;

        using (var stale = await Mutation(host, _a.Admin, created.MembershipId, Guid.NewGuid(), revision + 1, "Suspend"))
            Assert.Equal("StaleRevision", await ConflictCode(stale));
        // Already active: reactivating at the current revision changes nothing.
        using (var noOp = await Mutation(host, _a.Admin, created.MembershipId, Guid.NewGuid(), revision, "Reactivate"))
            Assert.Equal("NoOp", await ConflictCode(noOp));
        // Same role already present: also a no-op.
        using (var role = await Send(host, Members + "/" + created.MembershipId + "/mutations", _a.Admin,
                   new { EventId = Guid.NewGuid(), ExpectedRevision = revision, Action = "AddRole", Role = "Teacher", Reason = "Revision test" }, "POST"))
            Assert.Equal("NoOp", await ConflictCode(role));

        var eventId = Guid.NewGuid();
        using (var first = await Mutation(host, _a.Admin, created.MembershipId, eventId, revision, "Suspend"))
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        // Same EventId, different intent (another action at the now current revision).
        using (var reused = await Mutation(host, _a.Admin, created.MembershipId, eventId, revision + 1, "Reactivate"))
            Assert.Equal("EventIdConflict", await ConflictCode(reused));
        // None of the conflicts advanced the revision.
        Assert.Equal(revision + 1, (await MemberDetail(host, _a.Admin, created.MembershipId)).Revision);
    }
}
