using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using MySqlConnector;

namespace ElectronicRegisterAPI.Api.Operations;

public static class OperationalPipeline
{
    public const string MeterName = "ElectronicRegister.Http";
    private static readonly Meter Meter = new(MeterName, "1.0");
    private static readonly Counter<long> Requests = Meter.CreateCounter<long>("http.server.requests");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("http.server.duration", "ms");

    public static void AddOperationalServices(this WebApplicationBuilder builder, RuntimePolicy policy)
    {
        builder.Services.AddSingleton(policy);
        builder.Services.AddHostedService<InstitutionImportRetentionWorker>();
        builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.None);
        builder.Logging.AddFilter("Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware", LogLevel.None);
        builder.Logging.AddJsonConsole(o => { o.IncludeScopes = true; o.TimestampFormat = "O"; });
        builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(30));
        builder.Services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            o.ForwardLimit = 1;
            o.KnownProxies.Clear(); o.KnownIPNetworks.Clear();
            foreach (var proxy in policy.Proxies) o.KnownProxies.Add(proxy);
        });
        builder.Services.AddCors(o => o.AddPolicy("ConfiguredOrigins", p =>
        {
            if (policy.Origins.Length != 0) p.WithOrigins(policy.Origins).AllowAnyHeader().AllowAnyMethod();
        }));
        builder.Services.AddHttpsRedirection(o => o.HttpsPort = builder.Configuration.GetValue<int?>("Transport:HttpsPort") ?? 443);
        builder.Services.AddHsts(o => { o.MaxAge = TimeSpan.FromDays(180); o.IncludeSubDomains = false; });
        builder.Services.AddRateLimiter(o =>
        {
            var concurrentImports=builder.Configuration.GetValue("Multitenancy:InstitutionImport:ConcurrentUploads",2);
            if(concurrentImports is <1 or >8)throw new InvalidOperationException("Import concurrent upload limit must be 1..8.");
            o.AddConcurrencyLimiter("InstitutionImportUpload",options=>{options.PermitLimit=concurrentImports;options.QueueLimit=0;});
            o.RejectionStatusCode = 429;
            o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                HttpMethods.IsPost(context.Request.Method) && context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
                ? RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new()
                    { PermitLimit = policy.SensitivePermitLimit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true })
                : RateLimitPartition.GetNoLimiter("read"));
            o.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = "60";
                await Results.Problem(statusCode: 429, title: "Request limit exceeded").ExecuteAsync(context.HttpContext);
            };
        });
    }

    public static void UseOperationalPipeline(this WebApplication app)
    {
        var policy = app.Services.GetRequiredService<RuntimePolicy>();
        if (policy.Proxies.Length > 0) app.UseForwardedHeaders();
        app.Use(async (context, next) =>
        {
            if (policy.Local && (context.Connection.RemoteIpAddress is not { } ip || !IPAddress.IsLoopback(ip)))
            { context.Response.StatusCode = 403; return; }
            // A server-generated identifier prevents log/header injection and unbounded client labels.
            var correlation = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
            context.TraceIdentifier = correlation;
            context.Response.OnStarting(() =>
            {
                context.Response.Headers["X-Correlation-ID"] = correlation;
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                context.Response.Headers["X-Frame-Options"] = "DENY";
                context.Response.Headers["Referrer-Policy"] = "no-referrer";
                context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
                if (!app.Environment.IsDevelopment()) context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
                context.Response.Headers.CacheControl = "no-store";
                return Task.CompletedTask;
            });
            using var scope = app.Logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlation });
            var started = Stopwatch.GetTimestamp();
            try { await next(context); }
            finally
            {
                // No URLs, query strings, user IDs, SQL, bodies, tokens or exception text.
                var status = context.Response.StatusCode;
                Requests.Add(1, new KeyValuePair<string, object?>("status", status));
                Duration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                app.Logger.LogInformation("HTTP completed {StatusCode}", status);
            }
        });
        if (!policy.Local) { app.UseHsts(); app.UseHttpsRedirection(); }
        app.UseCors("ConfiguredOrigins");
        app.UseRateLimiter();
    }

    public static void MapOperationalEndpoints(this WebApplication app)
    {
        app.MapGet("/health/live", () => Results.Ok(new { status = "live" })).AllowAnonymous();
        app.MapGet("/health/ready", async (IConfiguration configuration, RuntimePolicy policy, IHostApplicationLifetime lifetime, CancellationToken ct) =>
        {
            if (lifetime.ApplicationStopping.IsCancellationRequested) return Results.StatusCode(503);
            try
            {
                var connection = new MySqlConnectionStringBuilder(configuration.GetConnectionString("DefaultConnection") ?? "") { ConnectionTimeout = 3, DefaultCommandTimeout = 3 };
                await using var sql = new MySqlConnection(connection.ConnectionString);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(4));
                await sql.OpenAsync(timeout.Token);
                await using var command = new MySqlCommand("SELECT VERSION(),@@hostname,@@port,REPLACE(@@datadir,CHAR(92),'/'),DATABASE()", sql);
                await using var reader = await command.ExecuteReaderAsync(timeout.Token);
                if (!await reader.ReadAsync(timeout.Token)) return Results.StatusCode(503);
                var expectedVersion = configuration["Readiness:ExpectedVersion"] ?? (policy.Local ? "5.7.24" : "");
                var expectedHost = configuration["Readiness:ExpectedHost"] ?? (policy.Local ? Environment.MachineName : "");
                var datadir = reader.GetString(3).TrimEnd('/');
                var expectedData = configuration["Readiness:ExpectedDataDir"]?.Replace('\\', '/').TrimEnd('/');
                var validData = expectedData is not null ? datadir.Equals(expectedData, StringComparison.OrdinalIgnoreCase) :
                    policy.Local && datadir.EndsWith("/migrations/mt4/private/mt5a-mamp-13306/data", StringComparison.OrdinalIgnoreCase);
                return reader.GetString(0) == expectedVersion && reader.GetString(1).Equals(expectedHost, StringComparison.OrdinalIgnoreCase) &&
                    reader.GetInt32(2) == connection.Port && reader.GetString(4) == connection.Database && validData
                    ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503);
            }
            catch (Exception) { return Results.StatusCode(503); }
        }).AllowAnonymous();
    }
}
