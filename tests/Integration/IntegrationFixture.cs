using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Testcontainers.MySql;
using Xunit;
using ElectronicRegisterAPI.Application.DependencyInjection;
using ElectronicRegisterAPI.Business.DependencyInjection;
using ElectronicRegisterAPI.Infrastructure.DependencyInjection;
using ElectronicRegisterAPI.Infrastructure.Persistence;

namespace ElectronicRegisterAPI.Tests.Integration
{
    // A single fixture instance is shared by every integration test class: schema creation and base seed
    // run once, and Testcontainers mode starts one container only.
    [CollectionDefinition(Name)]
    public sealed class IntegrationCollection : ICollectionFixture<IntegrationFixture>
    {
        public const string Name = "MySqlIntegration";
    }

    public sealed class IntegrationFixture : IAsyncLifetime
    {
        public const string ConnectionEnvVar = "TEST_MYSQL_CONNECTION";

        // The only database a local (non-container) connection may target.
        public const string AllowedLocalDatabase = "electronic_register_test";

        private const string ContainerImage = "mysql:8.4.3";

        private MySqlContainer? _mysql;
        private ServiceProvider? _serviceProvider;

        internal string ConnectionString { get; private set; } = string.Empty;

        public bool IsAvailable { get; private set; }

        public string UnavailableReason { get; private set; } = "Ambiente di integrazione non inizializzato.";

        public IServiceScopeFactory ScopeFactory => _serviceProvider?.GetRequiredService<IServiceScopeFactory>()
            ?? throw new InvalidOperationException("Ambiente di integrazione non disponibile: " + UnavailableReason);

        public async Task InitializeAsync()
        {
            string connectionString;

            var providedConnection = Environment.GetEnvironmentVariable(ConnectionEnvVar);
            if (!string.IsNullOrWhiteSpace(providedConnection))
            {
                // Validated before any connection is opened; a wrong target throws and fails every test.
                connectionString = ValidateLocalConnectionString(providedConnection);
                await VerifyIsolatedIdentityAsync(connectionString);
            }
            else
            {
                var containerConnection = await TryStartContainerAsync();
                if (containerConnection is null)
                    return;

                connectionString = containerConnection;
            }

            ConnectionString = connectionString;
            _serviceProvider = BuildServiceProvider(connectionString);

            using var scope = _serviceProvider.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();

            await TestDatabaseSchema.EnsureAsync(connectionString, ctx);
            await SeedData.EnsureBaseDataAsync(ctx);

            IsAvailable = true;
        }

        public async Task<ScenarioIds> ResetScenarioAsync(IntegrationScenario scenario)
        {
            using var scope = ScopeFactory.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
            return await SeedData.ResetScenarioAsync(ctx, scenario);
        }

        public async Task<ScenarioIds> PreparePendingReviewScenarioAsync(IntegrationScenario scenario)
        {
            // Reset scenario to default in-progress state, then perform a real submit as the student
            var ids = await ResetScenarioAsync(scenario);

            using var scope = ScopeFactory.CreateScope();
            var examMgr = scope.ServiceProvider.GetRequiredService<FixtureExamClient>();

            // Build submit DTO from currently persisted answers
            var repo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Repositories.IExamSubmissionRepository>();
            var submission = await repo.GetByIdAsync(ids.SubmissionId, withAnswers: true)
                ?? throw new InvalidOperationException("Failed to load submission for preparing pending review scenario.");

            var dto = new Domain.DTOs.SubmitExamDto { ClientRevision = submission.Revision + 1 };

            if (submission.Answers != null)
            {
                foreach (var a in submission.Answers)
                {
                    if (a.OpenText != null)
                    {
                        dto.Answers.Add(new Domain.DTOs.AnswerInputDto { QuestionId = a.QuestionId, OpenText = a.OpenText });
                    }
                    else if (a.SelectedOptionIds != null && a.SelectedOptionIds.Count > 0)
                    {
                        var ai = new Domain.DTOs.AnswerInputDto { QuestionId = a.QuestionId };
                        foreach (var oid in a.SelectedOptionIds) ai.SelectedOptionIds.Add(oid);
                        dto.Answers.Add(ai);
                    }
                }
            }

            // Use persisted user identity for the student actor when submitting
            // Create or reuse a per-scenario student user if needed; for now reuse the scenario StudentId as UserId when present
            var studentUserId = ids.StudentId; // student user persisted by scenario
            var studentCaller = new FixtureActor(studentUserId, ids.StudentId, null);

            await examMgr.SubmitAsync(ids.SubmissionId, dto, studentCaller);

            return ids;
        }

        public async Task DisposeAsync()
        {
            if (_serviceProvider != null)
                await _serviceProvider.DisposeAsync();

            if (_mysql != null)
                await _mysql.DisposeAsync();
        }

        internal static string ValidateLocalConnectionString(string connectionString)
        {
            MySqlConnectionStringBuilder builder;
            try
            {
                builder = new MySqlConnectionStringBuilder(connectionString);
            }
            catch (ArgumentException ex)
            {
                throw new InvalidOperationException($"{ConnectionEnvVar} non contiene una connection string MySQL valida.", ex);
            }

            var database = builder.Database;

            // Local mode is reserved for the explicitly provisioned isolated instance.
            // Container mode obtains its connection independently and never enters here.
            var isolated = uint.TryParse(Environment.GetEnvironmentVariable("ER_TEST_ISOLATED_PORT"), out var port) &&
                port is >= 1024 and <= 65535 and not 3306 and not 13306 && builder.Port == port &&
                !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ER_TEST_ISOLATED_DATADIR"));
            if (builder.Server != "127.0.0.1" || builder.Port != 13306 && !isolated)
                throw new InvalidOperationException("Local integration tests require 127.0.0.1:13306; no fallback is allowed.");

            if (string.IsNullOrWhiteSpace(database))
                throw new InvalidOperationException(
                    $"{ConnectionEnvVar} non specifica un database. È consentito esclusivamente Database={AllowedLocalDatabase}.");

            if (!string.Equals(database, AllowedLocalDatabase, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"{ConnectionEnvVar} punta al database '{database}', che non è autorizzato per i test d'integrazione. " +
                    $"È consentito esclusivamente Database={AllowedLocalDatabase}.");

            return builder.ConnectionString;
        }

        private static async Task VerifyIsolatedIdentityAsync(string connectionString)
        {
            var builder = new MySqlConnectionStringBuilder(connectionString);
            if (builder.Port == 13306) return;
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "migrations", "bootstrap", "001_schema.sql"))) root = root.Parent;
            var expected = Path.GetFullPath(Environment.GetEnvironmentVariable("ER_TEST_ISOLATED_DATADIR")!).TrimEnd('\\', '/');
            if (root is null || !expected.StartsWith(Path.Combine(root.FullName, ".staff-private") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Isolated datadir must be inside workspace .staff-private.");
            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand("SELECT VERSION(),@@port,@@datadir", connection);
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync() || reader.GetString(0) != "5.7.24" || reader.GetUInt32(1) != builder.Port ||
                !string.Equals(Path.GetFullPath(reader.GetString(2)).TrimEnd('\\', '/'), expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Isolated MySQL identity rejected before schema/seed.");
        }

        private async Task<string?> TryStartContainerAsync()
        {
            try
            {
                _mysql = new MySqlBuilder(ContainerImage)
                    .WithDatabase("testdb")
                    .WithUsername("test")
                    .WithPassword("test")
                    .Build();

                await _mysql.StartAsync();
                return _mysql.GetConnectionString();
            }
            catch (DockerUnavailableException ex)
            {
                // Only a missing Docker engine turns into skipped tests; any other container failure propagates.
                if (_mysql != null)
                {
                    await _mysql.DisposeAsync();
                    _mysql = null;
                }

                UnavailableReason = $"Docker non disponibile e {ConnectionEnvVar} non impostata: {ex.Message}";
                return null;
            }
        }

        private static ServiceProvider BuildServiceProvider(string connectionString)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new[] { new KeyValuePair<string, string?>("ConnectionStrings:DefaultConnection", connectionString) })
                .Build();

            var services = new ServiceCollection();
            services.AddInfrastructure(configuration);
            services.AddBusinessServices();
            services.AddApplicationManagers();
        FixtureTenant.Register(services);

            return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        }
    }
}
