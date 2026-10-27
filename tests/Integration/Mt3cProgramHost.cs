using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Api.Controllers;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
using ElectronicRegisterAPI.Infrastructure.Security;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

internal static class Mt3cHosting
{
    internal const string Key = "mt3c-local-test-signing-key-only-0123456789abcdef";
    // Executes the real Program, all DI and worker registrations retained; ephemeral Kestrel port.
    internal sealed class ProgramHost(IHost host, Task run, HttpClient client) : IAsyncDisposable
    {
        internal IHost Host => host;
        internal HttpClient Client => client;
        internal static async Task<ProgramHost> StartAsync(string connectionString, Guid legacyInstitution, Guid[] users, bool enabled = true, bool unbackfilled = true, bool? school = null, string environment = "Production", bool? exam = null, bool? course = null, bool? membershipAdmin = null, bool? membershipWrite = null, Action<IServiceCollection>? configureServices = null, bool? onboarding = null, string[]? extraArguments = null)
        {
            using var observer = new HostObserver(configureServices);
            using var subscription = DiagnosticListener.AllListeners.Subscribe(observer);
            var args = new List<string>
            {
                "--Deployment:Mode=LocalTest", "--environment=" + environment, "--urls=http://127.0.0.1:0", "--applicationName=" + typeof(ExamController).Assembly.GetName().Name,
                "--ConnectionStrings:DefaultConnection=" + connectionString,
                "--Jwt:Key=" + Key, "--Jwt:Issuer=mt2-tests", "--Jwt:Audience=mt2-client", "--Jwt:ExpiresInMinutes=10",
                "--AzureAd:TenantId=11111111-2222-3333-4444-555555555555", "--AzureAd:Audience=mt2-microsoft",
                "--Identity:Legacy:Enabled=" + enabled, "--Identity:Legacy:InstitutionId=" + legacyInstitution,
                "--Identity:Legacy:AllowUnbackfilledAccounts=" + unbackfilled,
                "--Logging:LogLevel:Default=Critical", "--Logging:EventLog:LogLevel:Default=None"
            };
            if (Environment.GetEnvironmentVariable("ER_TEST_ISOLATED_PORT") is { } isolatedPort)
            {
                args.Add("--Deployment:LocalTestDatabasePort=" + isolatedPort);
                args.Add("--Readiness:ExpectedDataDir=" + Environment.GetEnvironmentVariable("ER_TEST_ISOLATED_DATADIR"));
            }
            if (school.HasValue) args.Add("--Multitenancy:SchoolApi:Enabled=" + school.Value);
            if (course.HasValue) args.Add("--Multitenancy:CourseApi:Enabled=" + course.Value);
            if (exam.HasValue) args.Add("--Multitenancy:ExamApi:Enabled=" + exam.Value);
            if (membershipAdmin.HasValue) args.Add("--Multitenancy:MembershipAdminApi:Enabled=" + membershipAdmin.Value);
            if (membershipWrite.HasValue) args.Add("--Multitenancy:MembershipWriteApi:Enabled=" + membershipWrite.Value);
            if (onboarding.HasValue) args.Add("--Multitenancy:Onboarding:Enabled="+onboarding.Value);
            if (extraArguments is not null) args.AddRange(extraArguments);
            args.AddRange(users.Select((id, i) => "--Identity:Legacy:UserIds:" + i + "=" + id));
            var run = Task.Run(async () =>
            {
                var result = typeof(ExamController).Assembly.EntryPoint!.Invoke(null, new object[] { args.ToArray() });
                if (result is Task task) await task;
            });
            var completed = await Task.WhenAny(observer.Built.Task, run).WaitAsync(TimeSpan.FromSeconds(30));
            if (completed == run) await run;
            var host = await observer.Built.Task.WaitAsync(TimeSpan.FromSeconds(30));
            var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
            var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = lifetime.ApplicationStarted.Register(() => ready.TrySetResult(true));
            var started = await Task.WhenAny(ready.Task, run).WaitAsync(TimeSpan.FromSeconds(30));
            if (started == run) await run; // Surface startup failures instead of masking them as readiness timeouts.
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Contains(host.Services.GetServices<IHostedService>(), s => s.GetType().Name == "ExpiredExamSubmissionWorker");
            var address = host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            return new(host, run, new HttpClient { BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(15) });
        }
        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
            await host.StopAsync().WaitAsync(TimeSpan.FromSeconds(30));
            await run.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.True(lifetime.ApplicationStopped.IsCancellationRequested);
        }
    }

    internal sealed class HostObserver(Action<IServiceCollection>? configureServices = null) : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
    {
        internal TaskCompletionSource<IHost> Built { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<IDisposable> _subscriptions = new();
        public void OnNext(DiagnosticListener value) { if (value.Name == "Microsoft.Extensions.Hosting") _subscriptions.Add(value.Subscribe(this)); }
        public void OnNext(KeyValuePair<string, object?> value)
        {
            if(value.Key=="HostBuilding" && value.Value is IHostBuilder builder)
                builder.ConfigureServices((_,services)=>{TestHostIsolation.Configure(services);configureServices?.Invoke(services);});
            if(value.Key=="HostBuilt" && value.Value is IHost host)Built.TrySetResult(host);
        }
        public void OnCompleted() { }
        public void OnError(Exception error) => Built.TrySetException(error);
        public void Dispose() { foreach (var subscription in _subscriptions) subscription.Dispose(); }
    }
}
