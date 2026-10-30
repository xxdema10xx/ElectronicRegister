using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MySqlConnector;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

// W3C trace context crosses real HTTP. EF supplies actual open connections.
// Test-only: no SQL-text inference and no production instrumentation.
internal static class SqlLockEvidence
{
    private sealed class Operation { internal MySqlConnection? Connection; }
    private static readonly ConcurrentDictionary<ActivityTraceId, Operation> Active = new();
    private static readonly ConditionalWeakTable<Task, Operation> Tasks = new();
    private static readonly AsyncLocal<Operation?> Current = new();
    private static readonly IDisposable Subscription = DiagnosticListener.AllListeners.Subscribe(new Listeners());

    internal static Task<T> Track<T>(Func<Task<T>> action) => Start(action, null);
    internal static Task<T> Track<T>(MySqlConnection connection, Func<Task<T>> action) => Start(action, connection);
    internal static Task Track(Func<Task> action) => Track(async () => { await action(); return true; });
    private static Task<T> Start<T>(Func<Task<T>> action, MySqlConnection? connection)
    {
        var operation = new Operation();
        async Task<T> Run()
        {
            using var activity = new Activity("SqlLockEvidence.Operation").SetIdFormat(ActivityIdFormat.W3C)
                .SetParentId(ActivityTraceId.CreateRandom(),ActivitySpanId.CreateRandom(),ActivityTraceFlags.Recorded).Start();
            Active[activity.TraceId] = operation;
            var previous = Current.Value; Current.Value = operation;
            if (connection is not null) Capture(connection);
            try { return await action(); }
            finally { Active.TryRemove(activity.TraceId, out _); Current.Value = previous; }
        }
        var task = Run(); Tasks.Add(task, operation); return task;
    }

    internal static void Capture(MySqlConnection connection)
    {
        var operation = Current.Value;
        if (Activity.Current is { } activity && Active.TryGetValue(activity.TraceId, out var traced)) operation = traced;
        if (operation is not null) Volatile.Write(ref operation.Connection, connection);
    }
    private static long Thread(Operation operation)
    {
        var connection=Volatile.Read(ref operation.Connection);
        return connection?.State==System.Data.ConnectionState.Open?connection.ServerThread:0;
    }

    internal static Task WaitAsync(string connectionString, Task contender, Task blocker) => WaitCoreAsync(connectionString, contender, blocker, 0);
    internal static async Task WaitAsync(string connectionString, Task contender, MySqlConnection blocker)
    {
        Assert.Equal(System.Data.ConnectionState.Open,blocker.State);
        var id=blocker.ServerThread;
        await WaitCoreAsync(connectionString,contender,null,id);
        Assert.Equal(System.Data.ConnectionState.Open,blocker.State);Assert.Equal(id,blocker.ServerThread);
    }
    internal static Task WaitAsync(string connectionString, Task contender, long blockerThread) => throw new Xunit.Sdk.XunitException("An unassociated integer cannot identify the blocker operation. Supply its tracked task or open connection.");
    // Original audit binary shape is retained, but resource markers fail closed.
    internal static Task WaitAsync(string connectionString, Task contender, Guid marker) => throw new Xunit.Sdk.XunitException("A resource marker cannot identify a MySQL connection or its task.");

    private static async Task WaitCoreAsync(string connectionString, Task contender, Task? blocker, long blockerThread)
    {
        Assert.True(Tasks.TryGetValue(contender, out var requester), "Requester task has no connection association. Use Track.");
        Operation? owner = null;
        if (blocker is not null) Assert.True(Tasks.TryGetValue(blocker, out owner), "Blocker task has no connection association.");
        await using var connection = new MySqlConnection(connectionString); await connection.OpenAsync();
        var watchdog = Stopwatch.StartNew();
        while (watchdog.Elapsed < TimeSpan.FromSeconds(15))
        {
            Assert.False(contender.IsCompleted, "Contender completed without its required InnoDB wait.");
            if (blocker is not null) Assert.False(blocker.IsCompleted, "Blocker completed before its wait edge was observed.");
            var requestId = Thread(requester!);
            var blockId = owner is null ? blockerThread : Thread(owner);
            if (requestId > 0 && blockId > 0)
            {
                Assert.NotEqual(requestId, blockId);
                await using var command = new MySqlCommand("SELECT COUNT(*) FROM information_schema.INNODB_LOCK_WAITS w JOIN information_schema.INNODB_TRX r ON r.trx_id=w.requesting_trx_id JOIN information_schema.INNODB_TRX b ON b.trx_id=w.blocking_trx_id WHERE r.trx_mysql_thread_id=@requester AND b.trx_mysql_thread_id=@blocker", connection);
                command.Parameters.AddWithValue("@requester", requestId); command.Parameters.AddWithValue("@blocker", blockId);
                if (Convert.ToInt64(await command.ExecuteScalarAsync()) > 0)
                {
                    Assert.False(contender.IsCompleted, "Contender completed during observation.");
                    if (blocker is not null) Assert.False(blocker.IsCompleted, "Blocker completed during observation.");
                    // A pre-transaction read can close/reopen a pooled connection
                    // while I_S is queried. An old physical ID is never evidence
                    // for its replacement: retry using the current open object.
                    if(requestId==Thread(requester!)&&(owner is null||blockId==Thread(owner)))return;
                }
            }
            await Task.Delay(150); // I_S cache pacing only; success requires the exact edge.
        }
        Assert.Fail("No requester -> blocker edge observed before watchdog elapsed.");
    }

    private sealed class Listeners : IObserver<DiagnosticListener>
    {
        public void OnNext(DiagnosticListener listener) { if (listener.Name == "Microsoft.EntityFrameworkCore") listener.Subscribe(new Commands(), key => key.EndsWith("CommandExecuting", StringComparison.Ordinal)); }
        public void OnError(Exception error) { }
        public void OnCompleted() { }
    }
    private sealed class Commands : IObserver<KeyValuePair<string, object?>>
    {
        public void OnNext(KeyValuePair<string, object?> value) { if (value.Value is CommandEventData data && data.Command.Connection is MySqlConnection connection) Capture(connection); }
        public void OnError(Exception error) { }
        public void OnCompleted() { }
    }
}
