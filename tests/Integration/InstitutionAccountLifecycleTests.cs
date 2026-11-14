using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.DTOs.Import;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Policies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MySqlConnector;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

// Account lifecycle (Active/Closed), D-C reactivation, closed-account login, startup key
// validation, retired institution email policies and the E4 platform-account import.
public sealed partial class InstitutionAccountProvisioningTests
{
    private const string Imports = "/api/institution/imports";
    private readonly ConcurrentDictionary<Guid, bool> _lifecycleUsers = new();

    private Task<Mt3cHosting.ProgramHost> StartPlatform(string[]? extra = null) =>
        Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString, Guid.NewGuid(), Array.Empty<Guid>(), enabled: false, school: true, membershipAdmin: true, membershipWrite: true,
            configureServices: services =>
            {
                var original = services.Last(d => d.ServiceType == typeof(IPlatformLifecycleRepository));
                services.Remove(original);
                services.AddScoped<IPlatformLifecycleRepository>(sp => new LifecycleCandidateFilter((IPlatformLifecycleRepository)ActivatorUtilities.CreateInstance(sp, original.ImplementationType!), _lifecycleUsers));
            },
            extraArguments: new[] { "--Multitenancy:InstitutionImport:Enabled=true", "--Multitenancy:InstitutionEmail:Enabled=true" }.Concat(PlatformIdentityTestConfig.Arguments()).Concat(extra ?? []).ToArray());

    private static async Task<LifecyclePassResult> Pass(Mt3cHosting.ProgramHost host)
    {
        using var scope = host.Host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IPlatformLifecycleManager>().RunPassAsync(default);
    }

    private async Task<string> State(Guid user)
    {
        await using var c = new MySqlConnection(fixture.ConnectionString); await c.OpenAsync();
        await using var cmd = new MySqlCommand("SELECT lifecycle_state FROM `user` WHERE id=@u", c); cmd.Parameters.AddWithValue("@u", user);
        return (string)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task Sql(string sql, params (string, object)[] values)
    {
        await using var c = new MySqlConnection(fixture.ConnectionString); await c.OpenAsync();
        await using var cmd = new MySqlCommand(sql, c);
        foreach (var (n, v) in values) cmd.Parameters.AddWithValue(n, v);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task Mutate(Mt3cHosting.ProgramHost host, string admin, Guid membership, string action)
    {
        var revision = await Count("SELECT security_generation FROM institution_membership_security_state WHERE membership_id=@m", ("@m", membership));
        using var response = await Send(host, "/api/institution/admin/members/" + membership + "/mutations", admin, new { EventId = Guid.NewGuid(), ExpectedRevision = revision, Action = action, Reason = "Lifecycle test" }, "POST");
        Assert.True(response.StatusCode == HttpStatusCode.OK, action + " " + response.StatusCode + " " + await response.Content.ReadAsStringAsync());
    }

    private async Task<(InstitutionAccountCreated Account, string Access)> Person(Mt3cHosting.ProgramHost host, string admin, Guid institution, string role, string? fiscalCode = null, string? last = null)
    {
        var created = await Created(admin, Body(new[] { role }, last: last, fiscalCode: fiscalCode));
        _lifecycleUsers[created.UserId] = true;
        return (created, await Activate(host, created.Email, created.TemporaryPassword!, "LifecyclePassword!1", institution));
    }

    [Fact]
    public async Task TeacherWithoutActiveMembershipIsClosedAtTheFirstPassOnceAndReopensWhenReactivated()
    {
        await using var host = await StartPlatform();
        var (teacher, access) = await Person(host, _a.Admin, _a.Id, "Teacher");
        var generation = await Count("SELECT generation FROM identity_account_state WHERE user_id=@u", ("@u", teacher.UserId));
        Assert.Equal(0, (await Pass(host)).Closed);
        await Mutate(host, _a.Admin, teacher.MembershipId, "Suspend");
        var first = await Pass(host);
        Assert.Equal(1, first.Closed); Assert.Equal(0, first.Failed);
        Assert.Equal("Closed", await State(teacher.UserId));
        // Idempotent: a second pass changes nothing and no event is duplicated.
        Assert.Equal(0, (await Pass(host)).Closed);
        Assert.Equal(1, await Count("SELECT COUNT(*) FROM identity_lifecycle_event WHERE user_id=@u AND transition='Closed' AND rule_code='NoActiveMembership'", ("@u", teacher.UserId)));
        Assert.Equal(generation + 1, await Count("SELECT generation FROM identity_account_state WHERE user_id=@u", ("@u", teacher.UserId)));
        // Closed: every earlier token is revoked and login looks exactly like an unknown address.
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(host, "/api/Auth/context", access)).StatusCode);
        using var closedLogin = await Send(host, "/api/Auth/identity/login", body: new LoginDto { Email = teacher.Email, Password = "LifecyclePassword!1" }, method: "POST");
        using var unknownLogin = await Send(host, "/api/Auth/identity/login", body: new LoginDto { Email = "nobody." + Unique("x") + "@platform.example.test", Password = "LifecyclePassword!1" }, method: "POST");
        Assert.Equal(unknownLogin.StatusCode, closedLogin.StatusCode);
        Assert.Equal(Neutral(await unknownLogin.Content.ReadAsStringAsync()), Neutral(await closedLogin.Content.ReadAsStringAsync()));
        // The address stays reserved: a homonym gets the next suffix, never the closed address.
        Assert.Equal(1, await Count("SELECT COUNT(*) FROM institution_email_reservation WHERE user_id=@u AND address=@e", ("@u", teacher.UserId), ("@e", teacher.Email)));
        // D-C: the membership active again reopens the account in the same transaction.
        await Mutate(host, _a.Admin, teacher.MembershipId, "Reactivate");
        Assert.Equal("Active", await State(teacher.UserId));
        Assert.Equal(1, await Count("SELECT COUNT(*) FROM identity_lifecycle_event WHERE user_id=@u AND transition='Reactivated' AND rule_code='MembershipReactivated'", ("@u", teacher.UserId)));
        Assert.Equal(generation + 2, await Count("SELECT generation FROM identity_account_state WHERE user_id=@u", ("@u", teacher.UserId)));
        Assert.NotNull(await Login(host, teacher.Email, "LifecyclePassword!1"));
        Assert.Equal(0, (await Pass(host)).Reactivated);
    }

    [Fact]
    public async Task ClosedAccountsLoginCostsTheSameAsUnknownAddresses()
    {
        await using var host = await StartPlatform();
        var (teacher, _) = await Person(host, _a.Admin, _a.Id, "Teacher");
        await Mutate(host, _a.Admin, teacher.MembershipId, "Suspend");
        Assert.Equal(1, (await Pass(host)).Closed);
        async Task<double> Median(string email)
        {
            var samples = new List<double>();
            for (var i = -1; i < 7; i++)
            {
                var t = Stopwatch.StartNew();
                using var r = await Send(host, "/api/Auth/identity/login", body: new LoginDto { Email = email, Password = "LifecyclePassword!1" }, method: "POST");
                Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
                if (i >= 0) samples.Add(t.Elapsed.TotalMilliseconds);
            }
            return samples.Order().ElementAt(samples.Count / 2);
        }
        var closed = await Median(teacher.Email);
        var unknown = await Median("nobody." + Unique("y") + "@platform.example.test");
        Assert.True(Math.Min(closed, unknown) * 2 >= Math.Max(closed, unknown), $"Login timing differs: closed {closed:F1} ms, unknown {unknown:F1} ms");
    }

    [Fact]
    public async Task StudentsCloseOnlyAfterThePeriodAndActiveStudentsStayOpen()
    {
        await using var host = await StartPlatform();
        var (recent, _) = await Person(host, _a.Admin, _a.Id, "Student");
        var (finished, _) = await Person(host, _a.Admin, _a.Id, "Student");
        var (repeating, _) = await Person(host, _a.Admin, _a.Id, "Student");
        await Mutate(host, _a.Admin, recent.MembershipId, "Suspend");
        await Mutate(host, _a.Admin, finished.MembershipId, "Suspend");
        await Sql("UPDATE institution_membership SET joined_at=UTC_TIMESTAMP(6)-INTERVAL 37 MONTH WHERE id IN (@f,@r)", ("@f", finished.MembershipId), ("@r", repeating.MembershipId));
        var pass = await Pass(host);
        Assert.Equal(1, pass.Closed);
        Assert.Equal("Active", await State(recent.UserId));
        Assert.Equal("Closed", await State(finished.UserId));
        Assert.Equal("Active", await State(repeating.UserId));
        Assert.Equal(1, await Count("SELECT COUNT(*) FROM identity_lifecycle_event WHERE user_id=@u AND rule_code='StudentPeriodElapsed'", ("@u", finished.UserId)));
    }

    [Fact]
    public async Task ClosedPersonIsReactivatedByAnotherInstitutionWithTheSameFiscalCode()
    {
        await using var host = await StartPlatform();
        var fiscalCode = PlatformIdentityTestConfig.NewFiscalCode();
        var (teacher, _) = await Person(host, _a.Admin, _a.Id, "Teacher", fiscalCode);
        await Mutate(host, _a.Admin, teacher.MembershipId, "Suspend");
        Assert.Equal(1, (await Pass(host)).Closed);
        var again = await Created(_b.Admin, Body(new[] { "Student" }, fiscalCode: fiscalCode));
        Assert.Equal(teacher.UserId, again.UserId);
        Assert.Equal(teacher.Email, again.Email);
        Assert.NotNull(again.TemporaryPassword);
        Assert.Equal("Active", await State(teacher.UserId));
        Assert.Null(await Login(host, teacher.Email, "LifecyclePassword!1"));
        Assert.True((await Login(host, teacher.Email, again.TemporaryPassword!))!.PasswordChangeRequired);
        Assert.Equal(1, await Count("SELECT COUNT(*) FROM identity_lifecycle_event WHERE user_id=@u AND transition='Reactivated' AND rule_code='AccountRecreated'", ("@u", teacher.UserId)));
        // Its suspended membership in A is untouched and still not usable.
        Assert.Equal(1, await Count("SELECT COUNT(*) FROM institution_membership WHERE id=@m AND status='suspended'", ("@m", teacher.MembershipId)));
    }

    [Fact]
    public async Task SafetyNetReactivatesClosedAccountsWithAnActiveMembership()
    {
        await using var host = await StartPlatform();
        var (teacher, _) = await Person(host, _a.Admin, _a.Id, "Teacher");
        await Mutate(host, _a.Admin, teacher.MembershipId, "Suspend");
        Assert.Equal(1, (await Pass(host)).Closed);
        // A writer outside the IAM workflow (no in-transaction hook) makes the membership active.
        await Sql("UPDATE institution_membership SET status='active',suspended_at=NULL WHERE id=@m", ("@m", teacher.MembershipId));
        var pass = await Pass(host);
        Assert.Equal(1, pass.Reactivated);
        Assert.Equal("Active", await State(teacher.UserId));
        Assert.Equal(1, await Count("SELECT COUNT(*) FROM identity_lifecycle_event WHERE user_id=@u AND rule_code='ActiveMembershipFound'", ("@u", teacher.UserId)));
        Assert.NotNull(await Login(host, teacher.Email, "LifecyclePassword!1"));
    }

    [Fact]
    public async Task OwnerProvisionedAdminWithoutFiscalCodeFollowsTheSameLifecycle()
    {
        await using var host = await StartPlatform();
        var c = await Institution("C");
        var firstAdmin = (await Count("SELECT COUNT(*) FROM `user` WHERE email=@e AND fiscal_code_hmac IS NULL", ("@e", c.Email)));
        Assert.Equal(1, firstAdmin);
        var user = Guid.Parse((await Scalar("SELECT id FROM `user` WHERE email=@e", ("@e", c.Email)))!);
        var membership = Guid.Parse((await Scalar("SELECT id FROM institution_membership WHERE user_id=@u", ("@u", user)))!);
        _lifecycleUsers[user] = true;
        var second = await Created(c.Admin, Body(new[] { "InstitutionAdmin" }));
        var secondAccess = await Activate(host, second.Email, second.TemporaryPassword!, "SecondAdmin!1", c.Id);
        await Mutate(host, secondAccess, membership, "Suspend");
        Assert.Equal(1, (await Pass(host)).Closed);
        Assert.Equal("Closed", await State(user));
        Assert.Null(await Login(host, c.Email, "AdminPassword!1"));
        // The manually chosen address is retained and immutable like every platform address.
        Assert.Equal(1, await Count("SELECT COUNT(*) FROM institution_email_reservation WHERE user_id=@u AND address=LOWER(@e)", ("@u", user), ("@e", c.Email)));
        await Mutate(host, secondAccess, membership, "Reactivate");
        Assert.Equal("Active", await State(user));
        Assert.NotNull(await Login(host, c.Email, "AdminPassword!1"));
    }

    private async Task<string?> Scalar(string sql, params (string, object)[] values)
    {
        await using var c = new MySqlConnection(fixture.ConnectionString); await c.OpenAsync();
        await using var cmd = new MySqlCommand(sql, c);
        foreach (var (n, v) in values) cmd.Parameters.AddWithValue(n, v);
        return Convert.ToString(await cmd.ExecuteScalarAsync());
    }

    [Fact]
    public async Task WorkerIsRegisteredAndTheHostStopsCleanly()
    {
        await using (var host = await StartPlatform())
            Assert.Contains(host.Host.Services.GetServices<IHostedService>(), s => s.GetType().Name == "PlatformLifecycleWorker");
        // Disposal above asserted ApplicationStopped; a disabled flag leaves the worker idle.
        await using var disabled = await Start(platform: false);
        Assert.Contains(disabled.Host.Services.GetServices<IHostedService>(), s => s.GetType().Name == "PlatformLifecycleWorker");
    }

    [Theory]
    [InlineData("FiscalCodeHmacKey", "")]
    [InlineData("FiscalCodeHmacKey", "c2hvcnQta2V5")]
    [InlineData("FiscalCodeEncryptionKey", "bm90LWJhc2U2NA==!")]
    [InlineData("FiscalCodeEncryptionKey", "MTIzNDU2Nzg5MDEyMzQ1Ng==")]
    [InlineData("EmailDomain", "")]
    [InlineData("StudentClosureAfterMonths", "0")]
    [InlineData("LifecycleInterval", "00:00:01")]
    public async Task StartupIsRefusedWithTheFlagEnabledAndMissingOrInvalidConfiguration(string key, string value)
    {
        var error = await Assert.ThrowsAnyAsync<Exception>(() => StartPlatform(new[] { "--Multitenancy:PlatformIdentity:" + key + "=" + value }));
        var message = error.GetBaseException().Message;
        Assert.Contains("Platform identity configuration", message);
        Assert.Contains(key, message);
        if (value.Length > 4)
            Assert.DoesNotContain(value, message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InstitutionEmailPolicyWritesAreNoLongerOperative()
    {
        await using var host = await StartPlatform();
        using var write = await Send(host, "/api/institution/email-policies/Student", _a.Admin, new { expectedRevision = 0 }, "POST");
        Assert.True(write.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.BadRequest, write.StatusCode.ToString());
        using var reserve = await Send(host, "/api/institution/email-assignments/create-person", _a.Admin, new { eventId = Guid.NewGuid(), category = "Student", policyRevision = 1, firstName = "A", lastName = "B" }, "POST");
        Assert.True(reserve.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.BadRequest, reserve.StatusCode.ToString());
        Assert.Equal(0, await Count("SELECT COUNT(*) FROM institution_email_policy WHERE institution_id=@a", ("@a", _a.Id)));
    }

    // ---- E4: platform-account import -------------------------------------------------

    private static async Task<HttpResponseMessage> Upload(Mt3cHosting.ProgramHost host, string token, string csv, ImportPreviewSettings settings)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(csv)), "file", "students.csv");
        form.Add(new StringContent(JsonSerializer.Serialize(settings)), "settings");
        using var request = new HttpRequestMessage(HttpMethod.Post, Imports + "/preview") { Content = form };
        request.Headers.Authorization = new("Bearer", token);
        return await host.Client.SendAsync(request);
    }

    private static readonly ImportPreviewSettings AccountImport = new(ImportCategory.Student, ImportEmailMode.CreatePlatformAccounts);

    private static async Task<ImportBatch> Preview(Mt3cHosting.ProgramHost host, string token, string csv)
    {
        using var response = await Upload(host, token, csv, AccountImport);
        Assert.True(response.StatusCode == HttpStatusCode.Created, response.StatusCode + " " + await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ImportBatch>())!;
    }

    private static Task<HttpResponseMessage> Confirm(Mt3cHosting.ProgramHost host, string token, ImportBatch batch, Guid? eventId = null) =>
        Send(host, Imports + "/" + batch.Id + "/confirm", token, new ImportConfirmation(eventId ?? Guid.NewGuid(), batch.Revision, batch.PreviewDigest, true, false, null), "POST");

    [Fact]
    public async Task ImportOf600StudentsCreatesLinksAndReactivatesAccountsInOneTransaction()
    {
        // 600 BCrypt hashes take about a minute of CPU: the client must outlast a small runner.
        Mt3cHosting.ClientTimeout.Value = TimeSpan.FromMinutes(3);
        await using var host = await StartPlatform();
        // People already on the platform through B: two active, one closed.
        var activeCodes = new[] { PlatformIdentityTestConfig.NewFiscalCode(), PlatformIdentityTestConfig.NewFiscalCode() };
        var active = new List<InstitutionAccountCreated>();
        foreach (var code in activeCodes)
            active.Add((await Person(host, _b.Admin, _b.Id, "Teacher", code)).Account);
        var closedCode = PlatformIdentityTestConfig.NewFiscalCode();
        var (closed, _) = await Person(host, _b.Admin, _b.Id, "Teacher", closedCode);
        await Mutate(host, _b.Admin, closed.MembershipId, "Suspend");
        Assert.Equal(1, (await Pass(host)).Closed);
        // 600 rows: 300 homonym pairs (deterministic suffixes in row order) and the 3 existing people.
        var tag = Unique("Imp");
        var csv = new StringBuilder("Nome;Cognome;CodiceFiscale;RiferimentoEsterno\n");
        var codes = new string[600];
        for (var i = 0; i < 600; i++)
        {
            codes[i] = i switch { 10 => activeCodes[0], 20 => activeCodes[1], 30 => closedCode, _ => PlatformIdentityTestConfig.NewFiscalCode() };
            csv.Append("Studente;").Append(tag).Append(Pair(i / 2)).Append(';').Append(i % 7 == 0 ? codes[i].ToLowerInvariant() : codes[i]).Append(';').Append(tag).Append('-').Append(i).Append('\n');
        }
        var before = await Count("SELECT COUNT(*) FROM institution_membership WHERE institution_id=@a", ("@a", _a.Id));
        var preview = Stopwatch.StartNew();
        var batch = await Preview(host, _a.Admin, csv.ToString());
        preview.Stop();
        Assert.Equal(600, batch.TotalRows); Assert.Equal(0, batch.InvalidRows);
        // The staged payload never contains a fiscal code in clear.
        foreach (var code in codes.Take(50))
            Assert.Equal(0, await Count("SELECT COUNT(*) FROM institution_import_batch WHERE id=@b AND (LOCATE(@c,payload)>0 OR LOCATE(LOWER(@c),payload)>0)", ("@b", batch.Id), ("@c", code)));
        var eventId = Guid.NewGuid();
        var confirm = Stopwatch.StartNew();
        using var response = await Confirm(host, _a.Admin, batch, eventId);
        confirm.Stop();
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, response.StatusCode + " " + body[..Math.Min(400, body.Length)]);
        var result = JsonSerializer.Deserialize<ImportResult>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        File.AppendAllText(Path.Combine(Path.GetTempPath(), "er-platform-import-timing.txt"), $"{DateTime.UtcNow:O} rows=600 preview_ms={preview.ElapsedMilliseconds} confirm_ms={confirm.ElapsedMilliseconds}{Environment.NewLine}");
        Assert.Equal(600, result.CreatedCount);
        var accounts = result.Accounts!.OrderBy(a => a.RowNumber).ToArray();
        Assert.Equal(600, accounts.Length);
        Assert.Equal(before + 600, await Count("SELECT COUNT(*) FROM institution_membership WHERE institution_id=@a", ("@a", _a.Id)));
        for (var i = 0; i < 600; i++)
        {
            if (i is 10 or 20 or 30)
                continue;
            var pair = i / 2;
            // The partner of an existing person takes the bare address (the existing one keeps its own).
            var number = i % 2 == 0 || (i - 1) is 10 or 20 or 30 ? 1 : 2;
            Assert.Equal(PlatformIdentityTestConfig.Address("Studente", tag + Pair(pair), number), accounts[i].Email);
            Assert.NotNull(accounts[i].TemporaryPassword);
        }
        // D-A: active people are linked without a new password and keep their address.
        Assert.Equal(active[0].UserId, accounts[10].UserId); Assert.Equal(active[0].Email, accounts[10].Email); Assert.Null(accounts[10].TemporaryPassword);
        Assert.Equal(active[1].UserId, accounts[20].UserId); Assert.Null(accounts[20].TemporaryPassword);
        // The closed person is reactivated with a new temporary password.
        Assert.Equal(closed.UserId, accounts[30].UserId); Assert.NotNull(accounts[30].TemporaryPassword);
        Assert.Equal("Active", await State(closed.UserId));
        Assert.True((await Login(host, accounts[1].Email!, accounts[1].TemporaryPassword!))!.PasswordChangeRequired);
        // The response shape is the same for every row, password included as null.
        var rows = JsonDocument.Parse(body).RootElement.GetProperty("accounts").EnumerateArray().Select(r => string.Join(",", r.EnumerateObject().Select(p => p.Name))).Distinct().ToArray();
        Assert.Single(rows);
        // Durable result and replay: identifiers and current addresses, never passwords.
        Assert.Equal(0, await Count("SELECT COUNT(*) FROM institution_import_batch WHERE id=@b AND (LOCATE(@p,result_json)>0 OR LOCATE(@e,result_json)>0)", ("@b", batch.Id), ("@p", accounts[1].TemporaryPassword!), ("@e", accounts[1].Email!)));
        using var replay = await Confirm(host, _a.Admin, batch, eventId);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayed = (await replay.Content.ReadFromJsonAsync<ImportResult>())!;
        Assert.All(replayed.Accounts!, a => Assert.Null(a.TemporaryPassword));
        Assert.Equal(accounts.Select(a => a.Email), replayed.Accounts!.OrderBy(a => a.RowNumber).Select(a => a.Email));
        Assert.Equal(before + 600, await Count("SELECT COUNT(*) FROM institution_membership WHERE institution_id=@a", ("@a", _a.Id)));
        Assert.True(confirm.Elapsed < TimeSpan.FromSeconds(120), $"Import confirmation took {confirm.Elapsed.TotalSeconds:F1} s");
    }

    private static string Pair(int n) => new(n.ToString("D3").Select(d => (char)('a' + d - '0')).ToArray());

    [Fact]
    public async Task InvalidRowsAreAllReportedAndNothingIsWritten()
    {
        await using var host = await StartPlatform();
        var (existing, _) = await Person(host, _a.Admin, _a.Id, "Teacher");
        var existingCode = PlatformIdentityTestConfig.NewFiscalCode();
        var inA = await Created(_a.Admin, Body(new[] { "Student" }, fiscalCode: existingCode));
        var duplicate = PlatformIdentityTestConfig.NewFiscalCode();
        var tag = Unique("Bad");
        var csv = "Nome;Cognome;CodiceFiscale\n"
            + "Anna;" + tag + "a;" + PlatformIdentityTestConfig.NewFiscalCode() + "\n"   // row 2 valid
            + "Luca;" + tag + "b;\n"                                                    // row 3 missing
            + "Sara;" + tag + "c;RSSMRA80A01H501X\n"                                    // row 4 check character
            + "Paolo;" + tag + "d;" + duplicate + "\n"                                  // row 5 duplicate
            + "Giada;" + tag + "e;" + duplicate + "\n"                                  // row 6 duplicate
            + "Ugo;" + tag + "f;" + existingCode + "\n"                                 // row 7 already in A
            + "李;" + tag + "g;" + PlatformIdentityTestConfig.NewFiscalCode() + "\n"; // row 8 name
        var before = await Digest();
        var batch = await Preview(host, _a.Admin, csv);
        Assert.Equal(6, batch.InvalidRows);
        using var response = await Confirm(host, _a.Admin, batch);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var report = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("rows").EnumerateArray()
            .ToDictionary(r => r.GetProperty("rowNumber").GetInt32(), r => r.GetProperty("codes").EnumerateArray().Select(c => c.GetString()!).ToArray());
        Assert.Equal(new[] { 3, 4, 5, 6, 7, 8 }, report.Keys.Order());
        Assert.Contains("RequiredFieldMissing", report[3]);
        Assert.Contains("InvalidFiscalCode", report[4]);
        Assert.Contains("DuplicateFiscalCodeWithinFile", report[5]); Assert.Contains("DuplicateFiscalCodeWithinFile", report[6]);
        Assert.Contains("FiscalCodeExistsInInstitution", report[7]);
        Assert.Contains("NameNotRepresentable", report[8]);
        Assert.Equal(before, await Digest());
        _ = existing; _ = inA;
    }

    [Fact]
    public async Task EmailColumnsPolicyModeAndTeachersAreRejectedWithClearCodes()
    {
        await using var host = await StartPlatform();
        foreach (var (csv, settings, code) in new (string, ImportPreviewSettings, string)[]
        {
            ("Nome;Cognome;CodiceFiscale;Email\nMario;Rossi;" + PlatformIdentityTestConfig.NewFiscalCode() + ";m@x.it", AccountImport, "EmailColumnNotAllowed"),
            ("Nome;Cognome;CodiceFiscale\nMario;Rossi;" + PlatformIdentityTestConfig.NewFiscalCode(), new(ImportCategory.Teacher, ImportEmailMode.CreatePlatformAccounts), "PlatformAccountsStudentOnly"),
            ("Nome;Cognome\nMario;Rossi", new(ImportCategory.Student, ImportEmailMode.ReserveInstitutionalAddress), "EmailPolicyModeRetired")
        })
        {
            using var response = await Upload(host, _a.Admin, csv, settings);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains(code, await response.Content.ReadAsStringAsync());
        }
        using var template = await Send(host, Imports + "/templates/students.csv?emailMode=CreatePlatformAccounts", _a.Admin);
        Assert.Equal(HttpStatusCode.OK, template.StatusCode);
        Assert.Contains("CodiceFiscale", await template.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ImportAndAConcurrentSingleCreationObtainDistinctAddresses()
    {
        await using var host = await StartPlatform();
        var tag = Unique("Conc");
        var csv = new StringBuilder("Nome;Cognome;CodiceFiscale\n");
        for (var i = 0; i < 200; i++)
            csv.Append("Elena;").Append(tag).Append(';').Append(PlatformIdentityTestConfig.NewFiscalCode()).Append('\n');
        var batch = await Preview(host, _a.Admin, csv.ToString());
        var importTask = Confirm(host, _a.Admin, batch);
        var singles = Enumerable.Range(0, 3).Select(_ => Created(_b.Admin, Body(new[] { "Teacher" }, first: "Elena", last: tag))).ToArray();
        using var imported = await importTask;
        Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
        var accounts = (await imported.Content.ReadFromJsonAsync<ImportResult>())!.Accounts!;
        var all = accounts.Select(a => a.Email!).Concat((await Task.WhenAll(singles)).Select(s => s.Email)).ToArray();
        Assert.Equal(203, all.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(Enumerable.Range(1, 203).Select(n => PlatformIdentityTestConfig.Address("Elena", tag, n)).Order(), all.Order());
    }
}
