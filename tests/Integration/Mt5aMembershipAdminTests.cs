using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Api.Security;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using ElectronicRegisterAPI.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MySqlConnector;
using Xunit;
using E = ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

namespace ElectronicRegisterAPI.Tests.Integration;

// Identity-only fixture: no schema preparation, migrations or dependency on course data.
// Serial collection also prevents concurrent snapshots against legacy suites.
[CollectionDefinition("Mt5aIdentityOnly", DisableParallelization = true)]
public sealed class Mt5aIdentityCollection;

[Collection("Mt5aIdentityOnly")]
public sealed class Mt5aMembershipAdminTests : IAsyncLifetime
{
    private const string Route = "/api/institution/admin/members", Password = "Mt5a-test-only!";
    private readonly Guid _a = Guid.NewGuid(), _b = Guid.NewGuid();
    private readonly Dictionary<string, Guid> _users = new();
    private readonly Dictionary<(string, bool), Guid> _members = new();
    private string _connection = null!;
    private Mt3cHosting.ProgramHost _host = null!;
    private Guid Member(string name, bool b = false) => _members[(name, b)];
    private static string Email(Guid id) => id.ToString("N") + "@mt5a.example.test";
    private static string Evidence => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../tests/Integration/TestResults/mt5a"));

    private async Task Db(Func<ElectronicRegisterContext, Task> action)
    {
        using var scope = _host.Host.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>());
    }

    public async Task InitializeAsync()
    {
        _connection = IntegrationFixture.ValidateLocalConnectionString(Environment.GetEnvironmentVariable("TEST_MYSQL_CONNECTION")
            ?? throw new InvalidOperationException("Explicit test database required."));
        var target = new MySqlConnectionStringBuilder(_connection);
        Assert.Contains(target.Server, new[] { "127.0.0.1", "localhost", "::1" });
        _host = await Mt3cHosting.ProgramHost.StartAsync(_connection, _a, Array.Empty<Guid>(), enabled: false,
            school: false, course: false, exam: false, membershipAdmin: true);
        var hash = BCrypt.Net.BCrypt.HashPassword(Password);
        await Db(async db =>
        {
            db.Institutions.AddRange(new E.Institution { Id = _a, Code = "MT5A_" + _a.ToString("N"), Name = "Temporary A", Status = "active" },
                new E.Institution { Id = _b, Code = "MT5A_" + _b.ToString("N"), Name = "Temporary B", Status = "active" });
            foreach (var name in new[] { "admin", "adminB", "teacher", "student", "multi", "suspended" })
            {
                var user = _users[name] = Guid.NewGuid();
                db.Users.Add(new() { Id = user, Email = Email(user), PasswordHash = hash, Role = "admin" });
                foreach (var b in new[] { false, true })
                {
                    if (name == "adminB" && !b) continue;
                    var id = _members[(name, b)] = Guid.NewGuid();
                    var roles = name switch
                    {
                        "admin" => new[] { b ? "Teacher" : "InstitutionAdmin" },
                        "adminB" => new[] { "InstitutionAdmin" },
                        "teacher" => new[] { b ? "Student" : "Teacher" },
                        "student" => new[] { b ? "Teacher" : "Student" },
                        "multi" => new[] { "Teacher", "Student" },
                        _ => new[] { "InstitutionAdmin" }
                    };
                    db.InstitutionMemberships.Add(new()
                    {
                        Id = id, InstitutionId = b ? _b : _a, UserId = user,
                        Status = name == "suspended" ? "suspended" : "active",
                        JoinedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        SuspendedAt = name == "suspended" ? DateTime.UtcNow : null,
                        InstitutionMembershipRoles = roles.Select(r => new E.InstitutionMembershipRole { MembershipId = id, Role = r }).ToList()
                    });
                }
            }
            await db.SaveChangesAsync();
        });
    }

    public async Task DisposeAsync()
    {
        if (_host is null) return;
        try
        {
            await Db(async db =>
            {
                var ids = _members.Values.ToArray(); var users = _users.Values.ToArray();
                await using var purge = await MembershipFixturePurge.AllowAsync(db, new[]{_a,_b});
                await db.InstitutionMembershipRoles.Where(r => ids.Contains(r.MembershipId)).ExecuteDeleteAsync();
                await db.InstitutionMemberships.Where(m => (m.InstitutionId == _a || m.InstitutionId == _b) && ids.Contains(m.Id)).ExecuteDeleteAsync();
                await db.Users.Where(u => users.Contains(u.Id) && u.Email.EndsWith("@mt5a.example.test")).ExecuteDeleteAsync();
                await db.Institutions.Where(i => (i.Id == _a || i.Id == _b) && i.Code.StartsWith("MT5A_")).ExecuteDeleteAsync();
            });
        }
        finally { await _host.DisposeAsync(); }
    }

    private async Task<HttpResponseMessage> Request(string path = Route, string? token = null, string method = "GET", object? body = null,
        Mt3cHosting.ProgramHost? host = null)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (token is not null) request.Headers.Authorization = new("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await (host ?? _host).Client.SendAsync(request);
    }
    private static async Task Expect(HttpResponseMessage response, HttpStatusCode status) =>
        Assert.True(response.StatusCode == status, $"Expected {status}, got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    private async Task<string> Ticket(string name = "admin")
    {
        var response = await Request("/api/Auth/identity/login", method: "POST", body: new LoginDto { Email = Email(_users[name]), Password = Password });
        await Expect(response, HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<IdentityTicketDto>())!.Ticket;
    }
    private async Task<string> Access(string name = "admin", bool b = false)
    {
        var response = await Request("/api/Auth/select-institution", await Ticket(name), "POST",
            new SelectInstitutionDto { InstitutionId = b ? _b : _a, MembershipId = Member(name, b) });
        await Expect(response, HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<InstitutionAccessDto>())!.Token;
    }
    private async Task<List<InstitutionMemberDto>> Rows(string token, string query = "")
    {
        var response = await Request(Route + query, token); await Expect(response, HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<List<InstitutionMemberDto>>())!;
    }
    // Fresh DbContext every time; secrets participate only in an in-memory digest.
    private async Task<string> Digest()
    {
        string result = "";
        await Db(async db =>
        {
            var snapshot = new
            {
                Users = await db.Users.AsNoTracking().OrderBy(u => u.Id).Select(u => new { u.Id, u.Email, u.PasswordHash, u.Role, u.StudentId, u.TeacherId }).ToListAsync(),
                Memberships = await db.InstitutionMemberships.AsNoTracking().OrderBy(m => m.Id).Select(m => new { m.Id, m.InstitutionId, m.UserId, m.Status, m.JoinedAt, m.SuspendedAt }).ToListAsync(),
                Roles = await db.InstitutionMembershipRoles.AsNoTracking().OrderBy(r => r.MembershipId).ThenBy(r => r.Role).Select(r => new { r.MembershipId, r.Role }).ToListAsync()
            };
            result = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(snapshot))));
        });
        return result;
    }

    [Fact]
    public async Task DirectoryAndDetailKeepGlobalIdentityAndRolesSeparateWithoutWritesOrSecrets()
    {
        var a = await Access(); var b = await Access("adminB", true); var before = await Digest();
        var rowsA = await Rows(a); var rowsB = await Rows(b);
        Assert.Equal(5, rowsA.Count); Assert.Equal(6, rowsB.Count);
        Assert.Equal(_members.Where(m => !m.Key.Item2).Select(m => m.Value).Order(), rowsA.Select(m => m.MembershipId).Order());
        Assert.Equal(_members.Where(m => m.Key.Item2).Select(m => m.Value).Order(), rowsB.Select(m => m.MembershipId).Order());
        var sharedA = Assert.Single(rowsA, m => m.UserId == _users["admin"]);
        var sharedB = Assert.Single(rowsB, m => m.UserId == _users["admin"]);
        Assert.NotEqual(sharedA.MembershipId, sharedB.MembershipId);
        Assert.Equal(new[] { "InstitutionAdmin" }, sharedA.Roles); Assert.Equal(new[] { "Teacher" }, sharedB.Roles);
        Assert.Equal(new[] { "Student", "Teacher" }, Assert.Single(rowsA, m => m.UserId == _users["multi"]).Roles);
        var own = await Request(Route + "/" + Member("teacher"), a); await Expect(own, HttpStatusCode.OK);
        var dto = (await own.Content.ReadFromJsonAsync<InstitutionMemberDto>())!;
        Assert.Equal(_users["teacher"], dto.UserId); Assert.Equal("active", dto.Status); Assert.Equal(DateTimeKind.Utc, dto.JoinedAt.Kind);
        var json = JsonDocument.Parse(await own.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(new[] { "email", "joinedAt", "membershipId", "roles", "status", "userId" }, json.EnumerateObject().Select(p => p.Name).Order());
        var foreign = await Request(Route + "/" + Member("teacher", true), a);
        var absent = await Request(Route + "/" + Guid.NewGuid(), a);
        await Expect(foreign, HttpStatusCode.NotFound); await Expect(absent, HttpStatusCode.NotFound);
        Assert.Equal(await absent.Content.ReadAsStringAsync(), await foreign.Content.ReadAsStringAsync());
        Assert.Equal(before, await Digest());
    }

    [Fact]
    public async Task FiltersSearchAndPaginationAreLocalDeterministicAndReadOnly()
    {
        var a = await Access(); var b = await Access("adminB", true); var before = await Digest();
        var all = await Rows(a); var pages = new List<Guid>();
        for (var page = 1; page <= 3; page++) pages.AddRange((await Rows(a, "?pageSize=2&pageNumber=" + page)).Select(r => r.MembershipId));
        Assert.Equal(all.Select(r => r.MembershipId), pages); Assert.Equal(pages.Count, pages.Distinct().Count());
        Assert.Empty(await Rows(a, "?pageNumber=100"));
        Assert.Equal(Member("suspended"), Assert.Single(await Rows(a, "?status=suspended")).MembershipId);
        Assert.All(await Rows(a, "?role=Teacher"), r => Assert.Contains("Teacher", r.Roles));
        Assert.Equal(2, (await Rows(a, "?role=Teacher")).Count);
        Assert.Single(await Rows(a, "?role=InstitutionAdmin&status=active"));
        Assert.Empty(await Rows(a, "?search=" + Email(_users["adminB"])));
        Assert.Single(await Rows(b, "?search=" + Email(_users["adminB"])));
        Assert.Empty(await Rows(a, "?search=not-registered%40example.test"));
        Assert.Empty(await Rows(a, "?search=%25")); // percent is literal, never a LIKE wildcard
        Assert.Equal(all.Select(r => r.MembershipId), (await Rows(a, "?institutionId=" + _b)).Select(r => r.MembershipId));
        Assert.Equal(before, await Digest());
    }

    [Theory]
    [InlineData("pageNumber=0")]
    [InlineData("pageNumber=2147483647&pageSize=100")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("status=Active")]
    [InlineData("status=unknown")]
    [InlineData("role=admin")]
    [InlineData("role=1")]
    [InlineData("search=%20")]
    [InlineData("search=")]
    [InlineData("status=")]
    [InlineData("role=")]
    public async Task InvalidFiltersReturn400(string query)
    {
        var token = await Access(); var before = await Digest();
        await Expect(await Request(Route + "?" + query, token), HttpStatusCode.BadRequest);
        await Expect(await Request(Route + "?search=" + new string('a', 101), token), HttpStatusCode.BadRequest);
        Assert.Equal(before, await Digest());
    }

    [Theory]
    [InlineData("teacher", false)]
    [InlineData("student", false)]
    [InlineData("multi", false)]
    [InlineData("admin", true)]
    public async Task MembershipRolesNeverInheritGlobalAdminOrAnotherTenantAdmin(string name, bool b)
    {
        var token = await Access(name, b); var before = await Digest();
        foreach (var path in new[] { Route, Route + "/" + Member("teacher", b) })
            await Expect(await Request(path, token), HttpStatusCode.Forbidden);
        Assert.Equal(before, await Digest());
    }

    [Fact]
    public async Task MissingLegacySelectionExpiredAndSuspendedTokensCannotRead()
    {
        var token = await Access(); var before = await Digest();
        await Expect(await Request(), HttpStatusCode.Unauthorized);
        await Expect(await Request(token: await Ticket()), HttpStatusCode.Unauthorized);
        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(token);
        string Sign(string audience, DateTime expiry, IEnumerable<System.Security.Claims.Claim> claims, string type)
        {
            var jwt = new JwtSecurityToken("mt2-tests", audience, claims, DateTime.UtcNow.AddHours(-1), expiry,
                new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Mt3cHosting.Key)), SecurityAlgorithms.HmacSha256));
            jwt.Header["typ"] = type; return new JwtSecurityTokenHandler().WriteToken(jwt);
        }
        await Expect(await Request(token: Sign(InstitutionTokenContract.AccessAudience("mt2-client"), DateTime.UtcNow.AddMinutes(-1), parsed.Claims.Where(c => c.Type is not ("exp" or "nbf" or "iss" or "aud")), InstitutionTokenContract.AccessType)), HttpStatusCode.Unauthorized);
        var legacy = Sign("mt2-client", DateTime.UtcNow.AddMinutes(5), new[] { new System.Security.Claims.Claim("sub", _users["admin"].ToString()), new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "admin") }, "JWT");
        await Expect(await Request(token: legacy), HttpStatusCode.Unauthorized);
        using var scope = _host.Host.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<IInstitutionTokens>();
        var suspended = tokens.Access(await PersistedTestTenant.ReadAsync(scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>(),
            new(_users["suspended"], _a, Member("suspended"), new[] { ElectronicRegisterAPI.Domain.Enums.InstitutionRole.InstitutionAdmin })));
        await Expect(await Request(token: suspended), HttpStatusCode.Forbidden);
        foreach (var path in new[] { "/api/Users", "/api/Users/count", "/api/institution/users" })
            Assert.Contains((await Request(path, token)).StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.NotFound });
        Assert.Equal(before, await Digest());
    }

    [Theory]
    [InlineData("membership")]
    [InlineData("institution")]
    [InlineData("remove-role")]
    [InlineData("add-role")]
    public async Task CurrentStateRevokesBothActionsAndDoesNotAffectOtherTenant(string change)
    {
        var token = await Access(); var b = await Access("adminB", true);
        await Db(async db =>
        {
            if (change == "membership") await db.InstitutionMemberships.Where(m => m.Id == Member("admin")).ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, "suspended").SetProperty(m => m.SuspendedAt, DateTime.UtcNow));
            if (change == "institution") await db.Institutions.Where(i => i.Id == _a).ExecuteUpdateAsync(s => s.SetProperty(i => i.Status, "suspended"));
            if (change == "remove-role") await db.InstitutionMembershipRoles.Where(r => r.MembershipId == Member("admin")).ExecuteDeleteAsync();
            if (change == "add-role") { db.InstitutionMembershipRoles.Add(new() { MembershipId = Member("admin"), Role = "Teacher" }); await db.SaveChangesAsync(); }
        });
        var before = await Digest();
        foreach (var path in new[] { Route, Route + "/" + Member("teacher") }) await Expect(await Request(path, token), HttpStatusCode.Forbidden);
        Assert.Equal(6, (await Rows(b)).Count); Assert.Equal(before, await Digest());
        if (change == "add-role") await Expect(await Request(token: await Access()), HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task BootstrapPoliciesReadOnlyRoutesAndClosedDefaultAreExplicit(string environment)
    {
        await using var host = await Mt3cHosting.ProgramHost.StartAsync(_connection, _a, Array.Empty<Guid>(), enabled: false,
            school: false, course: false, exam: false, environment: environment);
        var token = await Access(); var before = await Digest();
        var provider = host.Host.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var policy = (await provider.GetPolicyAsync(InstitutionMembershipAdminEndpointAttribute.PolicyName))!;
        Assert.Equal(new[] { InstitutionTokenContract.AccessScheme }, policy.AuthenticationSchemes);
        Assert.Contains(policy.Requirements.OfType<Microsoft.AspNetCore.Authorization.Infrastructure.RolesAuthorizationRequirement>(), r => r.AllowedRoles.SequenceEqual(new[] { "InstitutionAdmin" }));
        var endpoints = host.Host.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToArray();
        var admin = endpoints.Where(e => e.Metadata.GetMetadata<InstitutionMembershipAdminEndpointAttribute>() is not null).ToArray();
        Assert.Equal(2, admin.Length);
        Assert.All(admin, e =>
        {
            Assert.Equal(new[] { "GET" }, e.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods);
            Assert.Equal(new[] { InstitutionMembershipAdminEndpointAttribute.PolicyName }, e.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(a => a.Policy));
            Assert.Null(e.Metadata.GetMetadata<IAllowAnonymous>());
        });
        Assert.Equal(156, endpoints.Count(e => e.Metadata.GetMetadata<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>() is not null));
        foreach (var value in new string?[] { null, "false", "invalid" })
        {
            host.Host.Services.GetRequiredService<IConfiguration>()[InstitutionMembershipAdminEndpointAttribute.RolloutKey] = value;
            foreach (var path in new[] { Route, Route + "/" + Member("teacher") }) await Expect(await Request(path, token, host: host), HttpStatusCode.Forbidden);
        }
        foreach (var method in new[] { "POST", "PUT", "DELETE", "PATCH" })
            foreach (var path in new[] { Route, Route + "/" + Member("teacher") })
                await Expect(await Request(path, token, method, host: host), HttpStatusCode.MethodNotAllowed);
        using (var scope = host.Host.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<IInstitutionMembershipAdminManager>();
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => manager.ListAsync());
        }
        await Expect(await host.Client.GetAsync("/openapi/v1.json"), environment == "Development" ? HttpStatusCode.OK : HttpStatusCode.NotFound);
        Directory.CreateDirectory(Evidence);
        await File.WriteAllTextAsync(Path.Combine(Evidence, "routes-" + environment + ".json"), JsonSerializer.Serialize(admin.Select(e => new { route = e.RoutePattern.RawText, methods = e.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods, policy = InstitutionMembershipAdminEndpointAttribute.PolicyName }), new JsonSerializerOptions { WriteIndented = true }));
        Assert.Equal(before, await Digest());
    }

    [Fact]
    public async Task RepositoryUsesOneBoundedSqlProjectionPerReadAndNoTrackedEntities()
    {
        var token = await Access(); var before = await Digest();
        using var capture = new SqlCapture(); using var subscription = DiagnosticListener.AllListeners.Subscribe(capture);
        var observations = new List<object>();
        foreach (var query in new[] { "", "?status=active", "?role=Teacher", "?search=mt5a", "?pageSize=1&pageNumber=2", "/" + Member("teacher") })
        {
            capture.Commands.Clear(); await Expect(await Request(Route + query, token), HttpStatusCode.OK);
            var commands = capture.Commands.ToArray();
            // MT2 authorization + manager revalidation + one resource query, independent of member count.
            Assert.Equal(3, commands.Length);
            Assert.All(commands, sql => Assert.Matches("(?is)WHERE.*institution_id", sql));
            var resource = commands.Last(); Assert.DoesNotContain("password_hash", resource); Assert.DoesNotContain("SELECT *", resource);
            if (query.StartsWith("?role")) Assert.Contains("EXISTS", resource);
            if (!query.StartsWith("/")) Assert.Contains("LIMIT", resource);
            await using var connection = new MySqlConnection(_connection); await connection.OpenAsync();
            await using var explain = new MySqlCommand("EXPLAIN " + resource, connection);
            foreach (var parameter in capture.LastParameters) explain.Parameters.AddWithValue(parameter.Key, parameter.Value);
            await using var reader = await explain.ExecuteReaderAsync();
            var plan = new List<Dictionary<string, object?>>();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                for (var i = 0; i < reader.FieldCount; i++) row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                plan.Add(row);
            }
            observations.Add(new { query, commands, plan });
        }
        using (var scope = _host.Host.Services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IInstitutionMembershipDirectoryRepository>();
            var db = scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
            Assert.Null(await repo.GetAsync(_a, Member("teacher", true), default));
            Assert.Equal(5, (await repo.ListAsync(_a, 1, 20, null, null, null, default)).Count);
            Assert.Empty(db.ChangeTracker.Entries());
        }
        Directory.CreateDirectory(Evidence);
        await File.WriteAllTextAsync(Path.Combine(Evidence, "directory-sql.json"), JsonSerializer.Serialize(observations, new JsonSerializerOptions { WriteIndented = true }));
        Assert.Equal(before, await Digest());
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("school")]
    [InlineData("course")]
    [InlineData("exam")]
    [InlineData("duplicate")]
    [InlineData("policy")]
    [InlineData("scheme")]
    [InlineData("anonymous")]
    [InlineData("wrong-marker-policy")]
    [InlineData("no-context")]
    public async Task MarkerCannotBypassGateWithCombinedOrAlternativeMetadata(string kind)
    {
        using var scope = _host.Host.Services.CreateScope();
        var current = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        if (kind != "no-context") current.GetType().GetMethod("Set", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(current, new object[] { new ElectronicRegisterAPI.Domain.Models.TenantContext(_users["admin"], _a, Member("admin"), new[] { ElectronicRegisterAPI.Domain.Enums.InstitutionRole.InstitutionAdmin }) });
        var marker = new InstitutionMembershipAdminEndpointAttribute(); var metadata = new List<object> { marker };
        if (kind != "no-context") metadata.Add(kind switch
        {
            "identity" => new InstitutionIdentityEndpointAttribute(), "school" => new InstitutionSchoolEndpointAttribute(),
            "course" => new InstitutionCourseEndpointAttribute(), "exam" => new InstitutionExamEndpointAttribute(),
            "duplicate" => new InstitutionMembershipAdminEndpointAttribute(), "policy" => new AuthorizeAttribute { Policy = InstitutionTokenContract.AccessScheme },
            "scheme" => new AuthorizeAttribute { AuthenticationSchemes = "Bearer" }, "anonymous" => new AllowAnonymousAttribute(),
            _ => new AuthorizeAttribute { Roles = "InstitutionAdmin" }
        });
        if (kind == "wrong-marker-policy") marker.Policy = InstitutionTokenContract.AccessScheme;
        var gateType = typeof(InstitutionMembershipAdminEndpointAttribute).Assembly.GetType("ElectronicRegisterAPI.Api.Security.LegacyEndpointGate")!;
        var gate = (Microsoft.AspNetCore.Mvc.Filters.IAsyncAuthorizationFilter)scope.ServiceProvider.GetRequiredService(gateType);
        var http = new Microsoft.AspNetCore.Http.DefaultHttpContext { RequestServices = scope.ServiceProvider,
            User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[] { new System.Security.Claims.Claim("sub", _users["admin"].ToString()) }, "test")) };
        var action = new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor { EndpointMetadata = metadata };
        var context = new Microsoft.AspNetCore.Mvc.Filters.AuthorizationFilterContext(new Microsoft.AspNetCore.Mvc.ActionContext(http, new RouteData(), action), new List<Microsoft.AspNetCore.Mvc.Filters.IFilterMetadata>());
        await gate.OnAuthorizationAsync(context);
        Assert.Equal(403, Assert.IsType<Microsoft.AspNetCore.Mvc.ObjectResult>(context.Result).StatusCode);
    }

    private sealed class SqlCapture : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
    {
        private readonly List<IDisposable> _subscriptions = new();
        internal ConcurrentQueue<string> Commands { get; } = new();
        internal KeyValuePair<string, object?>[] LastParameters { get; private set; } = Array.Empty<KeyValuePair<string, object?>>();
        public void OnNext(DiagnosticListener listener) { if (listener.Name == "Microsoft.EntityFrameworkCore") _subscriptions.Add(listener.Subscribe(this)); }
        public void OnNext(KeyValuePair<string, object?> value)
        {
            if (value.Value is CommandExecutedEventData data && data.Command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            {
                LastParameters = data.Command.Parameters.Cast<System.Data.Common.DbParameter>().Select(p => new KeyValuePair<string, object?>(p.ParameterName, p.Value)).ToArray();
                Commands.Enqueue(data.Command.CommandText);
            }
        }
        public void OnCompleted() { }
        public void OnError(Exception error) => throw error;
        public void Dispose() { foreach (var subscription in _subscriptions) subscription.Dispose(); }
    }
}
