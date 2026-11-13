using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.DTOs;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

public sealed partial class IdentityAccountTests
{
    [Fact]
    public async Task Quality_PasswordPutBudgetIsSharedByTwoHostsAndCannotBeSpoofedByProxyHeaders()
    {
        var ticket = await Login();
        await using var replica = await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString, Guid.NewGuid(), [], enabled: false);
        for (var n = 0; n < 6; n++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, "/api/identity/account/password");
            request.Headers.Authorization = new("Bearer", ticket);
            request.Headers.TryAddWithoutValidation("X-Forwarded-For", "203.0.113." + (n + 1));
            request.Content = JsonContent.Create(new ChangeIdentityPasswordDto("incorrect", "ReplacementPassword!"));
            using var response = await (n % 2 == 0 ? _host : replica).Client.SendAsync(request);
            Assert.Equal(n < 5 ? HttpStatusCode.Forbidden : HttpStatusCode.TooManyRequests, response.StatusCode);
            if (n == 5) Assert.NotNull(response.Headers.RetryAfter);
        }
        Assert.Equal(HttpStatusCode.OK, (await Send("/api/identity/account", ticket)).StatusCode);
    }
}
