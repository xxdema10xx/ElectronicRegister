using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.DTOs;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

// D-02: InstitutionMemberDto.Revision is the persisted membership security generation that
// POST /admin/members/{id}/mutations compares with ExpectedRevision.
public sealed partial class InstitutionAccountProvisioningTests
{
    private const string Members = "/api/institution/admin/members";

    private static async Task<InstitutionMemberDto> MemberDetail(Mt3cHosting.ProgramHost host, string admin, Guid membership)
    {
        using var response = await Send(host, Members + "/" + membership, admin);
        Assert.True(response.StatusCode == HttpStatusCode.OK, response.StatusCode + " " + await response.Content.ReadAsStringAsync());
        Assert.Contains("\"revision\":", await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<InstitutionMemberDto>())!;
    }

    // Records compare Roles by reference: compare the observable member state by value.
    private static string MemberShape(InstitutionMemberDto m) => string.Join("|", m.MembershipId, m.UserId, m.Email, m.Status, string.Join(",", m.Roles), m.SuspendedAt, m.Revision);

    private static Task<HttpResponseMessage> Mutation(Mt3cHosting.ProgramHost host, string admin, Guid membership, Guid eventId, long revision, string action) =>
        Send(host, Members + "/" + membership + "/mutations", admin, new { EventId = eventId, ExpectedRevision = revision, Action = action, Reason = "Revision test" }, "POST");

    [Fact]
    public async Task MemberRevisionIsTheExpectedRevisionAcceptedByMutations()
    {
        await using var host = await StartPlatform();
        var created = await Created(_a.Admin, Body(new[] { "Teacher" }));
        var member = await MemberDetail(host, _a.Admin, created.MembershipId);
        Assert.Equal(await Count("SELECT security_generation FROM institution_membership_security_state WHERE membership_id=@m", ("@m", created.MembershipId)), member.Revision);
        using (var list = await Send(host, Members + "?search=" + Uri.EscapeDataString(created.Email), _a.Admin))
        {
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            Assert.Equal(MemberShape(member), MemberShape(Assert.Single((await list.Content.ReadFromJsonAsync<List<InstitutionMemberDto>>())!)));
        }
        foreach (var wrong in new[] { member.Revision - 1, member.Revision + 1 }.Where(r => r >= 0))
        {
            using var stale = await Mutation(host, _a.Admin, created.MembershipId, Guid.NewGuid(), wrong, "Suspend");
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        }
        Assert.Equal(MemberShape(member), MemberShape(await MemberDetail(host, _a.Admin, created.MembershipId)));

        var eventId = Guid.NewGuid();
        using var first = await Mutation(host, _a.Admin, created.MembershipId, eventId, member.Revision, "Suspend");
        Assert.True(first.StatusCode == HttpStatusCode.OK, first.StatusCode + " " + await first.Content.ReadAsStringAsync());
        var suspended = await MemberDetail(host, _a.Admin, created.MembershipId);
        Assert.Equal("suspended", suspended.Status); Assert.Equal(member.Revision + 1, suspended.Revision);
        using var replay = await Mutation(host, _a.Admin, created.MembershipId, eventId, member.Revision, "Suspend");
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        Assert.Equal(MemberShape(suspended), MemberShape(await MemberDetail(host, _a.Admin, created.MembershipId)));

        using (var previous = await Mutation(host, _a.Admin, created.MembershipId, Guid.NewGuid(), member.Revision, "Reactivate"))
            Assert.Equal(HttpStatusCode.Conflict, previous.StatusCode);
        using (var current = await Mutation(host, _a.Admin, created.MembershipId, Guid.NewGuid(), suspended.Revision, "Reactivate"))
            Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        var reactivated = await MemberDetail(host, _a.Admin, created.MembershipId);
        Assert.Equal("active", reactivated.Status); Assert.Equal(suspended.Revision + 1, reactivated.Revision);
    }

    [Fact]
    public async Task MemberRevisionIsNotVisibleToStaffOrOtherInstitutions()
    {
        await using var host = await StartPlatform();
        var created = await Created(_a.Admin, Body(new[] { "Student" }));
        var staff = await Member(_a.Admin, _a.Id, new[] { "Staff" });
        var revision = (await MemberDetail(host, _a.Admin, created.MembershipId)).Revision;
        var before = await Digest();
        foreach (var route in new[] { Members, Members + "/" + created.MembershipId })
        {
            using var read = await Send(host, route, staff);
            Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        }
        using (var mutation = await Mutation(host, staff, created.MembershipId, Guid.NewGuid(), revision, "Suspend"))
            Assert.Equal(HttpStatusCode.Forbidden, mutation.StatusCode);
        using (var foreign = await Send(host, Members + "/" + created.MembershipId, _b.Admin))
            Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        using (var foreignList = await Send(host, Members + "?search=" + Uri.EscapeDataString(created.Email), _b.Admin))
        {
            Assert.Equal(HttpStatusCode.OK, foreignList.StatusCode);
            Assert.Empty((await foreignList.Content.ReadFromJsonAsync<List<InstitutionMemberDto>>())!);
        }
        Assert.Equal(before, await Digest());
        Assert.Equal(revision, (await MemberDetail(host, _a.Admin, created.MembershipId)).Revision);
    }
}
