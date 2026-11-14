using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Application.Services;
using ElectronicRegisterAPI.Domain.Configuration;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Unit;

// BCrypt for imports: bounded by the cores and shared by every concurrent import.
public sealed class ImportHashBudgetTests
{
    [Theory]
    [InlineData(0, 8, 8)]
    [InlineData(0, 2, 2)]
    [InlineData(3, 8, 3)]
    [InlineData(16, 4, 4)]
    [InlineData(-5, 4, 4)]
    [InlineData(0, 64, PlatformIdentitySettings.MaximumImportHashParallelism)]
    [InlineData(0, 1, 1)]
    public void DegreeIsNeverAboveTheCoresOrTheCeiling(int configured, int cores, int expected) =>
        Assert.Equal(expected, new ImportHashBudget(configured, cores).Degree);

    [Fact]
    public void EverySecretIsHashedOnceAndInOrder()
    {
        var budget = new ImportHashBudget(3, 8);
        var secrets = Enumerable.Range(0, 50).Select(i => "s" + i).ToArray();
        var hashes = budget.Hash(secrets, s => "h:" + s, CancellationToken.None);
        Assert.Equal(secrets.Select(s => "h:" + s), hashes);
    }

    [Fact]
    public async Task ConcurrentImportsShareOneBudget()
    {
        var budget = new ImportHashBudget(2, 8);
        var running = 0;
        var peak = 0;
        string Slow(string s)
        {
            var now = Interlocked.Increment(ref running);
            int seen;
            while (now > (seen = Volatile.Read(ref peak)) && Interlocked.CompareExchange(ref peak, now, seen) != seen) { }
            Thread.Sleep(15);
            Interlocked.Decrement(ref running);
            return s;
        }
        var secrets = Enumerable.Range(0, 24).Select(i => "s" + i).ToArray();
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() => budget.Hash(secrets, Slow, CancellationToken.None))));
        Assert.InRange(peak, 1, 2);
    }

    [Fact]
    public void CancellationStopsTheBatch()
    {
        var budget = new ImportHashBudget(2, 8);
        using var cts = new CancellationTokenSource();
        var calls = 0;
        Assert.ThrowsAny<OperationCanceledException>(() => budget.Hash(Enumerable.Range(0, 500).Select(i => "s" + i).ToArray(), s =>
        {
            if (Interlocked.Increment(ref calls) == 5)
                cts.Cancel();
            return s;
        }, cts.Token));
        Assert.True(calls < 500);
    }
}
