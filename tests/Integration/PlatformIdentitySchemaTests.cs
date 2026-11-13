using System;
using System.IO;
using System.Threading.Tasks;
using MySqlConnector;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

// er-platform-identity-1 schema (migrations/platform-identity): G0 address retention,
// D-B fixture-only exception, lifecycle ledger and the read-only 002 validation.
[Collection(IntegrationCollection.Name)]
public sealed class PlatformIdentitySchemaTests(IntegrationFixture fixture)
{
    private async Task<MySqlConnection> Plain()
    {
        var c = new MySqlConnection(new MySqlConnectionStringBuilder(fixture.ConnectionString) { AllowUserVariables = true }.ConnectionString);
        await c.OpenAsync();
        return c;
    }

    private static async Task Execute(MySqlConnection c, string sql, params (string Name, object? Value)[] values)
    {
        await using var command = new MySqlCommand(sql, c);
        foreach (var (name, value) in values)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> Count(MySqlConnection c, string sql, params (string Name, object? Value)[] values)
    {
        await using var command = new MySqlCommand(sql, c);
        foreach (var (name, value) in values)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task<int> Rejected(Func<Task> action) => (await Assert.ThrowsAsync<MySqlException>(action)).Number;

    private static async Task<Guid> NewUser(MySqlConnection c, string email)
    {
        var id = Guid.NewGuid();
        await Execute(c, "INSERT INTO `user`(id,email,password_hash,role) VALUES(@u,@e,'not-a-login-hash','student')", ("@u", id), ("@e", email));
        return id;
    }

    [Fact]
    public async Task ValidationScriptReportsZeroViolations()
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);
        await using var c = await Plain();
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "migrations", "platform-identity", "002_validate.sql"))) root = root.Parent;
        var checks = 0;
        foreach (var statement in (await File.ReadAllTextAsync(Path.Combine(root!.FullName, "migrations", "platform-identity", "002_validate.sql"))).Split(";\n", StringSplitOptions.RemoveEmptyEntries))
        {
            if (!statement.TrimStart().StartsWith("SELECT", StringComparison.Ordinal) && !statement.Contains("\nSELECT", StringComparison.Ordinal)) continue;
            await using var command = new MySqlCommand(statement, c);
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.True(reader.GetInt64(1) == 0, reader.GetString(0));
            checks++;
        }
        Assert.Equal(13, checks);
    }

    [Fact]
    public async Task EmailUpdateAndUserDeletionAreRejectedOutsideTheFixtureException()
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);
        await using var c = await Plain();
        var user = await NewUser(c, Guid.NewGuid().ToString("N") + "@platform.example.test");
        // Root without the fixture capability, even naming the user as an owner.
        await Execute(c, "SET @er_mt5b_fixture_owners=@o", ("@o", user.ToString()));
        Assert.Equal(1644, await Rejected(() => Execute(c, "UPDATE `user` SET email=@e WHERE id=@u", ("@e", "moved@platform.example.test"), ("@u", user))));
        Assert.Equal(1644, await Rejected(() => Execute(c, "DELETE FROM `user` WHERE id=@u", ("@u", user))));
        // Case-only changes are changes too: the address is compared byte for byte.
        Assert.Equal(1644, await Rejected(() => Execute(c, "UPDATE `user` SET email=UPPER(email) WHERE id=@u", ("@u", user))));
        // The API principal can never use the exception, not even with the capability.
        await using (var probe = await Plain())
        {
            Assert.True(await Mysql84OwnedSession.PrepareAsync(probe));
            await using var get = new MySqlCommand("SELECT @er_mt5b_fixture_capability", probe);
            var capability = (string)(await get.ExecuteScalarAsync())!;
            await using var api = new MySqlConnection(new MySqlConnectionStringBuilder(fixture.ConnectionString) { UserID = "fixture_api", Password = "synthetic-fixture-only", AllowUserVariables = true }.ConnectionString);
            await api.OpenAsync();
            await Execute(api, "SET @er_mt5b_fixture_owners=@o; SET @er_mt5b_fixture_capability=@cap", ("@o", user.ToString()), ("@cap", capability));
            Assert.Equal(1644, await Rejected(() => Execute(api, "DELETE FROM `user` WHERE id=@u", ("@u", user))));
            Assert.Equal(1644, await Rejected(() => Execute(api, "UPDATE `user` SET email=@e WHERE id=@u", ("@e", "moved@platform.example.test"), ("@u", user))));
        }
        Assert.Equal(1L, await Count(c, "SELECT COUNT(*) FROM institution_email_reservation WHERE user_id=@u AND address=(SELECT LOWER(email) FROM `user` WHERE id=@u)", ("@u", user)));
    }

    [Fact]
    public async Task FixtureExceptionRequiresListedSyntheticUsers()
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);
        await using var c = await Plain();
        var listed = await NewUser(c, Guid.NewGuid().ToString("N") + "@platform.example.test");
        var other = await NewUser(c, Guid.NewGuid().ToString("N") + "@platform.example.test");
        var real = await NewUser(c, Guid.NewGuid().ToString("N") + "@its.example");
        await FixtureIdentityExemption.ExecuteAsync(fixture.ConnectionString, [listed], "UPDATE `user` SET email=@e WHERE id=@u", ("@e", listed.ToString("N") + "@moved.example.test"), ("@u", listed));
        Assert.Equal(1644, await Rejected(() => FixtureIdentityExemption.ExecuteAsync(fixture.ConnectionString, [listed], "UPDATE `user` SET email=@e WHERE id=@u", ("@e", "real@its.example"), ("@u", listed))));
        Assert.Equal(1644, await Rejected(() => FixtureIdentityExemption.ExecuteAsync(fixture.ConnectionString, [listed], "DELETE FROM `user` WHERE id=@u", ("@u", other))));
        Assert.Equal(1644, await Rejected(() => FixtureIdentityExemption.ExecuteAsync(fixture.ConnectionString, [real], "DELETE FROM `user` WHERE id=@u", ("@u", real))));
        await FixtureIdentityExemption.ExecuteAsync(fixture.ConnectionString, [listed], "DELETE FROM `user` WHERE id=@u", ("@u", listed));
        Assert.Equal(0L, await Count(c, "SELECT COUNT(*) FROM `user` WHERE id=@u", ("@u", listed)));
    }

    [Fact]
    public async Task ClosedAccountKeepsItsAddressAndTheLedgerIsAppendOnly()
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);
        await using var c = await Plain();
        var address = "closed." + Guid.NewGuid().ToString("N")[..12] + "@its.example";
        var user = await NewUser(c, address);
        await Execute(c, "INSERT INTO identity_account_state(user_id,generation) VALUES(@u,0)", ("@u", user));
        // A transition needs its timestamp; the event must match state and generation.
        Assert.Equal(1644, await Rejected(() => Execute(c, "UPDATE `user` SET lifecycle_state='Closed' WHERE id=@u", ("@u", user))));
        await Execute(c, "UPDATE `user` SET lifecycle_state='Closed',closed_at_utc=UTC_TIMESTAMP(6) WHERE id=@u", ("@u", user));
        var eventId = Guid.NewGuid();
        Assert.Equal(1644, await Rejected(() => Execute(c, "INSERT INTO identity_lifecycle_event VALUES(@e,@u,'Closed','NoActiveMembership',1,1,UTC_TIMESTAMP(6))", ("@e", eventId), ("@u", user))));
        await Execute(c, "UPDATE identity_account_state SET generation=1 WHERE user_id=@u", ("@u", user));
        Assert.Equal(1644, await Rejected(() => Execute(c, "INSERT INTO identity_lifecycle_event VALUES(@e,@u,'Closed','AccountRecreated',1,1,UTC_TIMESTAMP(6))", ("@e", eventId), ("@u", user))));
        await Execute(c, "INSERT INTO identity_lifecycle_event VALUES(@e,@u,'Closed','NoActiveMembership',1,1,UTC_TIMESTAMP(6))", ("@e", eventId), ("@u", user));
        Assert.Equal(1062, await Rejected(() => Execute(c, "INSERT INTO identity_lifecycle_event VALUES(@e,@u,'Closed','NoActiveMembership',1,1,UTC_TIMESTAMP(6))", ("@e", Guid.NewGuid()), ("@u", user))));
        Assert.Equal(1644, await Rejected(() => Execute(c, "UPDATE identity_lifecycle_event SET rule_version=2 WHERE id=@e", ("@e", eventId))));
        Assert.Equal(1644, await Rejected(() => Execute(c, "DELETE FROM identity_lifecycle_event WHERE id=@e", ("@e", eventId))));
        // The closed account's address is never released or reissued, in any letter case.
        Assert.Equal(1644, await Rejected(() => Execute(c, "DELETE FROM institution_email_reservation WHERE address=@a", ("@a", address))));
        Assert.Equal(1062, await Rejected(() => NewUser(c, address.ToUpperInvariant())));
        Assert.Equal(1L, await Count(c, "SELECT COUNT(*) FROM institution_email_reservation WHERE address=@a AND user_id=@u", ("@a", address), ("@u", user)));
    }

    [Fact]
    public async Task FiscalCodeColumnsAreCompleteUniqueAndImmutable()
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);
        await using var c = await Plain();
        var hmac = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var cipher = System.Security.Cryptography.RandomNumberGenerator.GetBytes(44);
        const string insert = "INSERT INTO `user`(id,email,password_hash,role,fiscal_code_hmac,fiscal_code_hmac_key_version,fiscal_code_enc,fiscal_code_enc_key_version) VALUES(@u,@e,'x','student',@h,@hk,@c,@ck)";
        Assert.Equal(1644, await Rejected(() => Execute(c, insert, ("@u", Guid.NewGuid()), ("@e", Guid.NewGuid().ToString("N") + "@its.example"), ("@h", hmac), ("@hk", "h1"), ("@c", null), ("@ck", "e1"))));
        Assert.Equal(1644, await Rejected(() => Execute(c, insert, ("@u", Guid.NewGuid()), ("@e", Guid.NewGuid().ToString("N") + "@its.example"), ("@h", hmac.ToLowerInvariant()), ("@hk", "h1"), ("@c", cipher), ("@ck", "e1"))));
        var user = Guid.NewGuid();
        await Execute(c, insert, ("@u", user), ("@e", Guid.NewGuid().ToString("N") + "@its.example"), ("@h", hmac), ("@hk", "h1"), ("@c", cipher), ("@ck", "e1"));
        Assert.Equal(1062, await Rejected(() => Execute(c, insert, ("@u", Guid.NewGuid()), ("@e", Guid.NewGuid().ToString("N") + "@its.example"), ("@h", hmac), ("@hk", "h1"), ("@c", cipher), ("@ck", "e1"))));
        Assert.Equal(1644, await Rejected(() => Execute(c, "UPDATE `user` SET fiscal_code_enc_key_version='e2' WHERE id=@u", ("@u", user))));
        var legacy = await NewUser(c, Guid.NewGuid().ToString("N") + "@its.example");
        Assert.Equal(1644, await Rejected(() => Execute(c, "UPDATE `user` SET fiscal_code_hmac=@h,fiscal_code_hmac_key_version='h1',fiscal_code_enc=@c,fiscal_code_enc_key_version='e1' WHERE id=@u",
            ("@h", Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))), ("@c", cipher), ("@u", legacy))));
    }
}
