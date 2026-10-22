using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Api.Controllers;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
using ElectronicRegisterAPI.Infrastructure.Security;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public sealed class Mt2IdentityTests(IntegrationFixture fixture) : IAsyncLifetime
{
    private const string Key = "mt2-local-test-signing-key-only-0123456789abcdef";
    private const string Password = "Mt2TestPassword!";
    private static readonly string Hash = BCrypt.Net.BCrypt.HashPassword(Password);
    private readonly Guid _user = Guid.NewGuid(), _other = Guid.NewGuid(), _legacy = Guid.NewGuid();
    private readonly Guid _a = Guid.NewGuid(), _b = Guid.NewGuid(), _ma = Guid.NewGuid(), _mb = Guid.NewGuid(), _otherMembership = Guid.NewGuid();
    private readonly Guid _legacyMembership = Guid.NewGuid();
    private ProgramHost _host = null!;
    private string Email(Guid id) => id.ToString("N") + "@mt2.example.test";

    public async Task InitializeAsync()
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);
        await Change(async db =>
        {
            foreach (var id in new[] { _user, _other, _legacy })
                db.Users.Add(new User { Id = id, Email = Email(id), PasswordHash = Hash, Role = "admin" });
            foreach (var id in new[] { _a, _b })
                db.Institutions.Add(new Institution { Id = id, Code = "MT2_" + id.ToString("N"), Name = "Test institution " + id,
                    Status = "active", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            db.InstitutionMemberships.AddRange(
                Membership(_ma, _a, _user, "InstitutionAdmin", "Student"),
                Membership(_mb, _b, _user, "Teacher"),
                Membership(_otherMembership, _a, _other, "Student"));
            await db.SaveChangesAsync();
        });
        _host = await ProgramHost.StartAsync(fixture.ConnectionString, _a, new[] { _legacy, _user });
    }

    private static InstitutionMembership Membership(Guid id, Guid institution, Guid user, params string[] roles) => new()
    {
        Id = id, InstitutionId = institution, UserId = user, Status = "active", JoinedAt = DateTime.UtcNow,
        InstitutionMembershipRoles = roles.Select(role => new InstitutionMembershipRole { MembershipId = id, Role = role }).ToList()
    };

    private async Task Change(Func<ElectronicRegisterContext, Task> action)
    {
        using var scope = fixture.ScopeFactory.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>());
    }

    private async Task<HttpResponseMessage> Request(string route, string? token = null, object? body = null, string? method = null)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method ?? (body is null ? "GET" : "POST")), route);
        if (token is not null) request.Headers.Authorization = new("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await _host.Client.SendAsync(request);
    }

    private async Task<string> Ticket(Guid? user = null)
    {
        var response = await Request("/api/Auth/identity/login", body: new LoginDto { Email = Email(user ?? _user), Password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<IdentityTicketDto>();
        Assert.Equal(300, result!.ExpiresInSeconds);
        return result.Ticket;
    }

    private async Task<string> Access(bool foundationB = false, string? ticket = null)
    {
        var response = await Request("/api/Auth/select-institution", ticket ?? await Ticket(),
            new SelectInstitutionDto { InstitutionId = foundationB ? _b : _a, MembershipId = foundationB ? _mb : _ma });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<InstitutionAccessDto>();
        Assert.Equal(900, result!.ExpiresInSeconds);
        return result.Token;
    }

    [Fact]
    public async Task IdentitySelectionSeparatesFoundationsAndRolesWithoutLegacyProfiles()
    {
        var ticket = await Ticket();
        var choices = await (await Request("/api/Auth/institutions", ticket)).Content.ReadFromJsonAsync<List<InstitutionChoiceDto>>();
        Assert.Equal(2, choices!.Count);
        Assert.Equal(new[] { "InstitutionAdmin", "Student" }, choices.Single(c => c.InstitutionId == _a).Roles);
        Assert.Equal(new[] { "Teacher" }, choices.Single(c => c.InstitutionId == _b).Roles);
        foreach (var b in new[] { false, true })
        {
            var token = await Access(b, ticket);
            var result = await Request("/api/Auth/context", token);
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            var context = await result.Content.ReadFromJsonAsync<TenantContextDto>();
            Assert.Equal(_user, context!.UserId);
            Assert.Equal(b ? _b : _a, context.InstitutionId);
            Assert.Equal(b ? _mb : _ma, context.MembershipId);
            Assert.Equal(b ? new[] { "Teacher" } : new[] { "InstitutionAdmin", "Student" }, context.Roles);
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            Assert.Equal(InstitutionTokenContract.AccessType, jwt.Header.Typ);
            Assert.Single(jwt.Claims.Where(c => c.Type == "institution_id"));
            Assert.DoesNotContain(jwt.Claims, c => c.Type.Contains("student_id") || c.Type.Contains("teacher_id") || c.Type == "role");
        }
        var single = await (await Request("/api/Auth/institutions", await Ticket(_other))).Content.ReadFromJsonAsync<List<InstitutionChoiceDto>>();
        Assert.Equal(_a, Assert.Single(single!).InstitutionId);
        var none = await (await Request("/api/Auth/institutions", await Ticket(_legacy))).Content.ReadFromJsonAsync<List<InstitutionChoiceDto>>();
        Assert.Empty(none!); // no fabricated membership or fallback to first institution
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("suspended")]
    [InlineData("left")]
    [InlineData("institution-suspended")]
    [InlineData("institution-archived")]
    [InlineData("institution-provisioning")]
    [InlineData("roles-removed")]
    [InlineData("admin-removed")]
    [InlineData("teacher-removed")]
    [InlineData("roles-added")]
    [InlineData("membership-deleted")]
    [InlineData("user-deleted")]
    public async Task ExistingTokenIsForbiddenAfterCommittedAuthorizationChange(string change)
    {
        var isB = change == "teacher-removed";
        var membership = isB ? _mb : _ma;
        var ticket = await Ticket();
        var token = await Access(isB, ticket);
        Assert.Equal(HttpStatusCode.OK, (await Request("/api/Auth/context", token)).StatusCode);
        await Change(async db =>
        {
            if (change.StartsWith("institution-"))
                await db.Institutions.Where(i => i.Id == _a).ExecuteUpdateAsync(s => s.SetProperty(i => i.Status, change.Substring(12)));
            else if (change == "roles-added")
            { db.InstitutionMembershipRoles.Add(new() { MembershipId = _ma, Role = "Teacher" }); await db.SaveChangesAsync(); }
            else if (change is "roles-removed" or "admin-removed" or "teacher-removed")
                await db.InstitutionMembershipRoles.Where(r => r.MembershipId == membership &&
                    (change == "roles-removed" || r.Role == (isB ? "Teacher" : "InstitutionAdmin"))).ExecuteDeleteAsync();
            else if (change is "membership-deleted" or "user-deleted")
            {
                var ids = change == "user-deleted" ? new[] { _ma, _mb } : new[] { _ma };
                await db.InstitutionMembershipRoles.Where(r => ids.Contains(r.MembershipId)).ExecuteDeleteAsync();
                await using var purge = await MembershipFixturePurge.AllowAsync(db, new[]{_a,_b});
                await db.InstitutionMemberships.Where(m => ids.Contains(m.Id)).ExecuteDeleteAsync();
                if (change == "user-deleted") await db.Users.Where(u => u.Id == _user).ExecuteDeleteAsync();
            }
            else await db.InstitutionMemberships.Where(m => m.Id == membership).ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, change));
        });
        var denied = await Request("/api/Auth/context", token);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(403, (await denied.Content.ReadFromJsonAsync<JsonObject>())!["status"]!.GetValue<int>());
        if (change is "pending" or "suspended" or "left")
        {
            await Change(async db => Assert.Equal(change, (await db.InstitutionMemberships.AsNoTracking().SingleAsync(m => m.Id == _ma)).Status));
            var choices = await (await Request("/api/Auth/institutions", ticket)).Content.ReadFromJsonAsync<List<InstitutionChoiceDto>>();
            Assert.DoesNotContain(choices!, c => c.MembershipId == _ma);
        }
        if (change is "admin-removed" or "roles-added")
        {
            var refreshed = await Access(false, ticket);
            Assert.Equal(HttpStatusCode.OK, (await Request("/api/Auth/context", refreshed)).StatusCode);
        }
        else
            Assert.Equal(HttpStatusCode.Forbidden, (await Request("/api/Auth/select-institution", ticket,
                new SelectInstitutionDto { InstitutionId = isB ? _b : _a, MembershipId = membership })).StatusCode);
    }

    [Theory]
    [InlineData("another-user")]
    [InlineData("wrong-institution")]
    [InlineData("missing-membership")]
    [InlineData("missing-institution")]
    [InlineData("unowned-role")]
    [InlineData("numeric-role")]
    public async Task SelectionNeverTrustsClientIdentifiersOrRole(string fault)
    {
        var dto = new SelectInstitutionDto { InstitutionId = _b, MembershipId = _mb };
        var ticket = await Ticket();
        switch (fault)
        {
            case "another-user": dto.MembershipId = _otherMembership; dto.InstitutionId = _a; break;
            case "wrong-institution": dto.InstitutionId = _a; break;
            case "missing-membership": dto.MembershipId = Guid.NewGuid(); break;
            case "missing-institution": dto.InstitutionId = Guid.NewGuid(); break;
            case "unowned-role": dto.RequestedRole = "InstitutionAdmin"; break;
            case "numeric-role": dto.RequestedRole = "1"; break;
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await Request("/api/Auth/select-institution", ticket, dto)).StatusCode);
        // Other identity really owns only A and cannot select B, despite global User.Role=admin.
        Assert.Equal(HttpStatusCode.Forbidden, (await Request("/api/Auth/select-institution", await Ticket(_other),
            new SelectInstitutionDto { InstitutionId = _b, MembershipId = _mb })).StatusCode);
    }

    [Theory]
    [InlineData("signature", 401)]
    [InlineData("issuer", 401)]
    [InlineData("audience", 401)]
    [InlineData("duplicate-audience", 401)]
    [InlineData("expired", 401)]
    [InlineData("type", 401)]
    [InlineData("purpose", 401)]
    [InlineData("missing-sub", 401)]
    [InlineData("missing-institution", 401)]
    [InlineData("missing-membership", 401)]
    [InlineData("missing-role", 401)]
    [InlineData("missing-jti", 401)]
    [InlineData("empty-institution", 401)]
    [InlineData("duplicate-sub", 401)]
    [InlineData("duplicate-property", 401)]
    [InlineData("duplicate-role", 401)]
    [InlineData("unknown-role", 401)]
    [InlineData("legacy-role", 401)]
    [InlineData("institution", 403)]
    [InlineData("membership", 403)]
    [InlineData("other-user", 403)]
    [InlineData("unowned-role", 403)]
    public async Task InvalidOrInconsistentJwtIsRejected(string fault, int status)
    {
        var token = await Access();
        var pieces = token.Split('.');
        var payload = JsonNode.Parse(Base64UrlEncoder.Decode(pieces[1]))!.AsObject();
        var header = JsonNode.Parse(Base64UrlEncoder.Decode(pieces[0]))!.AsObject();
        switch (fault)
        {
            case "issuer": payload["iss"] = "foreign"; break;
            case "audience": payload["aud"] = "foreign"; break;
            case "duplicate-audience": payload["aud"] = new JsonArray("mt2-client:institution-access", "mt2-client"); break;
            case "expired": payload.Remove("nbf"); payload["exp"] = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds(); break;
            case "type": header["typ"] = "JWT"; break;
            case "purpose": payload["purpose"] = InstitutionTokenContract.SelectionType; break;
            case "missing-sub": payload.Remove("sub"); break;
            case "missing-institution": payload.Remove("institution_id"); break;
            case "missing-membership": payload.Remove("membership_id"); break;
            case "missing-role": payload.Remove("institution_role"); break;
            case "missing-jti": payload.Remove("jti"); break;
            case "empty-institution": payload["institution_id"] = Guid.Empty.ToString(); break;
            case "duplicate-sub": payload["sub"] = new JsonArray(_user.ToString(), _other.ToString()); break;
            case "duplicate-role": payload["institution_role"] = new JsonArray("Student", "Student"); break;
            case "unknown-role": payload["institution_role"] = "SuperAdmin"; break;
            case "legacy-role": payload["role"] = "admin"; break;
            case "institution": payload["institution_id"] = _b.ToString(); break;
            case "membership": payload["membership_id"] = _mb.ToString(); break;
            case "other-user": payload["sub"] = _other.ToString(); break;
            case "unowned-role": payload["institution_role"] = "Teacher"; break;
        }
        var raw = payload.ToJsonString();
        if (fault == "duplicate-property") raw = raw[..^1] + ",\"sub\":\"" + _user + "\"}";
        var signed = Base64UrlEncoder.Encode(header.ToJsonString()) + "." + Base64UrlEncoder.Encode(raw);
        var signature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(fault == "signature" ? Key + "wrong" : Key), Encoding.ASCII.GetBytes(signed));
        var response = await Request("/api/Auth/context", signed + "." + Base64UrlEncoder.Encode(signature));
        Assert.Equal((HttpStatusCode)status, response.StatusCode);
    }

    [Fact]
    public async Task TokenPurposesCannotBeInterchangedAndNoMembershipIsPublic()
    {
        var ticket = await Ticket();
        var access = await Access(ticket: ticket);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Request("/api/Auth/context", ticket)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Request("/api/Auth/institutions", access)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Request("/api/Auth/select-institution", access,
            new SelectInstitutionDto { InstitutionId = _a, MembershipId = _ma })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Request("/api/Auth/institutions")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Request("/api/Auth/identity/login", body: new LoginDto { Email = Email(_user), Password = "wrong" })).StatusCode);
    }

    [Fact]
    public async Task EveryLegacyEndpointRejectsBothScopedAndSelectionTokensBeforeReadingOrWriting()
    {
        var ticket = await Ticket();
        var a = await Access(ticket: ticket);
        var b = await Access(true, ticket);
        var sources = _host.Host.Services.GetRequiredService<Microsoft.AspNetCore.Routing.EndpointDataSource>();
        var endpoints = sources.Endpoints.OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>() is not null &&
                e.Metadata.GetMetadata<ElectronicRegisterAPI.Api.Security.InstitutionIdentityEndpointAttribute>() is null &&
                e.Metadata.GetMetadata<ElectronicRegisterAPI.Api.Security.InstitutionSchoolEndpointAttribute>() is null &&
                e.Metadata.GetMetadata<ElectronicRegisterAPI.Api.Security.InstitutionExamEndpointAttribute>() is null &&
                e.Metadata.GetMetadata<ElectronicRegisterAPI.Api.Security.InstitutionCourseEndpointAttribute>() is null &&
                e.Metadata.GetMetadata<ElectronicRegisterAPI.Api.Security.InstitutionMembershipAdminEndpointAttribute>() is null).ToArray();
        MembershipHttpSurface.AssertRoutes(sources.Endpoints.OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>());
        Assert.Equal(MembershipHttpSurface.AdminCount, sources.Endpoints.Count(e => e.Metadata.GetMetadata<ElectronicRegisterAPI.Api.Security.InstitutionMembershipAdminEndpointAttribute>() is not null));
        Assert.Equal(19, sources.Endpoints.Count(e => e.Metadata.GetMetadata<ElectronicRegisterAPI.Api.Security.InstitutionCourseEndpointAttribute>() is not null));
        // Discover every current legacy action; new explicitly migrated endpoints have their own HTTP suite.
        Assert.Equal(73, endpoints.Length); // Preserved baseline plus dynamic traversal of every discovered action below.
        Assert.Contains(endpoints, e => e.RoutePattern.RawText!.StartsWith("api/Student", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(endpoints, e => e.RoutePattern.RawText!.StartsWith("api/Exam", StringComparison.OrdinalIgnoreCase));
        Assert.All(endpoints, e => Assert.DoesNotContain(e.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>(),
            p => p.Policy is InstitutionTokenContract.AccessScheme or ElectronicRegisterAPI.Api.Security.InstitutionSchoolEndpointAttribute.PolicyName or ElectronicRegisterAPI.Api.Security.InstitutionCourseEndpointAttribute.PolicyName));
        var before = await LegacyCounts();
        foreach (var endpoint in endpoints)
        {
            // Anonymous legacy credential endpoints are verified below with genuine credentials.
            if (endpoint.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>().Count == 0) continue;
            var path = System.Text.RegularExpressions.Regex.Replace(endpoint.RoutePattern.RawText!, @"\{([^}:?]+)[^}]*\}", match =>
                match.Groups[1].Value == "date" ? "2026-10-18" : match.Groups[1].Value is "lastName" or "name" or "subject" ? "MT2" : Guid.NewGuid().ToString());
            foreach (var method in endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()!.HttpMethods)
            foreach (var token in new[] { ticket, a, b })
            {
                var response = await Request("/" + path.TrimStart('/'), token, method is "POST" or "PUT" ? new { } : null, method);
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            }
        }
        Assert.Equal(before, await LegacyCounts());
        // Even explicitly allowlisting X cannot open legacy access when X has a membership in B.
        Assert.Equal(HttpStatusCode.Unauthorized, (await Request("/api/Auth/login", body: new LoginDto { Email = Email(_user), Password = Password })).StatusCode);
        var legacy = await Request("/api/Auth/login", body: new LoginDto { Email = Email(_legacy), Password = Password });
        Assert.Equal(HttpStatusCode.OK, legacy.StatusCode);
        var tokenLegacy = (await legacy.Content.ReadFromJsonAsync<JsonObject>())!["token"]!.GetValue<string>();
        Assert.Equal(HttpStatusCode.OK, (await Request("/api/Student", tokenLegacy)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Request("/api/Auth/context", tokenLegacy)).StatusCode);
        using var scope = _host.Host.Services.CreateScope();
        Assert.Throws<UnauthorizedAccessException>(() => scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Value);
    }

    private async Task<string> LegacyCounts()
    {
        string result = "";
        await Change(async db => result = string.Join(",", await db.Users.CountAsync(), await db.Students.CountAsync(),
            await db.Teachers.CountAsync(), await db.Grades.CountAsync(), await db.Exams.CountAsync(), await db.ExamSubmissions.CountAsync(), await db.AuditEvents.CountAsync()));
        return result;
    }

    [Fact]
    public async Task SimultaneousRequestsNeverShareTenantContext()
    {
        var ticket = await Ticket();
        var a = await Access(ticket: ticket);
        var b = await Access(true, ticket);
        await Task.WhenAll(Enumerable.Range(0, 12).Select(async i =>
        {
            var response = await Request("/api/Auth/context", i % 2 == 0 ? a : b);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var context = await response.Content.ReadFromJsonAsync<TenantContextDto>();
            Assert.Equal(i % 2 == 0 ? _a : _b, context!.InstitutionId);
        }));
    }

    [Theory]
    [InlineData("membership")]
    [InlineData("institution")]
    [InlineData("role")]
    [InlineData("global-role")]
    public async Task BackfilledLegacyBridgeAlsoChecksCurrentAuthorization(string change)
    {
        await Change(async db => { db.InstitutionMemberships.Add(Membership(_legacyMembership, _a, _legacy, "InstitutionAdmin")); await db.SaveChangesAsync(); });
        var login = await Request("/api/Auth/login", body: new LoginDto { Email = Email(_legacy), Password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<JsonObject>())!["token"]!.GetValue<string>();
        Assert.Equal(HttpStatusCode.OK, (await Request("/api/Auth/me", token)).StatusCode);
        await Change(async db =>
        {
            switch (change)
            {
                case "membership": await db.InstitutionMemberships.Where(m => m.Id == _legacyMembership).ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, "suspended")); break;
                case "institution": await db.Institutions.Where(i => i.Id == _a).ExecuteUpdateAsync(s => s.SetProperty(i => i.Status, "suspended")); break;
                case "role": await db.InstitutionMembershipRoles.Where(r => r.MembershipId == _legacyMembership).ExecuteDeleteAsync(); break;
                case "global-role": await db.Users.Where(u => u.Id == _legacy).ExecuteUpdateAsync(s => s.SetProperty(u => u.Role, "student")); break;
            }
        });
        Assert.Equal(HttpStatusCode.Forbidden, (await Request("/api/Auth/me", token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Request("/api/Auth/login", body: new LoginDto { Email = Email(_legacy), Password = Password })).StatusCode);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task LegacyNeedsExplicitEnablementAndUnbackfilledOptIn(bool enabled, bool unbackfilled)
    {
        await using var otherHost = await ProgramHost.StartAsync(fixture.ConnectionString, _a, new[] { _legacy }, enabled, unbackfilled);
        var login = await otherHost.Client.PostAsJsonAsync("/api/Auth/login", new LoginDto { Email = Email(_legacy), Password = Password });
        Assert.Equal(enabled ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden, login.StatusCode);
        var originalLogin = await Request("/api/Auth/login", body: new LoginDto { Email = Email(_legacy), Password = Password });
        var oldToken = (await originalLogin.Content.ReadFromJsonAsync<JsonObject>())!["token"]!.GetValue<string>();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/Student");
        request.Headers.Authorization = new("Bearer", oldToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await otherHost.Client.SendAsync(request)).StatusCode);
        var identity = await otherHost.Client.PostAsJsonAsync("/api/Auth/identity/login", new LoginDto { Email = Email(_user), Password = Password });
        Assert.Equal(HttpStatusCode.OK, identity.StatusCode);
    }

    public async Task DisposeAsync()
    {
        try { if (_host is not null) await _host.DisposeAsync(); }
        finally { await Change(async db =>
        {
            var ids = new[] { _ma, _mb, _otherMembership, _legacyMembership };
            await db.InstitutionMembershipRoles.Where(r => ids.Contains(r.MembershipId)).ExecuteDeleteAsync();
            await using var purge = await MembershipFixturePurge.AllowAsync(db, new[]{_a,_b});
                await db.InstitutionMemberships.Where(m => ids.Contains(m.Id)).ExecuteDeleteAsync();
            await db.Institutions.Where(i => i.Id == _a || i.Id == _b).ExecuteDeleteAsync();
            await db.Users.Where(u => u.Id == _user || u.Id == _other || u.Id == _legacy).ExecuteDeleteAsync();
        }); }
    }

    // Executes the real Program, all DI and worker registrations retained; ephemeral Kestrel port.
    private sealed class ProgramHost(IHost host, Task run, HttpClient client) : IAsyncDisposable
    {
        internal IHost Host => host;
        internal HttpClient Client => client;
        internal static async Task<ProgramHost> StartAsync(string connectionString, Guid legacyInstitution, Guid[] users, bool enabled = true, bool unbackfilled = true)
        {
            using var observer = new HostObserver();
            using var subscription = DiagnosticListener.AllListeners.Subscribe(observer);
            var args = new List<string>
            {
                "--Deployment:Mode=LocalTest", "--environment=Production", "--urls=http://127.0.0.1:0", "--applicationName=" + typeof(ExamController).Assembly.GetName().Name,
                "--ConnectionStrings:DefaultConnection=" + connectionString,
                "--Jwt:Key=" + Key, "--Jwt:Issuer=mt2-tests", "--Jwt:Audience=mt2-client", "--Jwt:ExpiresInMinutes=10",
                "--AzureAd:TenantId=11111111-2222-3333-4444-555555555555", "--AzureAd:Audience=mt2-microsoft",
                "--Identity:Legacy:Enabled=" + enabled, "--Identity:Legacy:InstitutionId=" + legacyInstitution,
                "--Identity:Legacy:AllowUnbackfilledAccounts=" + unbackfilled,
                "--Logging:LogLevel:Default=Warning"
            };
            args.AddRange(users.Select((id, i) => "--Identity:Legacy:UserIds:" + i + "=" + id));
            var run = Task.Run(async () =>
            {
                var result = typeof(ExamController).Assembly.EntryPoint!.Invoke(null, new object[] { args.ToArray() });
                if (result is Task task) await task;
            });
            var completed = await Task.WhenAny(observer.Built.Task, run).WaitAsync(TimeSpan.FromSeconds(30));
            if (completed == run) await run;
            var host = await observer.Built.Task.WaitAsync(TimeSpan.FromSeconds(30));
            var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
            var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = lifetime.ApplicationStarted.Register(() => ready.TrySetResult(true));
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Contains(host.Services.GetServices<IHostedService>(), s => s.GetType().Name == "ExpiredExamSubmissionWorker");
            var address = host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            return new(host, run, new HttpClient { BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(15) });
        }
        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
            await host.StopAsync().WaitAsync(TimeSpan.FromSeconds(30));
            await run.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.True(lifetime.ApplicationStopped.IsCancellationRequested);
        }
    }

    private sealed class HostObserver : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
    {
        internal TaskCompletionSource<IHost> Built { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<IDisposable> _subscriptions = new();
        public void OnNext(DiagnosticListener value) { if (value.Name == "Microsoft.Extensions.Hosting") _subscriptions.Add(value.Subscribe(this)); }
        public void OnNext(KeyValuePair<string, object?> value)
        {
            if(value.Key=="HostBuilding" && value.Value is IHostBuilder builder)builder.ConfigureServices((_,services)=>TestHostIsolation.Configure(services));
            if (value.Key == "HostBuilt" && value.Value is IHost host) Built.TrySetResult(host);
        }
        public void OnCompleted() { }
        public void OnError(Exception error) => Built.TrySetException(error);
        public void Dispose() { foreach (var subscription in _subscriptions) subscription.Dispose(); }
    }
}
