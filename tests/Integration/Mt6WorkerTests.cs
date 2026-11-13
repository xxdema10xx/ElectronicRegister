using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Api.BackgroundServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

public sealed class Mt6WorkerTests
{
    private sealed class ForbiddenScope : IServiceScopeFactory
    {
        public int Calls;
        public IServiceScope CreateScope() { Calls++; throw new InvalidOperationException("Must not query data with rollout closed"); }
    }
    [Theory]
    [InlineData(null)]
    [InlineData("false")]
    public async Task DisabledLegacyAndInstitutionWorkersDoNotEvenResolveRepositories(string? enabled)
    {
        var factory = new ForbiddenScope();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Identity:Legacy:Enabled"] = enabled }).Build();
        var worker = new ExpiredExamSubmissionWorker(factory, NullLogger<ExpiredExamSubmissionWorker>.Instance, config);
        await worker.ProcessBatchAsync(default);
        Assert.Equal(0, factory.Calls);
    }
}
