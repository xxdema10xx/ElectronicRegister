using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using ElectronicRegister.OwnerProvisioning;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Policies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

// POST /api/institution/accounts: decision 3 permissions, one transaction, no
// cross-institution disclosure, temporary password shown once, platform address generated
// by the server, fiscal-code deduplication (D-A, E1, E2).
[Collection(IntegrationCollection.Name)]
public sealed partial class InstitutionAccountProvisioningTests(IntegrationFixture fixture) : IAsyncLifetime
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

    private Task<Mt3cHosting.ProgramHost> Start(bool write = true, bool fault = false, bool platform = true) =>
        Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString, Guid.NewGuid(), Array.Empty<Guid>(), enabled: false, school: true, membershipWrite: write,
            configureServices: services =>
            {
                services.AddSingleton<ILoggerProvider>(new CapturingLogger(_logs));
                if (fault) services.Replace(ServiceDescriptor.Scoped<IOnboardingFaultObserver, FailingCommit>());
            },
            extraArguments: new[] { "--Logging:LogLevel:Default=Trace" }.Concat(PlatformIdentityTestConfig.Arguments(platform)).ToArray());

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

    // Unique, letters-only last names keep every generated address predictable per test.
    private static string Unique(string prefix) => prefix + new string(Guid.NewGuid().ToString("N").Select(c => (char)('a' + Convert.ToInt32(c.ToString(), 16) % 26)).Take(10).ToArray());

    private static object Body(string[] roles, Guid? eventId = null, string first = "Mario", string? last = null, string? fiscalCode = null, Guid? studentId = null, Guid? teacherId = null, Guid? classId = null) =>
        new { eventId = eventId ?? Guid.NewGuid(), firstName = first, lastName = last ?? Unique("Rossi"), fiscalCode = fiscalCode ?? PlatformIdentityTestConfig.NewFiscalCode(), roles, studentId, teacherId, classId, reason = "Test account" };

    private async Task<InstitutionAccountCreated> Created(string token, object body)
    {
        using var response = await Send(Accounts, token, body, "POST");
        Assert.True(response.StatusCode == HttpStatusCode.Created, response.StatusCode + " " + await response.Content.ReadAsStringAsync());
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        return (await response.Content.ReadFromJsonAsync<InstitutionAccountCreated>())!;
    }

    private async Task<string> Member(string token, Guid institution, string[] roles)
    {
        var created = await Created(token, Body(roles));
        return await Activate(_host, created.Email, created.TemporaryPassword!, "MemberPassword!1", institution);
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
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(Accounts, null, Body(new[] { "Teacher" }), "POST")).StatusCode);
        var ticket = (await Login(_host, _a.Email, "AdminPassword!1"))!.Ticket;
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(Accounts, ticket, Body(new[] { "Teacher" }), "POST")).StatusCode);
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
                Assert.Equal(HttpStatusCode.Forbidden, (await Send(Accounts, token, Body(roles), "POST")).StatusCode);
        // The institution is never accepted from the client: a foreign admin cannot name A.
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(Accounts, _b.Admin, new { eventId = Guid.NewGuid(), firstName = "A", lastName = "B", fiscalCode = PlatformIdentityTestConfig.NewFiscalCode(), roles = new[] { "Teacher" }, reason = "x", institutionId = _a.Id }, "POST")).StatusCode);
        Assert.Equal(before, await Digest());
        // What a foreign admin creates always lands in its own institution.
        var created = await Created(_b.Admin, Body(new[] { "Teacher" }));
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
            using var response = await Send(Accounts, staff, Body(roles), "POST");
            Assert.True(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.BadRequest, string.Join(",", roles) + " " + response.StatusCode);
        }
        // A role smuggled outside the array is an unknown field: rejected, never ignored.
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(Accounts, staff, new { eventId = Guid.NewGuid(), firstName = "A", lastName = "B", fiscalCode = PlatformIdentityTestConfig.NewFiscalCode(), roles = new[] { "Teacher" }, role = "InstitutionAdmin", reason = "x" }, "POST")).StatusCode);
        Assert.Equal(before, await Digest());
    }

    [Fact]
    public async Task AddressIsGeneratedByTheServerAndClientEmailOrMissingFieldsAreRejectedWithMessages()
    {
        var before = await Digest();
        // The address is no longer a client field: an "email" is an unknown member.
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(Accounts, _a.Admin, new { eventId = Guid.NewGuid(), email = "chosen@platform.example.test", firstName = "A", lastName = "B", fiscalCode = PlatformIdentityTestConfig.NewFiscalCode(), roles = new[] { "Teacher" }, reason = "x" }, "POST")).StatusCode);
        foreach (var (body, fragment) in new (object Body, string Fragment)[]
        {
            (Body(new[] { "Teacher" }, fiscalCode: "RSSMRA80A01H501X"), "Codice fiscale"),
            (Body(new[] { "Teacher" }, first: " "), "Nome"),
            (Body(new[] { "Teacher" }, last: ""), "Cognome"),
            (Body(new[] { "Pilot" }), "Ruolo"),
            (Body(new[] { "Teacher" }, first: "李", last: "王"), "lettere"),
            (new { eventId = Guid.NewGuid(), firstName = "A", lastName = "B", roles = new[] { "Teacher" }, reason = "x" }, "")
        })
        {
            using var response = await Send(Accounts, _a.Admin, body, "POST");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains(fragment, await response.Content.ReadAsStringAsync());
        }
        Assert.Equal(before, await Digest());
        var last = Unique("Esposito");
        var created = await Created(_a.Admin, Body(new[] { "Teacher" }, first: "Niccolò", last: last));
        Assert.Equal(PlatformIdentityTestConfig.Address("Niccolò", last), created.Email);
        Assert.Equal(created.Email, await Email(created.UserId));
    }

    [Fact]
    public async Task StaffCreatesStudentAndTeacherAccountsLinkedToNewOrExistingProfiles()
    {
        var staff = await Member(_a.Admin, _a.Id, new[] { "Staff" });
        var student = await Created(staff, Body(new[] { "Student" }, first: "Giulia", last: "Bianchi"));
        Assert.Equal(new[] { "Student" }, student.Roles); Assert.NotNull(student.StudentId); Assert.Null(student.TeacherId);
        var studentAccess = await Activate(_host, student.Email, student.TemporaryPassword!, "StudentPassword!1", _a.Id);
        var context = (await (await Send("/api/Auth/context", studentAccess)).Content.ReadFromJsonAsync<TenantContextDto>())!;
        Assert.Equal(new[] { "Student" }, context.Roles); Assert.Equal(_a.Id, context.InstitutionId);
        var me = await Send("/api/institution/students/me", studentAccess);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Contains("Giulia", await me.Content.ReadAsStringAsync());
        // Existing profile created earlier by Staff, then given an account with the same names (E2).
        using var profile = await Send("/api/institution/teachers", staff, new CreateTeacherDto { FirstName = "Luca", LastName = "Verdì" }, "POST");
        Assert.Equal(HttpStatusCode.Created, profile.StatusCode);
        var teacherId = (await profile.Content.ReadFromJsonAsync<Guid>());
        var before = await Digest();
        using (var mismatch = await Send(Accounts, staff, Body(new[] { "Teacher" }, first: "Luca", last: "Bianchi", teacherId: teacherId), "POST"))
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, mismatch.StatusCode);
            Assert.Contains("scheda collegata", await mismatch.Content.ReadAsStringAsync());
        }
        Assert.Equal(before, await Digest());
        var teacher = await Created(staff, Body(new[] { "Teacher" }, first: "LUCA", last: "verdi", teacherId: teacherId));
        Assert.Equal(teacherId, teacher.TeacherId);
        var teacherAccess = await Activate(_host, teacher.Email, teacher.TemporaryPassword!, "TeacherPassword!1", _a.Id);
        Assert.Equal(HttpStatusCode.OK, (await Send("/api/institution/teachers/me", teacherAccess)).StatusCode);
        // A profile already linked, or one of another institution, cannot be reused.
        Assert.Equal(HttpStatusCode.Conflict, (await Send(Accounts, staff, Body(new[] { "Teacher" }, first: "Luca", last: "Verdi", teacherId: teacherId), "POST")).StatusCode);
        using var foreignProfile = await Send("/api/institution/teachers", _b.Admin, new CreateTeacherDto { FirstName = "B", LastName = "Teacher" }, "POST");
        Assert.Equal(HttpStatusCode.NotFound, (await Send(Accounts, staff, Body(new[] { "Teacher" }, first: "B", last: "Teacher", teacherId: await foreignProfile.Content.ReadFromJsonAsync<Guid>()), "POST")).StatusCode);
    }

    [Fact]
    public async Task AdminCreatesAnotherAdminWhoAdministersOnlyTheSameInstitution()
    {
        var created = await Created(_a.Admin, Body(new[] { "InstitutionAdmin" }));
        var admin2 = await Activate(_host, created.Email, created.TemporaryPassword!, "SecondAdmin!1", _a.Id);
        var context = (await (await Send("/api/Auth/context", admin2)).Content.ReadFromJsonAsync<TenantContextDto>())!;
        Assert.Equal(_a.Id, context.InstitutionId); Assert.Equal(new[] { "InstitutionAdmin" }, context.Roles);
        // The second admin can itself create admins and members, always in A.
        var third = await Created(admin2, Body(new[] { "InstitutionAdmin" }));
        var teacher = await Created(admin2, Body(new[] { "Teacher" }));
        foreach (var membership in new[] { third.MembershipId, teacher.MembershipId })
            Assert.Equal(1, await Count("SELECT COUNT(*) FROM institution_membership WHERE id=@m AND institution_id=@a", ("@m", membership), ("@a", _a.Id)));
        Assert.Equal(0, await Count("SELECT COUNT(*) FROM institution_membership m JOIN `user` u ON u.id=m.user_id WHERE u.email=@e AND m.institution_id<>@a", ("@e", created.Email), ("@a", _a.Id)));
        // No upper bound on admins: four now exist in A.
        Assert.Equal(3, await Count("SELECT COUNT(*) FROM institution_membership m JOIN institution_membership_role r ON r.membership_id=m.id AND r.role='InstitutionAdmin' WHERE m.institution_id=@a", ("@a", _a.Id)));
    }

    private static string Neutral(string body)
    {
        var json = JsonNode.Parse(body)!.AsObject();
        json.Remove("traceId");
        return json.ToJsonString();
    }

    private static string[] Shape(string body) => JsonNode.Parse(body)!.AsObject().Select(p => p.Key + ":" + (p.Value is null ? "null" : p.Value.GetValueKind().ToString())).Order(StringComparer.Ordinal).ToArray();

    [Fact]
    public async Task HomonymsReceiveIncreasingSuffixesAndAnAddressIsNeverReissued()
    {
        var last = Unique("Rossi");
        var first = await Created(_a.Admin, Body(new[] { "Teacher" }, last: last));
        var second = await Created(_b.Admin, Body(new[] { "Teacher" }, last: last));
        var third = await Created(_a.Admin, Body(new[] { "Student" }, last: last));
        Assert.Equal(new[] { 1, 2, 3 }.Select(n => PlatformIdentityTestConfig.Address("Mario", last, n)), new[] { first.Email, second.Email, third.Email });
        Assert.Equal(3, new[] { first.UserId, second.UserId, third.UserId }.Distinct().Count());
        // Parallel homonyms in two institutions still obtain distinct addresses.
        var parallelLast = Unique("Bianchi");
        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(i => Created(i % 2 == 0 ? _a.Admin : _b.Admin, Body(new[] { "Teacher" }, first: "Anna", last: parallelLast))));
        Assert.Equal(Enumerable.Range(1, 6).Select(n => PlatformIdentityTestConfig.Address("Anna", parallelLast, n)).Order(), results.Select(r => r.Email).Order());
        Assert.Equal(6, await Count("SELECT COUNT(*) FROM institution_email_reservation WHERE user_id IS NOT NULL AND address LIKE @p", ("@p", PlatformEmailRules.Base("Anna", parallelLast) + "%")));
    }

    [Fact]
    public async Task PersonAlreadyActiveElsewhereGetsTheSameShapeWithoutPasswordAndNothingOfTheOtherInstitution()
    {
        var fiscalCode = PlatformIdentityTestConfig.NewFiscalCode();
        var last = Unique("Ferri");
        var inA = await Created(_a.Admin, Body(new[] { "Teacher" }, first: "Paola", last: last, fiscalCode: fiscalCode));
        var accessA = await Activate(_host, inA.Email, inA.TemporaryPassword!, "PaolaPassword!1", _a.Id);
        using var existingResponse = await Send(Accounts, _b.Admin, Body(new[] { "Student" }, first: "Paola", last: last, fiscalCode: fiscalCode.ToLowerInvariant()), "POST");
        Assert.Equal(HttpStatusCode.Created, existingResponse.StatusCode);
        var existingBody = await existingResponse.Content.ReadAsStringAsync();
        var inB = JsonSerializer.Deserialize<InstitutionAccountCreated>(existingBody, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        // Same person, same platform address, no new User and the credential is untouched (D-A).
        Assert.Equal(inA.UserId, inB.UserId);
        Assert.Equal(inA.Email, inB.Email);
        Assert.Null(inB.TemporaryPassword);
        Assert.NotNull(inB.StudentId);
        Assert.Equal(1, await Count("SELECT COUNT(*) FROM `user` WHERE fiscal_code_hmac=(SELECT fiscal_code_hmac FROM `user` WHERE id=@u)", ("@u", inA.UserId)));
        Assert.NotNull(await Login(_host, inA.Email, "PaolaPassword!1"));
        Assert.Equal(HttpStatusCode.OK, (await Send("/api/Auth/context", accessA)).StatusCode);
        // Nothing of A is disclosed to B: neither its id, its membership, roles nor profiles.
        foreach (var secret in new[] { _a.Id.ToString(), inA.MembershipId.ToString(), inA.TeacherId!.Value.ToString() })
            Assert.DoesNotContain(secret, existingBody, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(new[] { "Student" }, inB.Roles);
        Assert.Null(inB.TeacherId);
        // Same response shape as a brand-new person (password present there).
        using var freshResponse = await Send(Accounts, _b.Admin, Body(new[] { "Student" }, first: "Paola", last: Unique("Ferri")), "POST");
        Assert.Equal(HttpStatusCode.Created, freshResponse.StatusCode);
        var freshShape = Shape(await freshResponse.Content.ReadAsStringAsync()).Select(k => k.Replace("temporaryPassword:String", "temporaryPassword:null")).ToArray();
        Assert.Equal(freshShape, Shape(existingBody));
        Assert.Contains("\"temporaryPassword\":null", existingBody);
        // E1: the same person again in the same institution is a neutral conflict.
        var before = await Digest();
        using var again = await Send(Accounts, _b.Admin, Body(new[] { "Teacher" }, first: "Paola", last: last, fiscalCode: fiscalCode), "POST");
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.DoesNotContain(_a.Id.ToString(), await again.Content.ReadAsStringAsync());
        Assert.Equal(before, await Digest());
        // Fiscal codes never appear in plaintext in the database, audit or logs.
        Assert.Equal(0, await Count("SELECT COUNT(*) FROM institution_onboarding_event WHERE LOCATE(@c,CAST(result_json AS CHAR))>0 OR LOCATE(@c,reason)>0", ("@c", fiscalCode)));
        Assert.Equal(0, await Count("SELECT COUNT(*) FROM `user` WHERE LOCATE(@c,CAST(fiscal_code_enc AS CHAR))>0 OR fiscal_code_hmac=@c", ("@c", fiscalCode)));
        Assert.DoesNotContain(_logs, line => line.Contains(fiscalCode, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExistingPersonAndNewPersonCostTheSameAndFailuresRevealNothing()
    {
        var existing = PlatformIdentityTestConfig.NewFiscalCode();
        await Created(_b.Admin, Body(new[] { "Staff" }, fiscalCode: existing));
        async Task<double> Median(Func<Task<HttpResponseMessage>> call)
        {
            var samples = new List<double>();
            for (var i = -1; i < 7; i++) { var t = Stopwatch.StartNew(); using var r = await call(); Assert.True(r.IsSuccessStatusCode || r.StatusCode == HttpStatusCode.Conflict, r.StatusCode.ToString()); if (i >= 0) samples.Add(t.Elapsed.TotalMilliseconds); }
            return samples.Order().ElementAt(samples.Count / 2);
        }
        // Repeated creation of an existing person answers 201 once, then E1 conflicts; both
        // hash before any database access, exactly like a new person.
        var known = await Median(() => Send(Accounts, _a.Admin, Body(new[] { "Staff" }, fiscalCode: existing), "POST"));
        var fresh = await Median(() => Send(Accounts, _a.Admin, Body(new[] { "Staff" }), "POST"));
        Assert.True(Math.Min(known, fresh) * 2 >= Math.Max(known, fresh), $"Creation timing differs: existing {known:F1} ms, fresh {fresh:F1} ms");
        var bodies = new List<string>();
        foreach (var token in new[] { _a.Admin, _a.Admin })
        {
            using var response = await Send(Accounts, token, Body(new[] { "Staff" }, fiscalCode: existing), "POST");
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            bodies.Add(Neutral(await response.Content.ReadAsStringAsync()));
        }
        Assert.All(bodies, b => Assert.Equal(bodies[0], b));
        Assert.DoesNotContain(_b.Id.ToString(), bodies[0]);
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
        var eventId = Guid.NewGuid();
        var body = Body(new[] { "Teacher" }, eventId);
        var created = await Created(_a.Admin, body);
        var password = created.TemporaryPassword!;
        Assert.False(created.Replayed);
        using var replay = await Send(Accounts, _a.Admin, body, "POST");
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayed = (await replay.Content.ReadFromJsonAsync<InstitutionAccountCreated>())!;
        Assert.True(replayed.Replayed); Assert.Null(replayed.TemporaryPassword);
        Assert.Equal(created.UserId, replayed.UserId); Assert.Equal(created.MembershipId, replayed.MembershipId); Assert.Equal(created.Email, replayed.Email);
        Assert.DoesNotContain(password, await replay.Content.ReadAsStringAsync());
        // Same EventId with another intent (here another fiscal code) is a conflict.
        Assert.Equal(HttpStatusCode.Conflict, (await Send(Accounts, _a.Admin, Body(new[] { "Teacher" }, eventId), "POST")).StatusCode);
        Assert.Equal(0, await Count("SELECT COUNT(*) FROM institution_onboarding_event WHERE LOCATE(@p,CAST(result_json AS CHAR))>0 OR LOCATE(@p,reason)>0 OR LOCATE(@p,intent_hash)>0", ("@p", password)));
        Assert.Equal(0, await Count("SELECT COUNT(*) FROM institution_onboarding_event WHERE id=@e AND LOCATE(@m,CAST(result_json AS CHAR))>0", ("@e", eventId), ("@m", created.Email)));
        Assert.Equal(0, await Count("SELECT COUNT(*) FROM identity_account_event WHERE LOCATE(@p,action)>0", ("@p", password)));
        Assert.Equal(0, await Count("SELECT COUNT(*) FROM `user` WHERE password_hash=@p", ("@p", password)));
        Assert.Equal(1, await Count("SELECT COUNT(*) FROM institution_onboarding_event WHERE id=@e AND actor_kind='Member' AND action='AccountCreated'", ("@e", eventId)));
        Assert.NotEmpty(_logs);
        Assert.DoesNotContain(_logs, line => line.Contains(password, StringComparison.Ordinal));
        // Until changed, the new account can only replace its password.
        var ticket = (await Login(_host, created.Email, password))!;
        Assert.True(ticket.PasswordChangeRequired);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send("/api/Auth/institutions", ticket.Ticket)).StatusCode);
    }

    [Fact]
    public async Task TokensNeverEnterAnotherInstitutionAndResetRevokesThem()
    {
        var created = await Created(_a.Admin, Body(new[] { "Teacher" }));
        var access = await Activate(_host, created.Email, created.TemporaryPassword!, "TeacherPassword!1", _a.Id);
        var ticket = (await Login(_host, created.Email, "TeacherPassword!1"))!.Ticket;
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
        Assert.Equal(0, await OwnerProvisioningCli.RunAsync(new[] { "reset-password", "--email", created.Email, "--expected-server-uuid", _uuid }, output, error, v => v == OwnerProvisioningCli.ConnectionVariable ? fixture.ConnectionString : null));
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send("/api/Auth/context", access)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send("/api/Auth/institutions", ticket)).StatusCode);
        Assert.Null(await Login(_host, created.Email, "TeacherPassword!1"));
    }

    [Fact]
    public async Task FailureMidTransactionLeavesNoPartialData()
    {
        await using var host = await Start(fault: true);
        var token = (await Login(host, _a.Email, "AdminPassword!1"))!.Ticket;
        var choice = Assert.Single((await (await Send(host, "/api/Auth/institutions", token)).Content.ReadFromJsonAsync<InstitutionChoiceDto[]>())!);
        using var selected = await Send(host, "/api/Auth/select-institution", token, new SelectInstitutionDto { InstitutionId = choice.InstitutionId, MembershipId = choice.MembershipId }, "POST");
        var access = (await selected.Content.ReadFromJsonAsync<InstitutionAccessDto>())!.Token;
        var before = await Digest(); var last = Unique("Fault"); var fiscalCode = PlatformIdentityTestConfig.NewFiscalCode();
        foreach (var roles in new[] { new[] { "Student" }, new[] { "Teacher" }, new[] { "InstitutionAdmin" } })
            Assert.Equal(HttpStatusCode.InternalServerError, (await Send(host, Accounts, access, Body(roles, last: last, fiscalCode: fiscalCode), "POST")).StatusCode);
        Assert.Equal(before, await Digest());
        Assert.Equal(0, await Count("SELECT COUNT(*) FROM institution_email_reservation WHERE address LIKE @p", ("@p", PlatformEmailRules.Base("Mario", last) + "%")));
        // The same person and address are still available once the fault is gone.
        var created = await Created(_a.Admin, Body(new[] { "Teacher" }, last: last, fiscalCode: fiscalCode));
        Assert.NotNull(created.TemporaryPassword);
        Assert.Equal(PlatformIdentityTestConfig.Address("Mario", last), created.Email);
    }

    [Fact]
    public async Task EndpointIsOffWithoutTheMembershipWriteOrPlatformIdentityFlag()
    {
        foreach (var (write, platform) in new[] { (false, true), (true, false) })
        {
            await using var host = await Start(write: write, platform: platform);
            var token = (await Login(host, _a.Email, "AdminPassword!1"))!.Ticket;
            var choice = Assert.Single((await (await Send(host, "/api/Auth/institutions", token)).Content.ReadFromJsonAsync<InstitutionChoiceDto[]>())!);
            using var selected = await Send(host, "/api/Auth/select-institution", token, new SelectInstitutionDto { InstitutionId = choice.InstitutionId, MembershipId = choice.MembershipId }, "POST");
            var access = (await selected.Content.ReadFromJsonAsync<InstitutionAccessDto>())!.Token;
            var before = await Digest();
            Assert.Equal(HttpStatusCode.NotFound, (await Send(host, Accounts, access, Body(new[] { "Teacher" }), "POST")).StatusCode);
            Assert.Equal(before, await Digest());
        }
    }
}
