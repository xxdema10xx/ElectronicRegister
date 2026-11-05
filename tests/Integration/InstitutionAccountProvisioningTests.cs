using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using ElectronicRegister.OwnerProvisioning;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

// POST /api/institution/accounts: decision 3 permissions, one transaction, no
// cross-institution disclosure, temporary password shown once.
[Collection(IntegrationCollection.Name)]
public sealed class InstitutionAccountProvisioningTests(IntegrationFixture fixture) : IAsyncLifetime
{
    private const string Accounts = "/api/institution/accounts";
    private readonly string _suffix = Guid.NewGuid().ToString("N");
    private readonly ConcurrentQueue<string> _logs = new();
    private Mt3cHosting.ProgramHost _host = null!;
    private string _uuid = "";
    private (Guid Id, string Admin, string Email) _a, _b;

    private sealed class CapturingLogger(ConcurrentQueue<string> sink) : ILoggerProvider, ILogger
    {
        public ILogger CreateLogger(string categoryName) => this;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => sink.Enqueue(formatter(state, exception) + " " + exception);
        public void Dispose() { }
    }

    private sealed class FailingCommit : IOnboardingFaultObserver
    {
        public Task BeforeCommitAsync(string action, CancellationToken ct) => action == "AccountCreate" ? throw new InvalidOperationException("injected account fault") : Task.CompletedTask;
    }

    private Task<Mt3cHosting.ProgramHost> Start(bool write = true, bool fault = false) =>
        Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString, Guid.NewGuid(), Array.Empty<Guid>(), enabled: false, school: true, membershipWrite: write,
            configureServices: services =>
            {
                services.AddSingleton<ILoggerProvider>(new CapturingLogger(_logs));
                if (fault) services.Replace(ServiceDescriptor.Scoped<IOnboardingFaultObserver, FailingCommit>());
            },
            extraArguments: new[] { "--Logging:LogLevel:Default=Trace" });

    public async Task InitializeAsync()
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);
        await using (var c = new MySqlConnection(fixture.ConnectionString)) { await c.OpenAsync(); await using var cmd = new MySqlCommand("SELECT @@server_uuid", c); _uuid = (string)(await cmd.ExecuteScalarAsync())!; }
        _host = await Start();
        _a = await Institution("A");
        _b = await Institution("B");
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private async Task<(Guid Id, string Admin, string Email)> Institution(string name)
    {
        var email = "admin" + name.ToLowerInvariant() + "." + _suffix + "@accounts.example.test";
        using StringWriter output = new(), error = new();
        Assert.Equal(0, await OwnerProvisioningCli.RunAsync(new[] { "create-institution", "--code", "ACC" + name + "_" + _suffix[..16], "--name", "Istituto " + name, "--admin-email", email, "--expected-server-uuid", _uuid }, output, error,
            v => v == OwnerProvisioningCli.ConnectionVariable ? fixture.ConnectionString : null));
        var lines = output.ToString().Split('\n').Select(l => l.Trim()).ToArray();
        var id = Guid.Parse(lines.Single(l => l.StartsWith("institution: ")).Split(": ")[1]);
        var temporary = lines.Single(l => l.StartsWith("temporary password (shown once")).Split(": ", 2)[1];
        return (id, await Activate(_host, email, temporary, "AdminPassword!1", id), email);
    }

    private static async Task<HttpResponseMessage> Send(Mt3cHosting.ProgramHost host, string route, string? token = null, object? body = null, string method = "GET")
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), route);
        if (token is not null) request.Headers.Authorization = new("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await host.Client.SendAsync(request);
    }

    private Task<HttpResponseMessage> Send(string route, string? token = null, object? body = null, string method = "GET") => Send(_host, route, token, body, method);

    private static async Task<IdentityTicketDto?> Login(Mt3cHosting.ProgramHost host, string email, string password)
    {
        using var response = await Send(host, "/api/Auth/identity/login", body: new LoginDto { Email = email, Password = password }, method: "POST");
        return response.StatusCode == HttpStatusCode.OK ? await response.Content.ReadFromJsonAsync<IdentityTicketDto>() : null;
    }

    private static async Task<string> Activate(Mt3cHosting.ProgramHost host, string email, string temporary, string final, Guid institution)
    {
        var ticket = (await Login(host, email, temporary))!;
        Assert.True(ticket.PasswordChangeRequired);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(host, "/api/Auth/institutions", ticket.Ticket)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Send(host, "/api/identity/account/password", ticket.Ticket, new ChangeIdentityPasswordDto(temporary, final), "PUT")).StatusCode);
        var fresh = (await Login(host, email, final))!;
        Assert.False(fresh.PasswordChangeRequired);
        var choice = Assert.Single((await (await Send(host, "/api/Auth/institutions", fresh.Ticket)).Content.ReadFromJsonAsync<InstitutionChoiceDto[]>())!);
        Assert.Equal(institution, choice.InstitutionId);
        using var selected = await Send(host, "/api/Auth/select-institution", fresh.Ticket, new SelectInstitutionDto { InstitutionId = choice.InstitutionId, MembershipId = choice.MembershipId }, "POST");
        Assert.Equal(HttpStatusCode.OK, selected.StatusCode);
        return (await selected.Content.ReadFromJsonAsync<InstitutionAccessDto>())!.Token;
    }

    private string NewEmail(string who) => who + "." + Guid.NewGuid().ToString("N")[..12] + "@accounts.example.test";

    private static object Body(string email, string[] roles, Guid? eventId = null, string? first = "Mario", string? last = "Rossi", Guid? studentId = null, Guid? teacherId = null) =>
        roles.Contains("Student") || roles.Contains("Teacher")
            ? studentId.HasValue ? new { eventId = eventId ?? Guid.NewGuid(), email, roles, studentId, reason = "Test account" }
            : teacherId.HasValue ? new { eventId = eventId ?? Guid.NewGuid(), email, roles, teacherId, reason = "Test account" }
            : new { eventId = eventId ?? Guid.NewGuid(), email, roles, firstName = first, lastName = last, reason = "Test account" }
            : new { eventId = eventId ?? Guid.NewGuid(), email, roles, reason = "Test account" };

    private async Task<InstitutionAccountCreated> Created(string token, object body)
    {
        using var response = await Send(Accounts, token, body, "POST");
        Assert.True(response.StatusCode == HttpStatusCode.Created, response.StatusCode + " " + await response.Content.ReadAsStringAsync());
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        return (await response.Content.ReadFromJsonAsync<InstitutionAccountCreated>())!;
    }

    private async Task<string> Member(string token, Guid institution, string[] roles)
    {
        var email = NewEmail(string.Join("-", roles).ToLowerInvariant());
        var created = await Created(token, Body(email, roles));
        return await Activate(_host, email, created.TemporaryPassword!, "MemberPassword!1", institution);
    }

    private async Task<long> Digest()
    {
        await using var c = new MySqlConnection(fixture.ConnectionString); await c.OpenAsync();
        await using var cmd = new MySqlCommand("SELECT (SELECT COUNT(*) FROM `user`)+(SELECT COUNT(*) FROM institution_membership)+(SELECT COUNT(*) FROM institution_membership_role)+(SELECT COUNT(*) FROM student)+(SELECT COUNT(*) FROM teacher)+(SELECT COUNT(*) FROM institution_student_profile)+(SELECT COUNT(*) FROM institution_teacher_profile)+(SELECT COUNT(*) FROM identity_account_event)+(SELECT COUNT(*) FROM institution_onboarding_event)+(SELECT COUNT(*) FROM institution_email_reservation)", c);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync());
    }

    private async Task<long> Count(string sql, params (string, object)[] values)
    {
        await using var c = new MySqlConnection(fixture.ConnectionString); await c.OpenAsync();
        await using var cmd = new MySqlCommand(sql, c);
        foreach (var (n, v) in values) cmd.Parameters.AddWithValue(n, v);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync());
    }

    [Fact]
    public async Task AnonymousAndSelectionTicketsCannotCreateAccounts()
    {
        var before = await Digest();
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(Accounts, null, Body(NewEmail("anon"), new[] { "Teacher" }), "POST")).StatusCode);
        var ticket = (await Login(_host, _a.Email, "AdminPassword!1"))!.Ticket;
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(Accounts, ticket, Body(NewEmail("ticket"), new[] { "Teacher" }), "POST")).StatusCode);
        Assert.Equal(before, await Digest());
    }

    [Fact]
    public async Task TeacherStudentAndForeignAdminCannotCreateAccountsInTheInstitutionAndNothingChanges()
    {
        var teacher = await Member(_a.Admin, _a.Id, new[] { "Teacher" });
        var student = await Member(_a.Admin, _a.Id, new[] { "Student" });
        var before = await Digest();
        foreach (var token in new[] { teacher, student })
            foreach (var roles in new[] { new[] { "Student" }, new[] { "Teacher" }, new[] { "InstitutionAdmin" } })
                Assert.Equal(HttpStatusCode.Forbidden, (await Send(Accounts, token, Body(NewEmail("low"), roles), "POST")).StatusCode);
        // The institution is never accepted from the client: a foreign admin cannot name A.
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(Accounts, _b.Admin, new { eventId = Guid.NewGuid(), email = NewEmail("foreign"), roles = new[] { "Teacher" }, firstName = "A", lastName = "B", reason = "x", institutionId = _a.Id }, "POST")).StatusCode);
        Assert.Equal(before, await Digest());
        // What a foreign admin creates always lands in its own institution.
        var created = await Created(_b.Admin, Body(NewEmail("foreign"), new[] { "Teacher" }));
        Assert.Equal(1, await Count("SELECT COUNT(*) FROM institution_membership WHERE id=@m AND institution_id=@b", ("@m", created.MembershipId), ("@b", _b.Id)));
        Assert.Equal(0, await Count("SELECT COUNT(*) FROM institution_membership WHERE user_id=@u AND institution_id=@a", ("@u", created.UserId), ("@a", _a.Id)));
    }

    [Fact]
    public async Task StaffCannotCreateStaffOrAdminEvenWithAdditionalRolesAndNothingChanges()
    {
        var staff = await Member(_a.Admin, _a.Id, new[] { "Staff" });
        var before = await Digest();
        foreach (var roles in new[] { new[] { "Staff" }, new[] { "InstitutionAdmin" }, new[] { "Teacher", "InstitutionAdmin" }, new[] { "Student", "Staff" }, new[] { "Teacher", "Staff" }, new[] { "InstitutionAdmin", "Staff" } })
        {
            using var response = await Send(Accounts, staff, Body(NewEmail("escalate"), roles), "POST");
            Assert.True(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.BadRequest, string.Join(",", roles) + " " + response.StatusCode);
        }
        // A role smuggled outside the array is an unknown field: rejected, never ignored.
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(Accounts, staff, new { eventId = Guid.NewGuid(), email = NewEmail("smuggle"), roles = new[] { "Teacher" }, role = "InstitutionAdmin", firstName = "A", lastName = "B", reason = "x" }, "POST")).StatusCode);
        Assert.Equal(before, await Digest());
    }

    [Fact]
    public async Task StaffCreatesStudentAndTeacherAccountsLinkedToNewOrExistingProfiles()
    {
        var staff = await Member(_a.Admin, _a.Id, new[] { "Staff" });
        var studentEmail = NewEmail("student");
        var student = await Created(staff, Body(studentEmail, new[] { "Student" }, first: "Giulia", last: "Bianchi"));
        Assert.Equal(new[] { "Student" }, student.Roles); Assert.NotNull(student.StudentId); Assert.Null(student.TeacherId);
        var studentAccess = await Activate(_host, studentEmail, student.TemporaryPassword!, "StudentPassword!1", _a.Id);
        var context = (await (await Send("/api/Auth/context", studentAccess)).Content.ReadFromJsonAsync<TenantContextDto>())!;
        Assert.Equal(new[] { "Student" }, context.Roles); Assert.Equal(_a.Id, context.InstitutionId);
        var me = await Send("/api/institution/students/me", studentAccess);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Contains("Giulia", await me.Content.ReadAsStringAsync());
        // Existing profile created earlier by Staff, then given an account.
        using var profile = await Send("/api/institution/teachers", staff, new CreateTeacherDto { FirstName = "Luca", LastName = "Verdi" }, "POST");
        Assert.Equal(HttpStatusCode.Created, profile.StatusCode);
        var teacherId = (await profile.Content.ReadFromJsonAsync<Guid>());
        var teacherEmail = NewEmail("teacher");
        var teacher = await Created(staff, Body(teacherEmail, new[] { "Teacher" }, teacherId: teacherId));
        Assert.Equal(teacherId, teacher.TeacherId);
        var teacherAccess = await Activate(_host, teacherEmail, teacher.TemporaryPassword!, "TeacherPassword!1", _a.Id);
        Assert.Equal(HttpStatusCode.OK, (await Send("/api/institution/teachers/me", teacherAccess)).StatusCode);
        // A profile already linked, or one of another institution, cannot be reused.
        Assert.Equal(HttpStatusCode.Conflict, (await Send(Accounts, staff, Body(NewEmail("again"), new[] { "Teacher" }, teacherId: teacherId), "POST")).StatusCode);
        using var foreignProfile = await Send("/api/institution/teachers", _b.Admin, new CreateTeacherDto { FirstName = "B", LastName = "Teacher" }, "POST");
        Assert.Equal(HttpStatusCode.NotFound, (await Send(Accounts, staff, Body(NewEmail("foreignprofile"), new[] { "Teacher" }, teacherId: await foreignProfile.Content.ReadFromJsonAsync<Guid>()), "POST")).StatusCode);
    }

    [Fact]
    public async Task AdminCreatesAnotherAdminWhoAdministersOnlyTheSameInstitution()
    {
        var email = NewEmail("admin2");
        var created = await Created(_a.Admin, Body(email, new[] { "InstitutionAdmin" }));
        var admin2 = await Activate(_host, email, created.TemporaryPassword!, "SecondAdmin!1", _a.Id);
        var context = (await (await Send("/api/Auth/context", admin2)).Content.ReadFromJsonAsync<TenantContextDto>())!;
        Assert.Equal(_a.Id, context.InstitutionId); Assert.Equal(new[] { "InstitutionAdmin" }, context.Roles);
        // The second admin can itself create admins and members, always in A.
        var third = await Created(admin2, Body(NewEmail("admin3"), new[] { "InstitutionAdmin" }));
        var teacher = await Created(admin2, Body(NewEmail("teacher"), new[] { "Teacher" }));
        foreach (var membership in new[] { third.MembershipId, teacher.MembershipId })
            Assert.Equal(1, await Count("SELECT COUNT(*) FROM institution_membership WHERE id=@m AND institution_id=@a", ("@m", membership), ("@a", _a.Id)));
        Assert.Equal(0, await Count("SELECT COUNT(*) FROM institution_membership m JOIN `user` u ON u.id=m.user_id WHERE u.email=@e AND m.institution_id<>@a", ("@e", email), ("@a", _a.Id)));
        // No upper bound on admins: four now exist in A.
        Assert.Equal(3, await Count("SELECT COUNT(*) FROM institution_membership m JOIN institution_membership_role r ON r.membership_id=m.id AND r.role='InstitutionAdmin' WHERE m.institution_id=@a", ("@a", _a.Id)));
    }

    private static string Neutral(string body)
    {
        var json = JsonNode.Parse(body)!.AsObject();
        json.Remove("traceId");
        return json.ToJsonString();
    }

    [Fact]
    public async Task EmailUsedInAnotherInstitutionRevealsNothing()
    {
        var local = await Created(_a.Admin, Body(NewEmail("local"), new[] { "Staff" }));
        var localEmail = await Email(local.UserId);
        var before = await Digest();
        var bodies = new List<string>();
        foreach (var taken in new[] { _b.Email, localEmail, _b.Email.ToUpperInvariant() })
        {
            using var response = await Send(Accounts, _a.Admin, Body(taken, new[] { "Staff" }), "POST");
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            bodies.Add(Neutral(await response.Content.ReadAsStringAsync()));
        }
        Assert.All(bodies, b => Assert.Equal(bodies[0], b));
        Assert.DoesNotContain(_b.Id.ToString(), bodies[0]);
        Assert.Equal(before, await Digest());
        // Timing: a foreign address costs the same BCrypt work as a successful creation.
        async Task<double> Median(Func<Task<HttpResponseMessage>> call)
        {
            var samples = new List<double>();
            for (var i = -1; i < 7; i++) { var t = Stopwatch.StartNew(); using var r = await call(); if (i >= 0) samples.Add(t.Elapsed.TotalMilliseconds); }
            return samples.Order().ElementAt(samples.Count / 2);
        }
        var foreign = await Median(() => Send(Accounts, _a.Admin, Body(_b.Email, new[] { "Staff" }), "POST"));
        var fresh = await Median(() => Send(Accounts, _a.Admin, Body(NewEmail("fresh"), new[] { "Staff" }), "POST"));
        Assert.True(Math.Min(foreign, fresh) * 2 >= Math.Max(foreign, fresh), $"Creation timing differs: foreign {foreign:F1} ms, fresh {fresh:F1} ms");
    }

    private async Task<string> Email(Guid user)
    {
        await using var c = new MySqlConnection(fixture.ConnectionString); await c.OpenAsync();
        await using var cmd = new MySqlCommand("SELECT email FROM `user` WHERE id=@u", c); cmd.Parameters.AddWithValue("@u", user);
        return (string)(await cmd.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task TemporaryPasswordIsShownOnceNeverStoredLoggedOrReplayed()
    {
        var eventId = Guid.NewGuid(); var email = NewEmail("once");
        var body = Body(email, new[] { "Teacher" }, eventId);
        var created = await Created(_a.Admin, body);
        var password = created.TemporaryPassword!;
        Assert.False(created.Replayed);
        using var replay = await Send(Accounts, _a.Admin, body, "POST");
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayed = (await replay.Content.ReadFromJsonAsync<InstitutionAccountCreated>())!;
        Assert.True(replayed.Replayed); Assert.Null(replayed.TemporaryPassword);
        Assert.Equal(created.UserId, replayed.UserId); Assert.Equal(created.MembershipId, replayed.MembershipId);
        Assert.DoesNotContain(password, await replay.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, (await Send(Accounts, _a.Admin, Body(NewEmail("other"), new[] { "Teacher" }, eventId), "POST")).StatusCode);
        Assert.Equal(0, await Count("SELECT COUNT(*) FROM institution_onboarding_event WHERE LOCATE(@p,CAST(result_json AS CHAR))>0 OR LOCATE(@p,reason)>0 OR LOCATE(@p,intent_hash)>0", ("@p", password)));
        Assert.Equal(0, await Count("SELECT COUNT(*) FROM identity_account_event WHERE LOCATE(@p,action)>0", ("@p", password)));
        Assert.Equal(0, await Count("SELECT COUNT(*) FROM `user` WHERE password_hash=@p", ("@p", password)));
        Assert.Equal(1, await Count("SELECT COUNT(*) FROM institution_onboarding_event WHERE id=@e AND actor_kind='Member' AND action='AccountCreated'", ("@e", eventId)));
        Assert.NotEmpty(_logs);
        Assert.DoesNotContain(_logs, line => line.Contains(password, StringComparison.Ordinal));
        // Until changed, the new account can only replace its password.
        var ticket = (await Login(_host, email, password))!;
        Assert.True(ticket.PasswordChangeRequired);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send("/api/Auth/institutions", ticket.Ticket)).StatusCode);
    }

    [Fact]
    public async Task TokensNeverEnterAnotherInstitutionAndResetRevokesThem()
    {
        var email = NewEmail("teacher");
        var created = await Created(_a.Admin, Body(email, new[] { "Teacher" }));
        var access = await Activate(_host, email, created.TemporaryPassword!, "TeacherPassword!1", _a.Id);
        var ticket = (await Login(_host, email, "TeacherPassword!1"))!.Ticket;
        await using (var c = new MySqlConnection(fixture.ConnectionString))
        {
            await c.OpenAsync();
            await using var cmd = new MySqlCommand("SELECT m.id FROM institution_membership m JOIN `user` u ON u.id=m.user_id WHERE u.email=@e", c); cmd.Parameters.AddWithValue("@e", _b.Email);
            var bMembership = (Guid)(await cmd.ExecuteScalarAsync())!;
            using var select = await Send("/api/Auth/select-institution", ticket, new SelectInstitutionDto { InstitutionId = _b.Id, MembershipId = bMembership }, "POST");
            Assert.True(select.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden);
        }
        Assert.Equal(_a.Id, (await (await Send("/api/Auth/context", access)).Content.ReadFromJsonAsync<TenantContextDto>())!.InstitutionId);
        using StringWriter output = new(), error = new();
        Assert.Equal(0, await OwnerProvisioningCli.RunAsync(new[] { "reset-password", "--email", email, "--expected-server-uuid", _uuid }, output, error, v => v == OwnerProvisioningCli.ConnectionVariable ? fixture.ConnectionString : null));
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send("/api/Auth/context", access)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send("/api/Auth/institutions", ticket)).StatusCode);
        Assert.Null(await Login(_host, email, "TeacherPassword!1"));
    }

    [Fact]
    public async Task FailureMidTransactionLeavesNoPartialData()
    {
        await using var host = await Start(fault: true);
        var token = (await Login(host, _a.Email, "AdminPassword!1"))!.Ticket;
        var choice = Assert.Single((await (await Send(host, "/api/Auth/institutions", token)).Content.ReadFromJsonAsync<InstitutionChoiceDto[]>())!);
        using var selected = await Send(host, "/api/Auth/select-institution", token, new SelectInstitutionDto { InstitutionId = choice.InstitutionId, MembershipId = choice.MembershipId }, "POST");
        var access = (await selected.Content.ReadFromJsonAsync<InstitutionAccessDto>())!.Token;
        var before = await Digest(); var email = NewEmail("fault");
        foreach (var roles in new[] { new[] { "Student" }, new[] { "Teacher" }, new[] { "InstitutionAdmin" } })
            Assert.Equal(HttpStatusCode.InternalServerError, (await Send(host, Accounts, access, Body(email, roles), "POST")).StatusCode);
        Assert.Equal(before, await Digest());
        Assert.Equal(0, await Count("SELECT COUNT(*) FROM `user` WHERE email=@e", ("@e", email)));
        // The same address is still available once the fault is gone.
        var created = await Created(_a.Admin, Body(email, new[] { "Teacher" }));
        Assert.NotNull(created.TemporaryPassword);
    }

    [Fact]
    public async Task EndpointIsOffWithoutTheMembershipWriteFlag()
    {
        await using var host = await Start(write: false);
        var token = (await Login(host, _a.Email, "AdminPassword!1"))!.Ticket;
        var choice = Assert.Single((await (await Send(host, "/api/Auth/institutions", token)).Content.ReadFromJsonAsync<InstitutionChoiceDto[]>())!);
        using var selected = await Send(host, "/api/Auth/select-institution", token, new SelectInstitutionDto { InstitutionId = choice.InstitutionId, MembershipId = choice.MembershipId }, "POST");
        var access = (await selected.Content.ReadFromJsonAsync<InstitutionAccessDto>())!.Token;
        var before = await Digest();
        Assert.Equal(HttpStatusCode.NotFound, (await Send(host, Accounts, access, Body(NewEmail("off"), new[] { "Teacher" }), "POST")).StatusCode);
        Assert.Equal(before, await Digest());
    }
}
