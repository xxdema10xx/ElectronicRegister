using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Policies;
using MySqlConnector;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

// A21: a creation that loses a deadlock (or a lock wait) is repeated as a whole; when the
// attempts run out the answer is a 503 PlatformBusy, never the neutral 409 of E1.
public sealed partial class InstitutionAccountProvisioningTests
{
    // Produces a genuine InnoDB deadlock (error 1213) between two throwaway connections and
    // surfaces the server exception from inside the creation transaction, before commit.
    private sealed class RealDeadlockBeforeCommit(string connectionString, int failures) : IOnboardingFaultObserver
    {
        private int _calls;
        private int _remaining = failures;
        public int Calls => Volatile.Read(ref _calls);

        public async Task BeforeCommitAsync(string action, CancellationToken ct)
        {
            if (action != "AccountCreate")
                return;
            Interlocked.Increment(ref _calls);
            if (Interlocked.Decrement(ref _remaining) < 0)
                return;
            throw await Deadlock(connectionString);
        }

        private static async Task<MySqlException> Deadlock(string connectionString)
        {
            await using var setup = new MySqlConnection(connectionString);
            await setup.OpenAsync();
            await Exec(setup, "CREATE TABLE IF NOT EXISTS zz_deadlock_probe(id INT PRIMARY KEY) ENGINE=InnoDB");
            await Exec(setup, "INSERT IGNORE INTO zz_deadlock_probe VALUES (1),(2)");
            await using var first = new MySqlConnection(connectionString);
            await using var second = new MySqlConnection(connectionString);
            await first.OpenAsync();
            await second.OpenAsync();
            await Exec(first, "SET SESSION innodb_lock_wait_timeout=20");
            await Exec(second, "SET SESSION innodb_lock_wait_timeout=20");
            await Exec(first, "START TRANSACTION");
            await Exec(second, "START TRANSACTION");
            await Exec(first, "SELECT id FROM zz_deadlock_probe WHERE id=1 FOR UPDATE");
            await Exec(second, "SELECT id FROM zz_deadlock_probe WHERE id=2 FOR UPDATE");
            var a = Attempt(first, "SELECT id FROM zz_deadlock_probe WHERE id=2 FOR UPDATE");
            await Task.Delay(300);
            var b = Attempt(second, "SELECT id FROM zz_deadlock_probe WHERE id=1 FOR UPDATE");
            var errors = (await Task.WhenAll(a, b)).Where(e => e is not null).ToArray();
            await Exec(first, "ROLLBACK");
            await Exec(second, "ROLLBACK");
            return errors.Length == 1 && errors[0]!.Number == 1213 ? errors[0]! : throw new InvalidOperationException("The probe did not produce exactly one deadlock victim.");
        }

        private static async Task<MySqlException?> Attempt(MySqlConnection connection, string sql)
        {
            try
            {
                await Exec(connection, sql);
                return null;
            }
            catch (MySqlException error)
            {
                return error;
            }
        }

        private static async Task Exec(MySqlConnection connection, string sql)
        {
            await using var command = new MySqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    private async Task<string> AdminAccess(Mt3cHosting.ProgramHost host)
    {
        var ticket = (await Login(host, _a.Email, "AdminPassword!1"))!.Ticket;
        var choice = Assert.Single((await (await Send(host, "/api/Auth/institutions", ticket)).Content.ReadFromJsonAsync<InstitutionChoiceDto[]>())!);
        using var selected = await Send(host, "/api/Auth/select-institution", ticket, new SelectInstitutionDto { InstitutionId = choice.InstitutionId, MembershipId = choice.MembershipId }, "POST");
        return (await selected.Content.ReadFromJsonAsync<InstitutionAccessDto>())!.Token;
    }

    [Fact]
    public async Task ADeadlockVictimIsRetriedTransparentlyAndCreatesOneAccount()
    {
        var observer = new RealDeadlockBeforeCommit(fixture.ConnectionString, failures: 2);
        await using var host = await Start(observer: observer);
        var access = await AdminAccess(host);
        var before = await Digest();
        var last = Unique("Retry");
        using var response = await Send(host, Accounts, access, Body(new[] { "Teacher" }, last: last), "POST");
        Assert.True(response.StatusCode == HttpStatusCode.Created, response.StatusCode + " " + await response.Content.ReadAsStringAsync());
        var created = (await response.Content.ReadFromJsonAsync<InstitutionAccountCreated>())!;
        Assert.Equal(3, observer.Calls);
        Assert.NotNull(created.TemporaryPassword);
        Assert.Equal(PlatformIdentityTestConfig.Address("Mario", last), created.Email);
        Assert.Equal(1, await Count("SELECT COUNT(*) FROM `user` WHERE email=@e", ("@e", created.Email)));
        Assert.Equal(1, await Count("SELECT COUNT(*) FROM institution_email_reservation WHERE address=@e", ("@e", created.Email)));
        Assert.NotEqual(before, await Digest());
    }

    [Fact]
    public async Task ExhaustedAttemptsAnswerBusyDistinctFromTheNeutralConflictAndLeaveNothingBehind()
    {
        var observer = new RealDeadlockBeforeCommit(fixture.ConnectionString, failures: int.MaxValue);
        await using var host = await Start(observer: observer);
        var access = await AdminAccess(host);
        var before = await Digest();
        var last = Unique("Busy");
        var fiscalCode = PlatformIdentityTestConfig.NewFiscalCode();
        using (var busy = await Send(host, Accounts, access, Body(new[] { "Teacher" }, last: last, fiscalCode: fiscalCode), "POST"))
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, busy.StatusCode);
            Assert.NotNull(busy.Headers.RetryAfter);
            var problem = JsonDocument.Parse(await busy.Content.ReadAsStringAsync()).RootElement;
            Assert.Equal("PlatformBusy", problem.GetProperty("code").GetString());
            Assert.DoesNotContain("membership", problem.GetProperty("title").GetString(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("deadlock", problem.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("1213", problem.ToString(), StringComparison.Ordinal);
        }
        Assert.Equal(4, observer.Calls);
        Assert.Equal(before, await Digest());
        Assert.Equal(0, await Count("SELECT COUNT(*) FROM institution_email_reservation WHERE address LIKE @p", ("@p", PlatformEmailRules.Base("Mario", last) + "%")));

        // E1 is another answer: the same person created twice is the neutral 409, not a 503.
        await using var plain = await Start();
        var plainAccess = await AdminAccess(plain);
        using (var created = await Send(plain, Accounts, plainAccess, Body(new[] { "Teacher" }, last: last, fiscalCode: fiscalCode), "POST"))
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var conflict = await Send(plain, Accounts, plainAccess, Body(new[] { "Teacher" }, last: last, fiscalCode: fiscalCode), "POST");
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }
}
