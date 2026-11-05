using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

public sealed partial class IdentityAccountTests
{
    private async Task MarkTemporary(Guid user)
    {
        using var scope = fixture.ScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO identity_account_event(id,user_id,action) VALUES({Guid.NewGuid()},{user},{IdentityAccountEvents.TemporaryPassword})");
    }

    private async Task<IdentityTicketDto> Ticket(string password = Password)
    {
        using var response = await Send("/api/Auth/identity/login", body: new LoginDto { Email = Email, Password = password }, method: "POST");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<IdentityTicketDto>())!;
    }

    [Fact]
    public async Task TemporaryPasswordAllowsOnlyPasswordChangeUntilReplaced()
    {
        var before = await Ticket();
        Assert.False(before.PasswordChangeRequired);
        await MarkTemporary(_user);
        // Even a ticket issued before the marker is confined: the state is read on every request.
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send("/api/identity/account", before.Ticket)).StatusCode);
        var ticket = await Ticket();
        Assert.True(ticket.PasswordChangeRequired);
        foreach (var (route, method, body) in new (string, string, object?)[]
        {
            ("/api/identity/account", "GET", null),
            ("/api/identity/account", "PUT", new UpdateIdentityAccountDto("Person")),
            ("/api/Auth/institutions", "GET", null),
            ("/api/Auth/select-institution", "POST", new { institutionId = Guid.NewGuid(), membershipId = Guid.NewGuid() }),
            ("/api/Auth/invitations/" + Guid.NewGuid() + "/accept", "POST", new { institutionId = Guid.NewGuid(), eventId = Guid.NewGuid(), challenge = new string('A', 64) }),
            ("/api/Auth/context", "GET", null),
        })
            Assert.Equal(HttpStatusCode.Unauthorized, (await Send(route, ticket.Ticket, body, method)).StatusCode);
        using (var scope = fixture.ScopeFactory.CreateScope())
        {
            // The rejected PUT did not reach the write: no display name and no profile event.
            var db = scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
            Assert.Empty(await db.Database.SqlQueryRaw<string>("SELECT display_name AS Value FROM identity_account_state WHERE user_id={0} AND display_name IS NOT NULL", _user).ToListAsync());
            Assert.Empty(await db.Database.SqlQueryRaw<string>("SELECT action AS Value FROM identity_account_event WHERE user_id={0} AND action='profile.updated'", _user).ToListAsync());
        }
        // A wrong current password keeps the obligation and the credential.
        Assert.Equal(HttpStatusCode.Forbidden, (await Send("/api/identity/account/password", ticket.Ticket, new ChangeIdentityPasswordDto("wrong", "ReplacementPassword!"), "PUT")).StatusCode);
        Assert.True((await Ticket()).PasswordChangeRequired);
        Assert.Equal(HttpStatusCode.NoContent, (await Send("/api/identity/account/password", ticket.Ticket, new ChangeIdentityPasswordDto(Password, "ReplacementPassword!"), "PUT")).StatusCode);
        // The change revokes the temporary ticket; the new credential is unrestricted.
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send("/api/identity/account/password", ticket.Ticket, new ChangeIdentityPasswordDto("ReplacementPassword!", "AnotherPassword!"), "PUT")).StatusCode);
        var fresh = await Ticket("ReplacementPassword!");
        Assert.False(fresh.PasswordChangeRequired);
        Assert.Equal(HttpStatusCode.OK, (await Send("/api/identity/account", fresh.Ticket)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send("/api/Auth/institutions", fresh.Ticket)).StatusCode);
    }

    [Fact]
    public async Task TemporaryCredentialAfterAChangeIsMandatoryAgain()
    {
        var first = await Ticket();
        Assert.Equal(HttpStatusCode.NoContent, (await Send("/api/identity/account/password", first.Ticket, new ChangeIdentityPasswordDto(Password, "ReplacementPassword!"), "PUT")).StatusCode);
        Assert.False((await Ticket("ReplacementPassword!")).PasswordChangeRequired);
        await MarkTemporary(_user);
        var again = await Ticket("ReplacementPassword!");
        Assert.True(again.PasswordChangeRequired);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send("/api/identity/account", again.Ticket)).StatusCode);
    }

    private static string Neutral(string body)
    {
        var json = JsonNode.Parse(body)!.AsObject();
        json.Remove("traceId");
        return json.ToJsonString();
    }

    [Fact]
    public async Task LoginIsUniformForExistingMissingAndExternalOnlyEmails()
    {
        var cases = new[]
        {
            new LoginDto { Email = Email, Password = "WrongPassword!" },
            new LoginDto { Email = Guid.NewGuid().ToString("N") + "@identity.example.test", Password = "WrongPassword!" },
            new LoginDto { Email = _other.ToString("N") + "@identity.example.test", Password = "WrongPassword!" },
        };
        var bodies = new string[cases.Length];
        var medians = new double[cases.Length];
        for (var i = 0; i < cases.Length; i++)
        {
            var samples = new double[9];
            for (var n = -2; n < samples.Length; n++)
            {
                var timer = Stopwatch.StartNew();
                using var response = await Send("/api/Auth/identity/login", body: cases[i], method: "POST");
                var elapsed = timer.Elapsed.TotalMilliseconds;
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
                bodies[i] = Neutral(await response.Content.ReadAsStringAsync());
                if (n >= 0) samples[n] = elapsed;
            }
            medians[i] = samples.Order().ElementAt(samples.Length / 2);
        }
        Assert.All(bodies, b => Assert.Equal(bodies[0], b));
        // Without a verification of the same cost an absent email answers in a few ms
        // against tens of ms for BCrypt: a factor of 2 separates the two clearly.
        Assert.True(medians.Min() * 2 >= medians.Max(), "Login timing differs: " + string.Join(", ", medians.Select(m => m.ToString("F1"))));
    }

    [Fact]
    public void AbsentCredentialUsesTheSameBcryptCostAsRealHashes()
    {
        using var scope = fixture.ScopeFactory.CreateScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var absent = (Lazy<string>)hasher.GetType().GetField("Absent", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        static string Cost(string hash) => hash.Split('$')[2];
        Assert.Equal(Cost(hasher.Hash("Comparison!1")), Cost(absent.Value));
        Assert.False(hasher.VerifyAbsent("anything"));
    }
}
