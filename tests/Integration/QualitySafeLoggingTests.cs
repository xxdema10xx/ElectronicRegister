using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Api.Controllers;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

public sealed class QualitySafeLoggingTests
{
    [Fact]
    public async Task FailureLogContainsSafeCodeButNoExceptionMessageOrRequestSecrets()
    {
        var provider = new Capture();
        var services = new ServiceCollection().AddLogging(b => b.AddProvider(provider)).BuildServiceProvider();
        var type = typeof(InstitutionExamController).Assembly.GetType("ElectronicRegisterAPI.Api.ExceptionHandling.GlobalExceptionHandler", true)!;
        var handler = (IExceptionHandler)ActivatorUtilities.CreateInstance(services, type);
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = new MemoryStream();
        context.Request.Headers.Authorization = "Bearer must-never-be-logged";
        await handler.TryHandleAsync(context, new InvalidOperationException("Password=private; SELECT personal FROM user"), default);
        var log = string.Join("\n", provider.Messages);
        Assert.Contains("REQUEST_FAILURE", log);
        Assert.DoesNotContain("private", log);
        Assert.DoesNotContain("SELECT", log);
        Assert.DoesNotContain("must-never-be-logged", log);
        Assert.Equal(500, context.Response.StatusCode);
    }
    private sealed class Capture : ILoggerProvider, ILogger
    {
        internal List<string> Messages { get; } = [];
        public ILogger CreateLogger(string categoryName) => this;
        public void Dispose() { }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? error, Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, error));
    }
}
