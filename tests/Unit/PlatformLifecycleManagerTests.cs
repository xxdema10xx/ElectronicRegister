using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Application.Managers;
using ElectronicRegisterAPI.Domain.Configuration;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using Moq;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Unit;

public sealed class PlatformLifecycleManagerTests
{
    private static PlatformIdentitySettings Settings(bool enabled = true, int batch = 2) =>
        new(enabled, "its.example", "h", new byte[32], "e", new byte[32], 36, TimeSpan.FromDays(1), batch);

    private static Guid Id(int n) => new($"00000000-0000-4000-8000-{n:D12}");

    [Fact]
    public async Task DisabledOrMissingSchemaDoesNothing()
    {
        var repository = new Mock<IPlatformLifecycleRepository>(MockBehavior.Strict);
        var evaluator = new Mock<ILifecycleEvaluator>(MockBehavior.Strict);
        Assert.Equal(0, (await new PlatformLifecycleManager(Settings(enabled: false), repository.Object, evaluator.Object).RunPassAsync(default)).Examined);
        repository.Setup(r => r.InstalledAsync(default)).ReturnsAsync(false);
        Assert.Equal(0, (await new PlatformLifecycleManager(Settings(), repository.Object, evaluator.Object).RunPassAsync(default)).Examined);
    }

    [Fact]
    public async Task PassPaginatesWithinTheBatchAndIsolatesFailingAccounts()
    {
        var cutoff = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var repository = new Mock<IPlatformLifecycleRepository>();
        repository.Setup(r => r.InstalledAsync(default)).ReturnsAsync(true);
        repository.Setup(r => r.ReactivationCandidatesAsync(null, 2, default)).ReturnsAsync([Id(1)]);
        repository.Setup(r => r.ReactivationCandidatesAsync(Id(1), 1, default)).ReturnsAsync([]);
        repository.Setup(r => r.ClosureCandidatesAsync(null, 2, cutoff, default)).ReturnsAsync([Id(2), Id(3)]);
        repository.Setup(r => r.ApplyAsync(Id(1), default)).ReturnsAsync(LifecycleAction.Reactivate);
        repository.Setup(r => r.ApplyAsync(Id(2), default)).ThrowsAsync(new TimeoutException("contains no data"));
        repository.Setup(r => r.ApplyAsync(Id(3), default)).ReturnsAsync(LifecycleAction.Close);
        var evaluator = new Mock<ILifecycleEvaluator>();
        evaluator.Setup(e => e.StudentStartedBeforeUtc()).Returns(cutoff);
        var result = await new PlatformLifecycleManager(Settings(), repository.Object, evaluator.Object).RunPassAsync(default);
        Assert.Equal(3, result.Examined);
        Assert.Equal(1, result.Reactivated);
        Assert.Equal(1, result.Closed);
        Assert.Equal(1, result.Failed);
        Assert.Equal(["TimeoutException"], result.FailureTypes);
        // The batch budget (2) is exhausted by the closure page: no third closure query.
        repository.Verify(r => r.ClosureCandidatesAsync(It.IsAny<Guid?>(), It.IsAny<int>(), cutoff, default), Times.Once);
    }

    [Fact]
    public async Task CancellationStopsThePass()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var repository = new Mock<IPlatformLifecycleRepository>();
        repository.Setup(r => r.InstalledAsync(cancelled.Token)).ReturnsAsync(true);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new PlatformLifecycleManager(Settings(), repository.Object, Mock.Of<ILifecycleEvaluator>()).RunPassAsync(cancelled.Token));
    }
}
