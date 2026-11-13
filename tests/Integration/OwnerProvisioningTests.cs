using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ElectronicRegister.OwnerProvisioning;
using ElectronicRegisterAPI.Domain.DTOs;
using MySqlConnector;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

// Owner provisioning tool against the disposable MySQL 8.4 target, through the same
// entry point the owner runs (OwnerProvisioningCli.RunAsync), then the real API.
[Collection(IntegrationCollection.Name)]
public sealed class OwnerProvisioningTests(IntegrationFixture fixture) : IAsyncLifetime
{
    private Mt3cHosting.ProgramHost _host = null!;
    private string _uuid = "";
    private readonly string _suffix = Guid.NewGuid().ToString("N");
    private string Code => "OWN_" + _suffix[..20];
    private string AdminEmail => "admin." + _suffix + "@owner.example.test";

    public async Task InitializeAsync()
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);
        _uuid = (string)(await Scalar("SELECT @@server_uuid"))!;
        _host = await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString, Guid.NewGuid(), Array.Empty<Guid>(), enabled: false);
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private async Task<object?> Scalar(string sql, params (string, object)[] values)
    {
        await using var c = new MySqlConnection(fixture.ConnectionString); await c.OpenAsync();
        await using var cmd = new MySqlCommand(sql, c);
        foreach (var (n, v) in values) cmd.Parameters.AddWithValue(n, v);
        var value = await cmd.ExecuteScalarAsync();
        return value is DBNull ? null : value;
    }

    private async Task<long> Count(string sql, params (string, object)[] values) => Convert.ToInt64(await Scalar(sql, values));

    private async Task<(int Code, string Out, string Err)> Run(params string[] args)
    {
        using StringWriter output = new(), error = new();
        var code = await OwnerProvisioningCli.RunAsync(args, output, error, name => name == OwnerProvisioningCli.ConnectionVariable ? fixture.ConnectionString : null);
        return (code, output.ToString(), error.ToString());
    }

    private Task<(int Code, string Out, string Err)> Create(string? code = null, string? email = null, string? uuid = null) =>
        Run("create-institution", "--code", code ?? Code, "--name", "Istituto di prova " + _suffix[..6], "--admin-email", email ?? AdminEmail, "--expected-server-uuid", uuid ?? _uuid);

    private static string Password(string output) =>
        output.Split('\n').Select(l => l.Trim()).Single(l => l.StartsWith("temporary password (shown once", StringComparison.Ordinal)).Split(": ", 2)[1];

    private static string Field(string output, string name) => output.Split('\n').Select(l => l.Trim()).Single(l => l.StartsWith(name + ": ", StringComparison.Ordinal))[(name.Length + 2)..];

    private async Task<HttpResponseMessage> Send(string route, string? token = null, object? body = null, string method = "GET")
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), route);
        if (token is not null) request.Headers.Authorization = new("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await _host.Client.SendAsync(request);
    }

    private async Task<IdentityTicketDto?> Login(string email, string password)
    {
        using var response = await Send("/api/Auth/identity/login", body: new LoginDto { Email = email, Password = password }, method: "POST");
        return response.StatusCode == HttpStatusCode.OK ? await response.Content.ReadFromJsonAsync<IdentityTicketDto>() : null;
    }

    private async Task<string> Access(string ticket, Guid institution)
    {
        var choices = await (await Send("/api/Auth/institutions", ticket)).Content.ReadFromJsonAsync<InstitutionChoiceDto[]>();
        var choice = Assert.Single(choices!);
        Assert.Equal(institution, choice.InstitutionId);
        using var response = await Send("/api/Auth/select-institution", ticket, new SelectInstitutionDto { InstitutionId = choice.InstitutionId, MembershipId = choice.MembershipId }, "POST");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<InstitutionAccessDto>())!.Token;
    }

    private async Task<string> Activate(string email, string temporary, string final, Guid institution)
    {
        var ticket = (await Login(email, temporary))!;
        Assert.True(ticket.PasswordChangeRequired);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send("/api/Auth/institutions", ticket.Ticket)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Send("/api/identity/account/password", ticket.Ticket, new ChangeIdentityPasswordDto(temporary, final), "PUT")).StatusCode);
        var fresh = (await Login(email, final))!;
        Assert.False(fresh.PasswordChangeRequired);
        return await Access(fresh.Ticket, institution);
    }

    [Fact]
    public async Task CreateInstitutionProvisionsOneTemporaryAdminWhoMustChangePasswordBeforeAdministering()
    {
        var run = await Create();
        Assert.Equal(0, run.Code); Assert.Equal("", run.Err);
        Assert.Equal("created", Field(run.Out, "status"));
        var institution = Guid.Parse(Field(run.Out, "institution")); var user = Guid.Parse(Field(run.Out, "admin user"));
        var temporary = Password(run.Out);
        Assert.Equal(1, run.Out.Split(temporary).Length - 1);
        // Never stored or audited in clear: only a BCrypt hash, no event or audit row carries it.
        var hash = (string)(await Scalar("SELECT password_hash FROM `user` WHERE id=@u", ("@u", user)))!;
        Assert.NotEqual(temporary, hash); Assert.True(BCrypt.Net.BCrypt.Verify(temporary, hash)); Assert.Equal("11", hash.Split('$')[2]);
        Assert.Equal(0, await Count("SELECT COUNT(*) FROM institution_onboarding_event WHERE institution_id=@i AND (LOCATE(@p,CAST(result_json AS CHAR))>0 OR LOCATE(@p,reason)>0)", ("@i", institution), ("@p", temporary)));
        Assert.Equal(1, await Count("SELECT COUNT(*) FROM institution_onboarding_event WHERE institution_id=@i AND actor_kind='Operator' AND operator_id='owner-script' AND action='OwnerProvision'", ("@i", institution)));
        Assert.Equal("active", await Scalar("SELECT status FROM institution WHERE id=@i", ("@i", institution)));
        Assert.Equal(1, await Count("SELECT COUNT(*) FROM institution_membership m JOIN institution_membership_role r ON r.membership_id=m.id JOIN institution_membership_security_state s ON s.membership_id=m.id WHERE m.institution_id=@i AND m.status='active'", ("@i", institution)));
        Assert.Equal("student", await Scalar("SELECT role FROM `user` WHERE id=@u", ("@u", user))); // legacy column never grants authority
        var access = await Activate(AdminEmail, temporary, "AdminPassword!1", institution);
        var context = (await (await Send("/api/Auth/context", access)).Content.ReadFromJsonAsync<TenantContextDto>())!;
        Assert.Equal(institution, context.InstitutionId);
        Assert.Equal(new[] { "InstitutionAdmin" }, context.Roles);
    }

    [Fact]
    public async Task CreateIsIdempotentNeverShowsThePasswordAgainAndRejectsConflictsWithoutChanges()
    {
        var first = await Create(); Assert.Equal(0, first.Code);
        var temporary = Password(first.Out);
        var before = await Count("SELECT (SELECT COUNT(*) FROM institution)+(SELECT COUNT(*) FROM `user`)+(SELECT COUNT(*) FROM institution_membership)+(SELECT COUNT(*) FROM identity_account_event)+(SELECT COUNT(*) FROM institution_onboarding_event)");
        var again = await Create();
        Assert.Equal(0, again.Code);
        Assert.Equal("already-provisioned", Field(again.Out, "status"));
        Assert.Equal(Field(first.Out, "institution"), Field(again.Out, "institution"));
        Assert.DoesNotContain(temporary, again.Out);
        Assert.Contains("not shown", again.Out);
        var otherAdmin = await Create(email: "other." + _suffix + "@owner.example.test");
        Assert.Equal(4, otherAdmin.Code); Assert.Equal("", otherAdmin.Out);
        var usedEmail = await Create(code: "OWN2_" + _suffix[..19]);
        Assert.Equal(4, usedEmail.Code); Assert.Equal("", usedEmail.Out);
        Assert.Equal(before, await Count("SELECT (SELECT COUNT(*) FROM institution)+(SELECT COUNT(*) FROM `user`)+(SELECT COUNT(*) FROM institution_membership)+(SELECT COUNT(*) FROM identity_account_event)+(SELECT COUNT(*) FROM institution_onboarding_event)"));
        // The temporary password still works: nothing was rotated by the replays.
        Assert.True((await Login(AdminEmail, temporary))!.PasswordChangeRequired);
    }

    [Theory]
    [InlineData("InstitutionAdmin")]
    [InlineData("Teacher")]
    [InlineData("Student")]
    [InlineData("Staff")]
    public async Task ResetPasswordOfAnyRoleRevokesEveryPriorTokenAndRequiresAChange(string role)
    {
        var created = await Create(); var institution = Guid.Parse(Field(created.Out, "institution"));
        var email = AdminEmail; var current = "AdminPassword!1";
        var adminAccess = await Activate(AdminEmail, Password(created.Out), current, institution);
        if (role != "InstitutionAdmin")
        {
            // A member of another role, created directly with a known credential.
            email = role.ToLowerInvariant() + "." + _suffix + "@owner.example.test"; current = "MemberPassword!1";
            await using var c = new MySqlConnection(fixture.ConnectionString); await c.OpenAsync();
            Guid user = Guid.NewGuid(), membership = Guid.NewGuid();
            foreach (var sql in new[]
            {
                "INSERT INTO `user`(id,email,password_hash,role) VALUES(@u,@e,@h,'student')",
                "INSERT INTO identity_account_state(user_id,generation) VALUES(@u,0)",
                "INSERT INTO institution_membership(id,institution_id,user_id,status,joined_at) VALUES(@m,@i,@u,'active',UTC_TIMESTAMP(6))",
                "INSERT INTO institution_membership_role(membership_id,role) VALUES(@m,@r)",
            })
            {
                await using var cmd = new MySqlCommand(sql, c);
                cmd.Parameters.AddWithValue("@u", user); cmd.Parameters.AddWithValue("@e", email); cmd.Parameters.AddWithValue("@h", BCrypt.Net.BCrypt.HashPassword(current, 11));
                cmd.Parameters.AddWithValue("@m", membership); cmd.Parameters.AddWithValue("@i", institution); cmd.Parameters.AddWithValue("@r", role);
                await cmd.ExecuteNonQueryAsync();
            }
        }
        var ticket = (await Login(email, current))!;
        Assert.False(ticket.PasswordChangeRequired);
        var access = await Access(ticket.Ticket, institution);
        Assert.Equal(HttpStatusCode.OK, (await Send("/api/Auth/context", access)).StatusCode);

        var reset = await Run("reset-password", "--email", email, "--expected-server-uuid", _uuid);
        Assert.Equal(0, reset.Code); Assert.Equal("", reset.Err);
        var temporary = Password(reset.Out);
        // Every token issued before the reset is rejected, selection and access alike.
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send("/api/Auth/context", access)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send("/api/Auth/institutions", ticket.Ticket)).StatusCode);
        Assert.Null(await Login(email, current));
        Assert.Equal(0, await Count("SELECT COUNT(*) FROM institution_onboarding_event WHERE institution_id=@i AND (LOCATE(@p,CAST(result_json AS CHAR))>0 OR LOCATE(@p,reason)>0)", ("@i", institution), ("@p", temporary)));
        Assert.Equal(1, await Count("SELECT COUNT(*) FROM institution_onboarding_event WHERE institution_id=@i AND action='OwnerPasswordReset'", ("@i", institution)));
        var final = await Activate(email, temporary, "ResetPassword!2", institution);
        Assert.Equal(HttpStatusCode.OK, (await Send("/api/Auth/context", final)).StatusCode);
        if (role != "InstitutionAdmin") Assert.Equal(HttpStatusCode.OK, (await Send("/api/Auth/context", adminAccess)).StatusCode); // other accounts untouched
    }

    [Fact]
    public async Task TargetGuardRefusesWrongServerRepositorySecretsAndUsageWithoutAnyWrite()
    {
        var before = await Count("SELECT COUNT(*) FROM institution");
        var wrong = await Create(uuid: Guid.NewGuid().ToString());
        Assert.Equal(3, wrong.Code); Assert.Equal("", wrong.Out); Assert.Contains("server_uuid", wrong.Err);
        // AppContext.BaseDirectory is inside this repository: a connection file there is refused before reading it.
        var inside = Path.Combine(AppContext.BaseDirectory, "never-read-connection.txt");
        var repository = await Run("create-institution", "--code", Code, "--name", "X", "--admin-email", AdminEmail, "--expected-server-uuid", _uuid, "--connection-file", inside);
        Assert.Equal(4, repository.Code); Assert.Contains("fuori da qualsiasi repository", repository.Err);
        using (StringWriter output = new(), error = new())
            Assert.Equal(4, await OwnerProvisioningCli.RunAsync(new[] { "reset-password", "--email", AdminEmail, "--expected-server-uuid", _uuid }, output, error, _ => null));
        Assert.Equal(2, (await Run("create-institution", "--code")).Code);
        Assert.Equal(2, (await Run("delete-everything")).Code);
        Assert.Equal(2, (await Run("reset-password", "--email", AdminEmail, "--expected-server-uuid", _uuid, "--code", "X")).Code);
        Assert.Equal(4, (await Run("reset-password", "--email", "absent." + _suffix + "@owner.example.test", "--expected-server-uuid", _uuid)).Code);
        Assert.Equal(before, await Count("SELECT COUNT(*) FROM institution"));
        Assert.DoesNotContain(new MySqlConnectionStringBuilder(fixture.ConnectionString).Password, wrong.Err + repository.Err);
    }

    [Fact]
    public async Task FailureMidTransactionLeavesNoPartialInstitutionAccountOrMembership()
    {
        // Fault after institution, user, identity events, membership and role were written:
        // the audit insert fails, and the whole transaction must disappear.
        var trigger = "zz_owner_fault_" + _suffix[..8];
        await Scalar($"CREATE TRIGGER {trigger} BEFORE INSERT ON institution_onboarding_event FOR EACH ROW BEGIN IF NEW.action='OwnerProvision' AND NEW.reason='fault-{_suffix[..8]}' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='injected owner fault'; END IF; END");
        try
        {
            var run = await Run("create-institution", "--code", Code, "--name", "Fault", "--admin-email", AdminEmail, "--expected-server-uuid", _uuid, "--reason", "fault-" + _suffix[..8]);
            Assert.Equal(5, run.Code); Assert.Equal("", run.Out);
            Assert.Equal(0, await Count("SELECT COUNT(*) FROM institution WHERE code=@c", ("@c", Code)));
            Assert.Equal(0, await Count("SELECT COUNT(*) FROM `user` WHERE email=@e", ("@e", AdminEmail)));
            Assert.Equal(0, await Count("SELECT COUNT(*) FROM institution_email_reservation WHERE address=@e", ("@e", AdminEmail.ToLowerInvariant())));
        }
        finally { await Scalar($"DROP TRIGGER IF EXISTS {trigger}"); }
        Assert.Equal(0, (await Create()).Code); // the same request succeeds once the fault is gone
    }
}
