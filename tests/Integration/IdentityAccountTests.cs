using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public sealed partial class IdentityAccountTests(IntegrationFixture fixture) : IAsyncLifetime
{
    private readonly Guid _user = Guid.NewGuid(), _other = Guid.NewGuid();
    private const string Password = "OriginalPassword!";
    private Mt3cHosting.ProgramHost _host = null!;
    private string Email => _user.ToString("N") + "@identity.example.test";

    public async Task InitializeAsync()
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);
        using var scope = fixture.ScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
        db.Users.AddRange(new User { Id = _user, Email = Email, PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password), Role = "admin" },
            new User { Id = _other, Email = _other.ToString("N") + "@identity.example.test", PasswordHash = "", Role = "student" });
        await db.SaveChangesAsync();
        _host = await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString, Guid.NewGuid(), Array.Empty<Guid>(), enabled: false);
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private async Task<HttpResponseMessage> Send(string route, string? token = null, object? body = null, string method = "GET")
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), route);
        if (token is not null) request.Headers.Authorization = new("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await _host.Client.SendAsync(request);
    }

    private async Task<string> Login(string password = Password)
    {
        using var response = await Send("/api/Auth/identity/login", body: new LoginDto { Email = Email, Password = password }, method: "POST");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<IdentityTicketDto>())!.Ticket;
    }

    [Fact]
    public async Task HistoricalAccountHasNoInferredMembershipAndPersonalUpdateCannotMutateAuthority()
    {
        var token = await Login();
        var account = await (await Send("/api/identity/account", token)).Content.ReadFromJsonAsync<IdentityAccountDto>();
        Assert.Equal(_user, account!.UserId);
        Assert.Equal(Email, account.Email);
        var memberships = await (await Send("/api/Auth/institutions", token)).Content.ReadFromJsonAsync<InstitutionChoiceDto[]>();
        Assert.Empty(memberships!); // Global admin is not a membership or platform authority.
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("/api/identity/account", token, new { displayName = "Person", userId = _other, role = "InstitutionAdmin" }, "PUT")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Send("/api/identity/account", token, new UpdateIdentityAccountDto(" Person "), "PUT")).StatusCode);
        var json = await (await Send("/api/identity/account", token)).Content.ReadAsStringAsync();
        Assert.DoesNotContain("passwordHash", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("securityGeneration", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Person", JsonSerializer.Deserialize<IdentityAccountDto>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.DisplayName);
        using var scope = fixture.ScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
        Assert.Equal("admin", (await db.Users.AsNoTracking().SingleAsync(u => u.Id == _user)).Role);
        Assert.Empty(await db.InstitutionMemberships.Where(m => m.UserId == _user).ToListAsync());
    }

    [Theory]
    [InlineData("wrong", "ReplacementPassword!", HttpStatusCode.Forbidden)]
    [InlineData(Password, "short!", HttpStatusCode.BadRequest)]
    [InlineData(Password, "NoSpecialCharacter", HttpStatusCode.BadRequest)]
    [InlineData(Password, Password, HttpStatusCode.BadRequest)]
    public async Task InvalidPasswordChangeDoesNotRevokeOrModifyCredential(string current, string replacement, HttpStatusCode expected)
    {
        var token = await Login();
        Assert.Equal(expected, (await Send("/api/identity/account/password", token, new ChangeIdentityPasswordDto(current, replacement), "PUT")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send("/api/identity/account", token)).StatusCode);
        await Login();
    }

    [Fact]
    public async Task PasswordChangeRevokesAllPriorTicketsAndCannotBeReplayed()
    {
        var first = await Login();
        var second = await Login();
        using var recipientScope=_host.Host.Services.CreateScope();
        var recipient=recipientScope.ServiceProvider.GetRequiredService<ElectronicRegisterAPI.Domain.Interfaces.Security.IRecipientIdentity>();
        Assert.Equal(_user,await recipient.VerifySelectionTicketAsync(first));
        var change = new ChangeIdentityPasswordDto(Password, "ReplacementPassword!");
        Assert.Equal(HttpStatusCode.NoContent, (await Send("/api/identity/account/password", first, change, "PUT")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send("/api/identity/account", second)).StatusCode);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>recipient.VerifySelectionTicketAsync(second));
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send("/api/identity/account/password", first, change, "PUT")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send("/api/Auth/identity/login", body: new LoginDto { Email = Email, Password = Password }, method: "POST")).StatusCode);
        var fresh = await Login("ReplacementPassword!");
        Assert.Equal(HttpStatusCode.OK, (await Send("/api/identity/account", fresh)).StatusCode);
        using var scope = fixture.ScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
        var events = await db.Database.SqlQueryRaw<string>("SELECT action AS Value FROM identity_account_event WHERE user_id={0}", _user).ToListAsync();
        Assert.Equal("password.changed", Assert.Single(events));
        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM identity_account_event WHERE user_id={_user}"));
    }

    [Fact]
    public async Task ConcurrentCredentialUpdatesCommitExactlyOnce()
    {
        using var initialScope = fixture.ScopeFactory.CreateScope();
        var original = await initialScope.ServiceProvider.GetRequiredService<IIdentityAccountRepository>().GetAsync(_user);
        async Task<bool> Change(string password)
        {
            using var scope = fixture.ScopeFactory.CreateScope();
            try
            {
                await scope.ServiceProvider.GetRequiredService<IIdentityAccountRepository>().ChangePasswordAsync(_user, 0, original!.PasswordHash, BCrypt.Net.BCrypt.HashPassword(password));
                return true;
            }
            catch (UnauthorizedAccessException) { return false; }
        }
        var outcomes = await Task.WhenAll(Change("ConcurrentFirst!"), Change("ConcurrentSecond!"));
        Assert.Single(outcomes.Where(x => x));
        using var verifyScope = fixture.ScopeFactory.CreateScope();
        Assert.Equal(1, await verifyScope.ServiceProvider.GetRequiredService<IIdentityAccountRepository>().GenerationAsync(_user));
    }

    [Fact]
    public async Task AnonymousAndExternalOnlyCredentialsCannotEstablishLocalPassword()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send("/api/identity/account")).StatusCode);
        using var scope = fixture.ScopeFactory.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<ElectronicRegisterAPI.Domain.Interfaces.Managers.IIdentityAccountManager>();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => manager.ChangePasswordAsync(_other, 0, new("anything", "ReplacementPassword!")));
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<IIdentityAccountRepository>().GenerationAsync(_other));
    }

    [Fact]
    public async Task MalformedPersistedCredentialFailsClosedWithoutLeakingHashErrors()
    {
        using(var scope=fixture.ScopeFactory.CreateScope())await scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>().Users.Where(u=>u.Id==_user).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.PasswordHash,"invalid-persisted-hash"));
        var response=await Send("/api/Auth/identity/login",body:new LoginDto{Email=Email,Password=Password},method:"POST");
        Assert.Equal(HttpStatusCode.Unauthorized,response.StatusCode);
        var body=await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Salt",body,StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("invalid-persisted-hash",body,StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RegistrationCreatesOnlyIdentityAndCollisionCannotAcquireHistoricalAccount()
    {
        var email = Guid.NewGuid().ToString("N") + "@new-account.example.test";
        var input = new RegisterIdentityAccountDto(email, "RegisteredPassword!");
        var response = await Send("/api/identity/accounts", body: input, method: "POST");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = await response.Content.ReadFromJsonAsync<Guid>();
        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(HttpStatusCode.Conflict, (await Send("/api/identity/accounts", body: input, method: "POST")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Send("/api/identity/accounts", body: input with { Email = Email }, method: "POST")).StatusCode);
        await Login();
        using var scope = fixture.ScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == id);
        Assert.Null(user.StudentId);
        Assert.Null(user.TeacherId);
        Assert.Empty(await db.InstitutionMemberships.Where(m => m.UserId == id).ToListAsync());
    }

    [Fact]
    public async Task PasswordChangeRevokesAccessAcrossEveryInstitution()
    {
        var ticket = await Login();
        var accesses = new System.Collections.Generic.List<string>();
        for(var i=0;i<2;i++)
        {
            var institution=Guid.NewGuid();
            var membership=Guid.NewGuid();
            using(var scope=fixture.ScopeFactory.CreateScope())
            {
                var db=scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
                db.Institutions.Add(new(){Id=institution,Name="Identity revocation test",Code="ID_"+institution.ToString("N"),Status="active",CreatedAt=DateTime.UtcNow,UpdatedAt=DateTime.UtcNow});
                db.InstitutionMemberships.Add(new(){Id=membership,InstitutionId=institution,UserId=_user,Status="active",JoinedAt=DateTime.UtcNow,InstitutionMembershipRoles=new[]{new InstitutionMembershipRole{MembershipId=membership,Role="InstitutionAdmin"}}});
                await db.SaveChangesAsync();
            }
            var response=await Send("/api/Auth/select-institution",ticket,new SelectInstitutionDto{InstitutionId=institution,MembershipId=membership},"POST");
            Assert.Equal(HttpStatusCode.OK,response.StatusCode);
            var access=(await response.Content.ReadFromJsonAsync<InstitutionAccessDto>())!.Token;
            Assert.Equal(HttpStatusCode.OK,(await Send("/api/Auth/context",access)).StatusCode);
            accesses.Add(access);
        }
        Assert.Equal(HttpStatusCode.NoContent,(await Send("/api/identity/account/password",ticket,new ChangeIdentityPasswordDto(Password,"RevokedGlobally!"),"PUT")).StatusCode);
        foreach(var access in accesses) Assert.Equal(HttpStatusCode.Unauthorized,(await Send("/api/Auth/context",access)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await Send("/api/Auth/institutions",ticket)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await Send("/api/identity/account",await Login("RevokedGlobally!"))).StatusCode);
    }
}
