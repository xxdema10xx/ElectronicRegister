using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Api.Security;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
using ElectronicRegisterAPI.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MySqlConnector;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;
public sealed partial class InstitutionClassCrudTests
{
    private static readonly string[] AdministrationFamilies = ["study-areas", "study-paths", "biennia", "biennium-study-areas", "biennium-study-paths", "class-subjects", "campuses"];
    private static string ChangeAdminToken(string token, string kind)
    {
        var parts = token.Split('.');
        var header = JsonNode.Parse(Base64UrlEncoder.Decode(parts[0]))!.AsObject();
        var payload = JsonNode.Parse(Base64UrlEncoder.Decode(parts[1]))!.AsObject();
        if (kind == "legacy")
        {
            header["typ"] = "JWT";
            payload["aud"] = "mt2-client";
            payload["role"] = "admin";
            foreach (var key in new[]
            {
                "purpose",
                "institution_id",
                "membership_id",
                "institution_role",
                "security_generation"
            }

            )
                payload.Remove(key);
        }
        else if (kind == "platform")
            payload["institution_role"] = "PlatformOperator";
        else
            payload["security_generation"] = "999999";
        var signed = Base64UrlEncoder.Encode(header.ToJsonString()) + "." + Base64UrlEncoder.Encode(payload.ToJsonString());
        return signed + "." + Base64UrlEncoder.Encode(System.Security.Cryptography.HMACSHA256.HashData(Encoding.UTF8.GetBytes(Mt3cHosting.Key), Encoding.UTF8.GetBytes(signed)));
    }

    [Theory]
    [InlineData("none")]
    [InlineData("legacy")]
    [InlineData("platform")]
    [InlineData("generation")]
    public async Task AdministrationEveryNewRouteRejectsInvalidTokenPurposesAndGenerations(string kind)
    {
        var token = kind == "none" ? null : ChangeAdminToken(await Access(), kind);
        var before = await Digest(false);
        var other = await Digest(true);
        foreach (var family in AdministrationFamilies)
        {
            var route = Academic(family);
            await Expect(await Request(route, token), HttpStatusCode.Unauthorized);
            await Expect(await Request(route, token, new { }, "POST"), HttpStatusCode.Unauthorized);
            await Expect(await Request(route + "/" + Guid.NewGuid(), token), HttpStatusCode.Unauthorized);
            await Expect(await Request(route + "/" + Guid.NewGuid(), token, method: "DELETE"), HttpStatusCode.Unauthorized);
            if (!family.StartsWith("biennium-study"))
                await Expect(await Request(route + "/" + Guid.NewGuid(), token, new { }, "PUT", etag: "\"0\""), HttpStatusCode.Unauthorized);
        }

        Assert.Equal(before, await Digest(false));
        Assert.Equal(other, await Digest(true));
    }

    [Theory]
    [InlineData("suspended")]
    [InlineData("role")]
    public async Task AdministrationRevocationBlocksEveryNewMutationAndRead(string kind)
    {
        var token = await Access();
        if (kind == "suspended")
            await Execute("UPDATE institution_membership SET status='suspended' WHERE id=@id", ("@id", Id("admin-membership")));
        else
            await Execute("DELETE FROM institution_membership_role WHERE membership_id=@id", ("@id", Id("admin-membership")));
        foreach (var family in AdministrationFamilies)
        {
            await Expect(await Request(Academic(family), token), HttpStatusCode.Forbidden);
            await Expect(await Request(Academic(family), token, new { }, "POST"), HttpStatusCode.Forbidden);
            await Expect(await Request(Academic(family) + "/" + Guid.NewGuid(), token, method: "DELETE"), HttpStatusCode.Forbidden);
        }
    }

    [Theory]
    [InlineData("school")]
    [InlineData("exam")]
    [InlineData("course")]
    public async Task AdministrationRolloutFlagsRemainFailClosed(string flag)
    {
        var token = await Access();
        await using var host = await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString, _a, Array.Empty<Guid>(), enabled: false, school: flag != "school", exam: flag != "exam", course: flag != "course");
        var families = flag == "school" ? AdministrationFamilies : flag == "exam" ? new[]
        {
            "class-subjects"
        }

        : new[]
        {
            "campuses"
        };
        foreach (var family in families)
        {
            await Expect(await Request(Academic(family), token, host: host), HttpStatusCode.Forbidden);
            await Expect(await Request(Academic(family), token, new { }, "POST", host: host), HttpStatusCode.Forbidden);
            await Expect(await Request(Academic(family) + "/" + Guid.NewGuid(), token, method: "DELETE", host: host), HttpStatusCode.Forbidden);
        }
    }

    [Fact]
    public async Task AdministrationLedgerIsAppendOnlyEvenForRootAndRetainsSnapshots()
    {
        var id = await CreateCampus();
        await Db(async db =>
        {
            var row = await db.Set<InstitutionAdministrationEvent>().AsNoTracking().SingleAsync(e => e.EntityId == id && e.InstitutionId == _a);
            foreach (var sql in new[]
            {
                "UPDATE institution_administration_event SET action='deleted' WHERE institution_id={0} AND id={1}",
                "DELETE FROM institution_administration_event WHERE institution_id={0} AND id={1}"
            }

            )
            {
                var error = await Assert.ThrowsAsync<MySqlException>(() => db.Database.ExecuteSqlRawAsync(sql, _a, row.Id));
                Assert.Equal(1644, error.Number);
            }

            Assert.Equal(row.AfterJson, (await db.Set<InstitutionAdministrationEvent>().AsNoTracking().SingleAsync(e => e.Id == row.Id && e.InstitutionId == _a)).AfterJson);
        });
    }

    [Fact]
    public void AdministrationRouteInventoryKeepsExactPolicyMarkersAndAdminCommands()
    {
        var routes = _host.Host.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().Where(e => AdministrationFamilies.Any(f => e.RoutePattern.RawText!.StartsWith("api/institution/" + f))).ToArray();
        Assert.Equal(33, routes.Length);
        foreach (var endpoint in routes)
        {
            var verb = Assert.Single(endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods);
            var auth = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
            Assert.Null(endpoint.Metadata.GetMetadata<IAllowAnonymous>());
            var school = endpoint.Metadata.GetMetadata<InstitutionSchoolEndpointAttribute>();
            var exam = endpoint.Metadata.GetMetadata<InstitutionExamEndpointAttribute>();
            var course = endpoint.Metadata.GetMetadata<InstitutionCourseEndpointAttribute>();
            Assert.Equal(1, new object? [] { school, exam, course }.Count(x => x is not null));
            var policy = school is not null ? InstitutionSchoolEndpointAttribute.PolicyName : exam is not null ? InstitutionExamEndpointAttribute.PolicyName : InstitutionCourseEndpointAttribute.PolicyName;
            Assert.Equal(policy, Assert.Single(auth.Where(a => a.Policy is not null)).Policy);
            var role = Assert.Single(auth.Where(a => a.Roles is not null)).Roles;
            Assert.Equal(verb == "GET" && endpoint.RoutePattern.RawText == "api/institution/class-subjects" ? "InstitutionAdmin,Teacher" : "InstitutionAdmin", role);
        }
    }
}
