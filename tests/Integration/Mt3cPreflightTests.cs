using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public sealed class Mt3cPreflightTests(IntegrationFixture fixture)
{
    [Fact]
    public async Task DefaultRolloutNeverOpensSchoolRoutesWithoutAuthorization()
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);
        await using var host = await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString, Guid.NewGuid(), Array.Empty<Guid>(), enabled: false);
        foreach (var family in new[] { "students", "teachers", "subjects" })
        {
            var response = await host.Client.GetAsync("/api/institution/" + family);
            Assert.Contains(response.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden });
        }
    }
}
