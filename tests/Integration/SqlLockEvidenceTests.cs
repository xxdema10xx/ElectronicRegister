using System;
using System.Threading.Tasks;
using MySqlConnector;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public sealed class SqlLockEvidenceTests(IntegrationFixture fixture)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualEdgeRequiresTheCurrentPhysicalConnections(bool reopenRequester)
    {
        await Scenario(async (owner, requester, other, table) =>
        {
            if (reopenRequester)
            {
                var old = requester.ServerThread;
                await requester.CloseAsync(); await requester.OpenAsync();
                Assert.NotEqual(old, requester.ServerThread);
            }
            var pending = SqlLockEvidence.Track(requester, () => Execute(requester, "UPDATE `"+table+"` SET value=2 WHERE id=1"));
            try { await SqlLockEvidence.WaitAsync(fixture.ConnectionString, pending, owner); }
            finally { await Execute(owner, "ROLLBACK"); await pending; }
        });
    }

    [Fact]
    public async Task DifferentConnectionIsNotEvidenceForTheActualBlocker()
    {
        await Scenario(async (owner, requester, other, table) =>
        {
            var pending = SqlLockEvidence.Track(requester, () => Execute(requester, "UPDATE `"+table+"` SET value=2 WHERE id=1"));
            try
            {
                await SqlLockEvidence.WaitAsync(fixture.ConnectionString, pending, owner);
                await Assert.ThrowsAnyAsync<Xunit.Sdk.XunitException>(() => SqlLockEvidence.WaitAsync(fixture.ConnectionString, pending, other));
            }
            finally { await Execute(owner, "ROLLBACK"); await pending; }
        });
    }

    [Fact]
    public async Task PendingTaskWithoutSqlDependencyCannotPassByWaiting()
    {
        await Scenario(async (owner, requester, other, table) =>
        {
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pending = SqlLockEvidence.Track(requester, () => release.Task);
            try
            {
                await Assert.ThrowsAnyAsync<Xunit.Sdk.XunitException>(() => SqlLockEvidence.WaitAsync(fixture.ConnectionString, pending, owner));
            }
            finally { release.SetResult(true); await pending; }
        });
    }

    [Fact]
    public async Task ClosedBlockerAndUnassociatedTaskAreRejected()
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);
        await using var closed = new MySqlConnection(fixture.ConnectionString);
        await Assert.ThrowsAnyAsync<Xunit.Sdk.XunitException>(() => SqlLockEvidence.WaitAsync(fixture.ConnectionString, Task.CompletedTask, closed));
        await closed.OpenAsync();
        await Assert.ThrowsAnyAsync<Xunit.Sdk.XunitException>(() => SqlLockEvidence.WaitAsync(fixture.ConnectionString, Task.CompletedTask, closed));
    }

    private async Task Scenario(Func<MySqlConnection, MySqlConnection, MySqlConnection, string, Task> test)
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);
        var settings = new MySqlConnectionStringBuilder(fixture.ConnectionString) { Pooling = false, DefaultCommandTimeout = 60 };
        await using var owner = new MySqlConnection(settings.ConnectionString);
        await using var requester = new MySqlConnection(settings.ConnectionString);
        await using var other = new MySqlConnection(settings.ConnectionString);
        await owner.OpenAsync(); await requester.OpenAsync(); await other.OpenAsync();
        var table = "lock_evidence_"+Guid.NewGuid().ToString("N");
        await Execute(owner, "CREATE TABLE `"+table+"` (id INT PRIMARY KEY, value INT NOT NULL) ENGINE=InnoDB");
        try
        {
            await Execute(owner, "INSERT INTO `"+table+"` VALUES (1,0)");
            await Execute(owner, "START TRANSACTION");
            await Execute(owner, "UPDATE `"+table+"` SET value=1 WHERE id=1");
            try { await test(owner, requester, other, table); }
            finally { await Execute(owner, "ROLLBACK"); }
        }
        finally { await Execute(owner, "DROP TABLE `"+table+"`"); }
    }

    private static async Task<int> Execute(MySqlConnection connection, string sql)
    {
        await using var command = new MySqlCommand(sql, connection);
        return await command.ExecuteNonQueryAsync();
    }
}
