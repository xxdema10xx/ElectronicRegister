using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MySqlConnector;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Infrastructure.DependencyInjection;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

[Collection("Mt5aIdentityOnly")]
public sealed class Mt5bSecurityStateTests : IAsyncLifetime
{
    private readonly Guid _a = Guid.NewGuid(), _b = Guid.NewGuid(), _user = Guid.NewGuid();
    private readonly Guid _ma = Guid.NewGuid(), _mb = Guid.NewGuid();
    private readonly List<Guid> _extraUsers = new();
    private string _connection = null!;
    private bool Proof => Environment.GetEnvironmentVariable("ER_MT5B_SQL_PROOF") == "1";
    private string Members => Proof ? "mt5b_probe_20261020_membership" : "institution_membership";
    private string Roles => Proof ? "mt5b_probe_20261020_role" : "institution_membership_role";
    private string State => Proof ? "mt5b_probe_20261020_security" : "institution_membership_security_state";
    private const string Mode = "ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION";

    private async Task<MySqlConnection> Open()
    {
        var c = new MySqlConnection(_connection);
        await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT VERSION(),@@port,REPLACE(@@datadir,CHAR(92),'/'),DATABASE(),@@GLOBAL.sql_mode";
        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync()); Assert.Equal("5.7.24", reader.GetString(0));
        Assert.Equal((int)TestMySqlTarget.Port, reader.GetInt32(1));
        Assert.EndsWith(TestMySqlTarget.DataDirectorySuffix, reader.GetString(2), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("electronic_register_test", reader.GetString(3)); Assert.Equal(Mode, reader.GetString(4));
        return c;
    }
    private static MySqlCommand Cmd(MySqlConnection c, string sql, MySqlTransaction? tx = null, params (string, object)[] values)
    {
        var cmd = new MySqlCommand(sql, c, tx);
        foreach (var (name, value) in values) cmd.Parameters.AddWithValue(name, value);
        return cmd;
    }
    private async Task Exec(string sql, params (string, object)[] values)
    { await using var c = await Open(); await using var cmd = Cmd(c, sql, null, values); await cmd.ExecuteNonQueryAsync(); }
    private async Task<long> Generation(Guid owner, Guid member)
    {
        await using var c = await Open();
        await using var cmd = Cmd(c, $"SELECT security_generation FROM {State} WHERE institution_id=@o AND membership_id=@m", null, ("@o", owner), ("@m", member));
        var value = await cmd.ExecuteScalarAsync(); Assert.NotNull(value); return Convert.ToInt64(value);
    }
    private Task Add(string role, Guid? member = null) => Exec($"INSERT INTO {Roles}(membership_id,role) VALUES(@m,@r)", ("@m", member ?? _ma), ("@r", role));
    private Task Status(string status) => Exec($"UPDATE {Members} SET status=@s WHERE institution_id=@o AND id=@m", ("@s", status), ("@o", _a), ("@m", _ma));

    public async Task InitializeAsync()
    {
        _connection = IntegrationFixture.ValidateLocalConnectionString(Environment.GetEnvironmentVariable("TEST_MYSQL_CONNECTION") ?? throw new InvalidOperationException("Explicit isolated target required"));
        var b = new MySqlConnectionStringBuilder(_connection);
        Assert.Equal("127.0.0.1", b.Server); Assert.Equal(TestMySqlTarget.Port, b.Port);
        b.AllowUserVariables = true; _connection = b.ConnectionString;
        await Exec("INSERT INTO institution(id,code,name,status) VALUES(@a,@ca,'Synthetic IAM A','active'),(@b,@cb,'Synthetic IAM B','active')",
            ("@a", _a), ("@b", _b), ("@ca", "MT5BP_" + _a.ToString("N")), ("@cb", "MT5BP_" + _b.ToString("N")));
        await Exec("INSERT INTO user(id,email,password_hash,role) VALUES(@u,@e,'not-an-authentication-fixture','admin')", ("@u", _user), ("@e", _user.ToString("N") + "@mt5bp.example.test"));
        await Exec($"INSERT INTO {Members}(id,institution_id,user_id,status) VALUES(@ma,@a,@u,'active'),(@mb,@b,@u,'active')",
            ("@ma", _ma), ("@mb", _mb), ("@a", _a), ("@b", _b), ("@u", _user));
    }
    public async Task DisposeAsync()
    {
        if (_connection is null) return;
        await using var c = await Open();
        await using var cmd = Cmd(c, $"SET @er_mt5b_fixture_owners=@owners; DELETE r FROM {Roles} r JOIN {Members} m ON m.id=r.membership_id WHERE m.institution_id IN (@a,@b); DELETE FROM {Members} WHERE institution_id IN (@a,@b); DELETE FROM institution WHERE id IN (@a,@b) AND code LIKE 'MT5BP_%'; DELETE FROM user WHERE id=@u AND email=@e; SET @er_mt5b_fixture_owners=NULL;", null,
            ("@owners", _a + "," + _b), ("@a", _a), ("@b", _b), ("@u", _user), ("@e", _user.ToString("N") + "@mt5bp.example.test"));
        try
        {
            await cmd.ExecuteNonQueryAsync();
            foreach(var id in _extraUsers)
            {
                await using var extra = Cmd(c, "DELETE FROM user WHERE id=@u AND email=@e", null, ("@u", id), ("@e", id.ToString("N") + "@mt5bp.example.test"));
                await extra.ExecuteNonQueryAsync();
            }
        }
        finally { await using var clear = Cmd(c, "SET @er_mt5b_fixture_owners=NULL"); await clear.ExecuteNonQueryAsync(); }
    }

    [Fact]
    public async Task Sql_InitialStateAndOwnerArePersistent()
    {
        Assert.Equal(0, await Generation(_a, _ma)); Assert.Equal(0, await Generation(_b, _mb));
        await using var c = await Open();
        await using var cmd = Cmd(c, $"SELECT COUNT(*) FROM {State} WHERE institution_id=@a AND membership_id=@mb", null, ("@a", _a), ("@mb", _mb));
        Assert.Equal(0L, Convert.ToInt64(await cmd.ExecuteScalarAsync()));
    }

    [Theory]
    [InlineData("InstitutionAdmin")][InlineData("Teacher")][InlineData("Student")][InlineData("Staff")]
    public async Task Sql_RoleAddRemoveRestoreNeverRestoresGeneration(string role)
    {
        await Add(role); Assert.Equal(1, await Generation(_a, _ma));
        await Exec($"DELETE r FROM {Roles} r JOIN {Members} m ON m.id=r.membership_id WHERE m.institution_id=@o AND m.id=@m AND r.role=@r", ("@o", _a), ("@m", _ma), ("@r", role));
        Assert.Equal(2, await Generation(_a, _ma)); await Add(role);
        Assert.Equal(3, await Generation(_a, _ma)); Assert.Equal(0, await Generation(_b, _mb));
    }

    [Fact]
    public async Task Sql_InsertSelectMultirowAndDuplicateNoopRemainCompatible()
    {
        await Exec($"INSERT INTO {Roles}(membership_id,role) SELECT m.id,'Teacher' FROM {Members} m WHERE m.id IN (@ma,@mb)", ("@ma", _ma), ("@mb", _mb));
        Assert.Equal(1, await Generation(_a, _ma)); Assert.Equal(1, await Generation(_b, _mb));
        await Exec($"INSERT INTO {Roles}(membership_id,role) VALUES(@m,'Student'),(@m,'InstitutionAdmin')", ("@m", _ma));
        Assert.Equal(3, await Generation(_a, _ma));
        var duplicate = await Assert.ThrowsAsync<MySqlException>(() => Add("Teacher")); Assert.Equal(1062, duplicate.Number);
        await Exec($"INSERT IGNORE INTO {Roles}(membership_id,role) SELECT m.id,'Teacher' FROM {Members} m WHERE m.id=@m", ("@m", _ma));
        await Exec($"UPDATE {Roles} SET role=role WHERE membership_id=@m", ("@m", _ma));
        Assert.Equal(3, await Generation(_a, _ma));
    }

    [Fact]
    public async Task Sql_StatusRoundtripAndNoopAreMonotone()
    {
        await Status("suspended"); Assert.Equal(1, await Generation(_a, _ma));
        await Status("suspended"); Assert.Equal(1, await Generation(_a, _ma));
        await Status("active"); Assert.Equal(2, await Generation(_a, _ma)); Assert.Equal(0, await Generation(_b, _mb));
    }

    [Fact]
    public async Task Sql_RollbackPreservesGenerationAndBothTenants()
    {
        await using var c = await Open(); await using var tx = await c.BeginTransactionAsync();
        await using var cmd = Cmd(c, $"INSERT INTO {Roles}(membership_id,role) VALUES(@m,'Teacher'); UPDATE {Members} SET status='suspended' WHERE id=@m;", tx, ("@m", _ma));
        await cmd.ExecuteNonQueryAsync(); await tx.RollbackAsync();
        Assert.Equal(0, await Generation(_a, _ma)); Assert.Equal(0, await Generation(_b, _mb));
    }

    [Fact]
    public async Task Sql_ConcurrentChangesHaveNoLostOrDuplicateIncrements()
    {
        await Task.WhenAll(Add("Teacher"), Add("Student")); Assert.Equal(2, await Generation(_a, _ma));
        await Task.WhenAll(Status("suspended"), Status("suspended")); Assert.Equal(3, await Generation(_a, _ma));
        Assert.Equal(0, await Generation(_b, _mb));
    }

    [Theory]
    [InlineData("generation")][InlineData("state-delete")][InlineData("membership-delete")]
    [InlineData("owner")][InlineData("user")][InlineData("id")][InlineData("role-update")]
    public async Task Sql_IdentityResetAndOperationalDeleteAreRejected(string kind)
    {
        await Add("Teacher");
        var sql = kind switch
        {
            "generation" => $"UPDATE {State} SET security_generation=0 WHERE membership_id=@m",
            "state-delete" => $"DELETE FROM {State} WHERE membership_id=@m",
            "membership-delete" => $"DELETE FROM {Members} WHERE id=@m",
            "owner" => $"UPDATE {Members} SET institution_id=@b WHERE id=@m",
            "user" => $"UPDATE {Members} SET user_id=@u WHERE id=@m",
            "id" => $"UPDATE {Members} SET id=@u WHERE id=@m",
            _ => $"UPDATE {Roles} SET role='Student' WHERE membership_id=@m"
        };
        var error = await Assert.ThrowsAsync<MySqlException>(() => Exec(sql, ("@m", _ma), ("@b", _b), ("@u", Guid.NewGuid())));
        Assert.Equal(kind == "membership-delete" && !Proof ? 1451 : 1644, error.Number); Assert.Equal(1, await Generation(_a, _ma));
        // Same ID cannot be recreated because the original identity is retained.
        var duplicate = await Assert.ThrowsAsync<MySqlException>(() => Exec($"INSERT INTO {Members}(id,institution_id,user_id,status) VALUES(@m,@a,@u,'active')", ("@m", _ma), ("@a", _a), ("@u", _user)));
        Assert.Equal(1062, duplicate.Number);
    }

    [Fact]
    public async Task Sql_MissingStateFailsClosedForRoleAndStatus()
    {
        await Exec($"SET @er_mt5b_fixture_owners=@o; DELETE FROM {State} WHERE institution_id=@o AND membership_id=@m; SET @er_mt5b_fixture_owners=NULL", ("@o", _a.ToString()), ("@m", _ma));
        Assert.Equal(1644, (await Assert.ThrowsAsync<MySqlException>(() => Add("Teacher"))).Number);
        Assert.Equal(1644, (await Assert.ThrowsAsync<MySqlException>(() => Status("suspended"))).Number);
    }

    [Fact]
    public async Task Sql_OverflowRollsBackTheMutationWithoutReset()
    {
        await using var c = await Open(); await using var tx = await c.BeginTransactionAsync();
        await using(var advance = Cmd(c, $"UPDATE {State} SET security_generation=9223372036854775807 WHERE membership_id=@m", tx, ("@m", _ma))) await advance.ExecuteNonQueryAsync();
        await using(var role = Cmd(c, $"INSERT INTO {Roles}(membership_id,role) VALUES(@m,'Teacher')", tx, ("@m", _ma)))
            Assert.Equal(1644, (await Assert.ThrowsAsync<MySqlException>(async () => { await role.ExecuteNonQueryAsync(); })).Number);
        await using(var state = Cmd(c, $"UPDATE {Members} SET status='suspended' WHERE id=@m", tx, ("@m", _ma)))
            Assert.Equal(1644, (await Assert.ThrowsAsync<MySqlException>(async () => { await state.ExecuteNonQueryAsync(); })).Number);
        await tx.RollbackAsync(); Assert.Equal(0, await Generation(_a, _ma));
    }

    [Fact]
    public async Task Sql_BackfillInitializesMissingOnlyWithoutResettingExistingGeneration()
    {
        await Add("Teacher", _mb);
        await Exec($"SET @er_mt5b_fixture_owners=@o; DELETE FROM {State} WHERE institution_id=@o AND membership_id=@m; SET @er_mt5b_fixture_owners=NULL", ("@o", _a.ToString()), ("@m", _ma));
        var sql = $"INSERT INTO {State}(institution_id,membership_id,security_generation) SELECT m.institution_id,m.id,0 FROM {Members} m LEFT JOIN {State} s ON s.membership_id=m.id WHERE s.membership_id IS NULL";
        await Exec(sql); Assert.Equal(0, await Generation(_a, _ma)); Assert.Equal(1, await Generation(_b, _mb));
        await Exec(sql); Assert.Equal(0, await Generation(_a, _ma)); Assert.Equal(1, await Generation(_b, _mb));
    }

    [Fact]
    public async Task Sql_CompositeForeignKeyRejectsWrongOwnerAndOrphanAfterPrivilegedFixturePurge()
    {
        await using var c=await Open(); await using var tx=await c.BeginTransactionAsync();
        try
        {
            await using(var remove=Cmd(c,$"SET @er_mt5b_fixture_owners=@a; DELETE FROM {State} WHERE institution_id=@a AND membership_id=@m",tx,("@a",_a.ToString()),("@m",_ma))) await remove.ExecuteNonQueryAsync();
            await using(var wrong=Cmd(c,$"INSERT INTO {State}(institution_id,membership_id,security_generation) VALUES(@b,@m,0)",tx,("@b",_b),("@m",_ma)))
                Assert.Equal(1452,(await Assert.ThrowsAsync<MySqlException>(async()=>{await wrong.ExecuteNonQueryAsync();})).Number);
            await using(var orphan=Cmd(c,$"INSERT INTO {State}(institution_id,membership_id,security_generation) VALUES(@a,@m,0)",tx,("@a",_a),("@m",Guid.NewGuid())))
                Assert.Equal(1452,(await Assert.ThrowsAsync<MySqlException>(async()=>{await orphan.ExecuteNonQueryAsync();})).Number);
        }
        finally
        {
            await tx.RollbackAsync();
            await using var clear=Cmd(c,"SET @er_mt5b_fixture_owners=NULL");await clear.ExecuteNonQueryAsync();
        }
        Assert.Equal(0,await Generation(_a,_ma));Assert.Equal(0,await Generation(_b,_mb));
    }

    [Fact]
    public async Task Sql_InstitutionMutexSerializesAndSecondTransactionSeesCommittedGeneration()
    {
        await using var first = await Open(); await using var second = await Open();
        Assert.NotEqual(first.ServerThread, second.ServerThread);
        await using var one = await first.BeginTransactionAsync(); await using var two = await second.BeginTransactionAsync();
        await using(var acquire = Cmd(first, "SELECT id FROM institution WHERE id=@o FOR UPDATE", one, ("@o", _a))) await acquire.ExecuteScalarAsync();
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<long> Contender()
        {
            started.SetResult(true);
            await using(var acquire = Cmd(second, "SELECT id FROM institution WHERE id=@o FOR UPDATE", two, ("@o", _a))) await acquire.ExecuteScalarAsync();
            await using var read = Cmd(second, $"SELECT security_generation FROM {State} WHERE institution_id=@o AND membership_id=@m", two, ("@o", _a), ("@m", _ma));
            var observed = Convert.ToInt64(await read.ExecuteScalarAsync());
            await using(var role = Cmd(second, $"INSERT INTO {Roles}(membership_id,role) VALUES(@m,'Student')", two, ("@m", _ma))) await role.ExecuteNonQueryAsync();
            await two.CommitAsync(); return observed;
        }
        var contender = Contender();
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Task.Delay(100); Assert.False(contender.IsCompleted);
            await using(var role = Cmd(first, $"INSERT INTO {Roles}(membership_id,role) VALUES(@m,'Teacher')", one, ("@m", _ma))) await role.ExecuteNonQueryAsync();
            await one.CommitAsync();
            Assert.Equal(1, await contender.WaitAsync(TimeSpan.FromSeconds(10)));
        }
        finally { if (!contender.IsCompleted) await one.RollbackAsync(); }
        Assert.Equal(2, await Generation(_a, _ma)); Assert.Equal(0, await Generation(_b, _mb));
    }

    [Theory]
    [InlineData("owner")][InlineData("membership")][InlineData("negative")]
    public async Task Sql_SidecarIdentityAndNegativeGenerationAreRejected(string kind)
    {
        var sql = kind switch
        {
            "owner" => $"UPDATE {State} SET institution_id=@b WHERE membership_id=@m",
            "membership" => $"UPDATE {State} SET membership_id=@other WHERE membership_id=@m",
            _ => $"UPDATE {State} SET security_generation=-1 WHERE membership_id=@m"
        };
        Assert.Equal(1644, (await Assert.ThrowsAsync<MySqlException>(() =>
            Exec(sql, ("@b", _b), ("@m", _ma), ("@other", Guid.NewGuid())))).Number);
        Assert.Equal(0, await Generation(_a, _ma));
        Assert.Equal(0, await Generation(_b, _mb));
    }

    [Theory]
    [InlineData("status-noop")][InlineData("role-noop")][InlineData("role-delete")]
    public async Task Sql_MissingStateRejectsNoopsAndRoleDelete(string kind)
    {
        await Add("Teacher");
        await using var c = await Open(); await using var tx = await c.BeginTransactionAsync();
        await using (var remove = Cmd(c, $"SET @er_mt5b_fixture_owners=@o; DELETE FROM {State} WHERE membership_id=@m; SET @er_mt5b_fixture_owners=NULL", tx, ("@o", _a.ToString()), ("@m", _ma)))
            await remove.ExecuteNonQueryAsync();
        var sql = kind switch
        {
            "status-noop" => $"UPDATE {Members} SET status=status WHERE id=@m",
            "role-noop" => $"UPDATE {Roles} SET role=role WHERE membership_id=@m",
            _ => $"DELETE FROM {Roles} WHERE membership_id=@m"
        };
        await using (var mutation = Cmd(c, sql, tx, ("@m", _ma)))
            Assert.Equal(1644, (await Assert.ThrowsAsync<MySqlException>(async () => { await mutation.ExecuteNonQueryAsync(); })).Number);
        await tx.RollbackAsync();
        Assert.Equal(1, await Generation(_a, _ma));
    }

    [Theory]
    [InlineData(-1L)][InlineData(1L)]
    public async Task Sql_NewStateRejectsAnyNonzeroInitialGeneration(long generation)
    {
        await using var c = await Open(); await using var tx = await c.BeginTransactionAsync();
        await using (var remove = Cmd(c, $"SET @er_mt5b_fixture_owners=@o; DELETE FROM {State} WHERE membership_id=@m; SET @er_mt5b_fixture_owners=NULL", tx, ("@o", _a.ToString()), ("@m", _ma)))
            await remove.ExecuteNonQueryAsync();
        await using (var insert = Cmd(c, $"INSERT INTO {State}(institution_id,membership_id,security_generation) VALUES(@o,@m,@g)", tx, ("@o", _a), ("@m", _ma), ("@g", generation)))
            Assert.Equal(1644, (await Assert.ThrowsAsync<MySqlException>(async () => { await insert.ExecuteNonQueryAsync(); })).Number);
        await tx.RollbackAsync();
        Assert.Equal(0, await Generation(_a, _ma));
    }

    [Fact]
    public async Task Sql_SuspendedTimestampAloneDoesNotAdvanceGeneration()
    {
        await Exec($"UPDATE {Members} SET suspended_at=@time WHERE id=@m", ("@time", DateTime.UtcNow), ("@m", _ma));
        Assert.Equal(0, await Generation(_a, _ma));
        Assert.Equal(0, await Generation(_b, _mb));
    }

    [Fact]
    public async Task Sql_OverflowRejectsRoleDeleteWithoutLosingRole()
    {
        await Add("Teacher");
        await using var c = await Open(); await using var tx = await c.BeginTransactionAsync();
        await using (var advance = Cmd(c, $"UPDATE {State} SET security_generation=9223372036854775807 WHERE membership_id=@m", tx, ("@m", _ma)))
            await advance.ExecuteNonQueryAsync();
        await using (var remove = Cmd(c, $"DELETE FROM {Roles} WHERE membership_id=@m", tx, ("@m", _ma)))
            Assert.Equal(1644, (await Assert.ThrowsAsync<MySqlException>(async () => { await remove.ExecuteNonQueryAsync(); })).Number);
        await using (var read = Cmd(c, $"SELECT COUNT(*) FROM {Roles} WHERE membership_id=@m AND role='Teacher'", tx, ("@m", _ma)))
            Assert.Equal(1L, Convert.ToInt64(await read.ExecuteScalarAsync()));
        await tx.RollbackAsync();
        Assert.Equal(1, await Generation(_a, _ma));
    }

    [Fact]
    public async Task Sql_FixtureGuardRequiresExplicitOwnerAndSyntheticCodeAndExcludesHolding()
    {
        await using var c = await Open(); await using var tx = await c.BeginTransactionAsync();
        await using (var owners = Cmd(c, "SET @er_mt5b_fixture_owners=@b", tx, ("@b", _b.ToString())))
            await owners.ExecuteNonQueryAsync();
        await using (var remove = Cmd(c, $"DELETE FROM {State} WHERE membership_id=@m", tx, ("@m", _ma)))
            Assert.Equal(1644, (await Assert.ThrowsAsync<MySqlException>(async () => { await remove.ExecuteNonQueryAsync(); })).Number);
        await using (var setup = Cmd(c, "SET @er_mt5b_fixture_owners=@a; UPDATE institution SET code=@code WHERE id=@a", tx, ("@a", _a.ToString()), ("@code", "UNLISTED_" + _a.ToString("N"))))
            await setup.ExecuteNonQueryAsync();
        await using (var remove = Cmd(c, $"DELETE FROM {State} WHERE membership_id=@m", tx, ("@m", _ma)))
            Assert.Equal(1644, (await Assert.ThrowsAsync<MySqlException>(async () => { await remove.ExecuteNonQueryAsync(); })).Number);
        // Observe the real holding only. Never use its data as a mutation fixture.
        await using (var holding = Cmd(c, "SELECT COUNT(*) FROM institution WHERE code='MT3A_LEGACY_TEST' AND NOT(BINARY code REGEXP '^MT(1|1_REHEARSAL|2|3|3_REHEARSAL|3A|3B|3C|3D|4|4B|5A|5BP)_[0-9a-fA-F]{32}$')", tx))
            Assert.Equal(1L, Convert.ToInt64(await holding.ExecuteScalarAsync()));
        await tx.RollbackAsync();
        await using var clear = Cmd(c, "SET @er_mt5b_fixture_owners=NULL"); await clear.ExecuteNonQueryAsync();
        Assert.Equal(0, await Generation(_a, _ma));
    }

    private async Task Services(Func<IServiceProvider, Task> action)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        { ["ConnectionStrings:DefaultConnection"] = _connection }).Build();
        await using var services = new ServiceCollection().AddLogging().AddInfrastructure(configuration).BuildServiceProvider();
        using var scope = services.CreateScope(); await action(scope.ServiceProvider);
    }

    [Theory]
    [InlineData("active")][InlineData("suspended")][InlineData("archived")]
    public async Task TechnicalWritersCannotMutateOperationalInstitutionsEvenForGlobalAdmin(string status)
    {
        await Exec("UPDATE institution SET status=@s WHERE id=@a", ("@s", status), ("@a", _a));
        var afterLifecycle=await Generation(_a,_ma);
        Assert.Equal(status=="active"?0L:1L,afterLifecycle);
        await Services(async sp =>
        {
            var r = sp.GetRequiredService<IInstitutionMembershipRepository>();
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => r.AddRoleAsync(_a, _ma, InstitutionRole.Teacher));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => r.SuspendAsync(_a, _ma, DateTime.UtcNow));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => r.AddAsync(new() { Id=Guid.NewGuid(),InstitutionId=_a,UserId=_user,Status=MembershipStatus.Active }));
        });
        Assert.Equal(afterLifecycle, await Generation(_a, _ma)); Assert.Equal(0, await Generation(_b, _mb));
        await using var c = await Open(); await using var user = Cmd(c, "SELECT role FROM user WHERE id=@u", null, ("@u", _user));
        Assert.Equal("admin", await user.ExecuteScalarAsync());
    }

    [Fact]
    public async Task TechnicalProvisioningUsesSqlGenerationWithoutInventingHumanActorOrProfiles()
    {
        await Exec("UPDATE institution SET status='provisioning' WHERE id=@a", ("@a", _a));
        // Synthetic preparation only: the governance manager never permits this
        // transition. SQL still revokes previous access rather than resetting it.
        var afterLifecycle=await Generation(_a,_ma);Assert.Equal(1,afterLifecycle);
        var user = Guid.NewGuid(); var member = Guid.NewGuid(); _extraUsers.Add(user);
        await Exec("INSERT INTO user(id,email,password_hash,role) VALUES(@u,@e,'not-an-authentication-fixture','admin')", ("@u", user), ("@e", user.ToString("N") + "@mt5bp.example.test"));
        await Services(async sp =>
        {
            var r = sp.GetRequiredService<IInstitutionMembershipRepository>();
            await r.AddRoleAsync(_a, _ma, InstitutionRole.Student);
            await r.SuspendAsync(_a, _ma, DateTime.UtcNow);
            await r.AddAsync(new() { Id=member, InstitutionId=_a, UserId=user, Status=MembershipStatus.Active,
                Roles=new(){InstitutionRole.Teacher,InstitutionRole.Student} });
        });
        Assert.Equal(afterLifecycle+2, await Generation(_a, _ma)); Assert.Equal(2, await Generation(_a, member)); Assert.Equal(0, await Generation(_b, _mb));
        await Services(async sp =>
        {
            var db = sp.GetRequiredService<ElectronicRegisterContext>();
            Assert.False(await db.InstitutionTeacherProfiles.AnyAsync(p=>p.InstitutionId==_a));
            Assert.False(await db.InstitutionStudentProfiles.AnyAsync(p=>p.InstitutionId==_a));
        });
    }

    [Fact]
    public async Task ScopedReaderUsesPersistedGenerationAndFailsClosedOutsideOwnerOrWhenAbsent()
    {
        await Add("Teacher");
        await Services(async sp =>
        {
            var r = sp.GetRequiredService<IInstitutionMembershipSecurityStateRepository>();
            Assert.Equal(1, await r.GetRequiredGenerationAsync(_a, _ma));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>r.GetRequiredGenerationAsync(_b,_ma));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>r.GetRequiredGenerationAsync(_a,Guid.NewGuid()));
        });
        await Exec($"SET @er_mt5b_fixture_owners=@o; DELETE FROM {State} WHERE institution_id=@o AND membership_id=@m; SET @er_mt5b_fixture_owners=NULL", ("@o", _a.ToString()), ("@m", _ma));
        // Restore the fixture state before teardown: missing-state failures cannot be
        // bypassed by deleting roles. This privileged synthetic test explicitly owns A.
        try { await Services(sp=>Assert.ThrowsAsync<UnauthorizedAccessException>(()=>sp.GetRequiredService<IInstitutionMembershipSecurityStateRepository>().GetRequiredGenerationAsync(_a,_ma))); }
        finally { await Exec($"INSERT INTO {State}(institution_id,membership_id,security_generation) VALUES(@a,@m,0)", ("@a",_a),("@m",_ma)); }
    }

    [Fact]
    public async Task MappingHasCompositeOwnerRestrictForeignKeyAndNoNavigationGraph()
    {
        await Services(sp=>
        {
            var db=sp.GetRequiredService<ElectronicRegisterContext>();
            var type=db.Model.FindEntityType(typeof(ElectronicRegisterAPI.Infrastructure.Persistence.Entities.InstitutionMembershipSecurityState))!;
            Assert.Equal(new[]{"InstitutionId","MembershipId"}, type.FindPrimaryKey()!.Properties.Select(p=>p.Name));
            Assert.Empty(type.GetNavigations());
            var fk=Assert.Single(type.GetForeignKeys()); Assert.Equal(DeleteBehavior.Restrict,fk.DeleteBehavior);
            Assert.Equal(new[]{"InstitutionId","MembershipId"},fk.Properties.Select(p=>p.Name));
            return Task.CompletedTask;
        });
    }

    [Fact]
    public void PersistenceIntroducesNoHttpMutationSurface()
    {
        var methods=typeof(ElectronicRegisterAPI.Api.Controllers.InstitutionMembershipAdminController).GetMethods()
            .SelectMany(m=>m.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute),true))
            .Cast<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>().ToArray();
        // Persistence adds no route; E explicitly adds exactly one gated mutation action.
        Assert.Equal(3,methods.Length);
        Assert.Equal(2,methods.Count(m=>m.HttpMethods.SequenceEqual(new[]{"GET"})));
        Assert.Single(methods.Where(m=>m.HttpMethods.SequenceEqual(new[]{"POST"})));
        Assert.True(typeof(ElectronicRegisterAPI.Api.Controllers.InstitutionMembershipAdminController).GetMethod("Mutate")!
            .IsDefined(typeof(ElectronicRegisterAPI.Api.Security.InstitutionMembershipWriteEndpointAttribute),false));
    }
}
