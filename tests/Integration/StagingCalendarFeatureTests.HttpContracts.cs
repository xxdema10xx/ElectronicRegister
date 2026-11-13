using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Api.DependencyInjection;
using ElectronicRegisterAPI.Api.Operations;
using ElectronicRegisterAPI.Api.Security;
using ElectronicRegisterAPI.Business.Services;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.DTOs.Calendar;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

public sealed partial class StagingCalendarFeatureTests
{
    private static string Python(string code)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "infra", "staging", "host.py"))) root = root.Parent;
        Assert.NotNull(root);
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("ER_TEST_PYTHON") ?? "python")
        {
            WorkingDirectory = root!.FullName, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
        };
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("import sys; sys.path.insert(0, sys.argv[1]); " + code);
        start.ArgumentList.Add(root.FullName);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        var exited = process.WaitForExit(30000);
        if (!exited) process.Kill(entireProcessTree: true);
        Assert.True(exited, "Offline Python contract export timed out");
        Assert.True(process.ExitCode == 0, error.GetAwaiter().GetResult());
        return output.GetAwaiter().GetResult();
    }

    [Fact]
    public async Task EverySmokeCaseMatchesRegisteredMvcRoutesPoliciesAndRealDtoContracts()
    {
        var runtime = Python("import json; from infra.staging.host import runtime_configuration; print(json.dumps(runtime_configuration('staging.example.invalid','172.30.252.1')))");
        var contracts = Python("import runpy; sys.argv=['smoke.py','--contracts']; runpy.run_path('infra/staging/smoke.py',run_name='__main__')");
        using var document = JsonDocument.Parse(contracts);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Staging" });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(runtime)));
        builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?>
        {
            ["Jwt:Key"] = new string('x', 64),
            ["ConnectionStrings:DefaultConnection"] = "Server=er-stage-db;Database=electronic_register_staging;User ID=er_api;Password=synthetic;SslMode=VerifyFull;SslCa=synthetic-ca.pem"
        });
        var policy = RuntimePolicy.Read(builder.Configuration, builder.Environment);
        builder.AddOperationalServices(policy);
        builder.Services.AddApiServices();
        builder.Services.AddApiControllers(builder.Configuration);
        builder.Services.AddInstitutionAuthentication(builder.Configuration);
        await using var app = builder.Build();
        app.MapControllers();
        app.MapOperationalEndpoints();
        // Materializes actual MVC endpoints and binding metadata without starting
        // hosted services, opening SQL or replacing application responses.
        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(s => s.Endpoints).OfType<RouteEndpoint>().ToArray();
        Assert.NotEmpty(endpoints);
        Assert.DoesNotContain(endpoints, e => e.Metadata.GetMetadata<ControllerActionDescriptor>()?.ControllerName == "InstitutionFormativePlan");
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        var policyProvider = app.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            var name = item.GetProperty("name").GetString()!;
            var route = item.GetProperty("route").GetString()!;
            var method = item.GetProperty("method").GetString()!;
            var matches = endpoints.Where(e => ("/" + e.RoutePattern.RawText!.TrimStart('/')).Equals(route, StringComparison.OrdinalIgnoreCase)
                && e.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(method == "OPTIONS" ? "GET" : method)).ToArray();
            if (name is "legacy-exam-retired" or "legacy-grade-retired") { Assert.Empty(matches); continue; }
            Assert.True(matches.Length == 1, $"{name}: {method} {route}: {matches.Length} registered endpoints");
            var endpoint = matches[0];
            foreach (var flag in item.GetProperty("features").EnumerateArray())
                Assert.True(builder.Configuration.GetValue<bool>($"Multitenancy:{flag.GetString()}:Enabled"), name);
            var authorization = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
            foreach (var data in authorization.Where(a => a.Policy is not null))
                Assert.NotNull(await policyProvider.GetPolicyAsync(data.Policy!));
            var actor = item.GetProperty("actor").GetString();
            if (actor is not null && item.GetProperty("expected").GetInt32() < 400 && method != "OPTIONS")
            {
                var role = actor.StartsWith("admin_") ? "InstitutionAdmin" : actor == "teacher" ? "Teacher" : "Student";
                Assert.All(authorization.Where(a => !string.IsNullOrEmpty(a.Roles)), a => Assert.Contains(role, a.Roles!.Split(',')));
            }
            if (item.TryGetProperty("payload", out var payload))
            {
                var action = endpoint.Metadata.GetRequiredMetadata<ControllerActionDescriptor>();
                var body = Assert.Single(action.Parameters.Where(p => p.BindingInfo?.BindingSource == BindingSource.Body));
                var dto = JsonSerializer.Deserialize(payload.GetRawText(), body.ParameterType, options)!;
                Assert.NotNull(dto);
                Assert.True(Validator.TryValidateObject(dto, new ValidationContext(dto), new List<ValidationResult>(), true), name);
                ValidateBusinessPayload(dto);
            }
        }
        // Sensitive POST traffic remains below the real default limiter, no relaxed policy.
        Assert.True(document.RootElement.EnumerateArray().Count(c => c.GetProperty("method").GetString() == "POST") < policy.SensitivePermitLimit);
    }

    private static void ValidateBusinessPayload(object dto)
    {
        switch (dto)
        {
            case CalendarSettingsCommand calendar:
                Assert.NotEqual(Guid.Empty, calendar.EventId);
                Assert.Equal(-1, calendar.ExpectedRevision);
                Assert.InRange(calendar.ApprovalLeadDays, 0, 366);
                Assert.False(string.IsNullOrWhiteSpace(calendar.Reason));
                _ = InstitutionCalendarRules.TimeZone(calendar.TimeZoneId);
                break;
            case CreateInstitutionClassDto created:
                _ = InstitutionClassRules.Name(created.Name);
                Assert.NotEqual(Guid.Empty, created.BienniumStudyPathId);
                break;
            case UpdateInstitutionClassDto updated:
                _ = InstitutionClassRules.Name(updated.Name);
                break;
            case CreateExamDto exam:
                var rules = new ExamService(null!); // Pure rule methods; no repository call.
                rules.EnsureValidSchedule(exam.AvailableFrom, exam.AvailableUntil, exam.DurationMinutes);
                rules.EnsureValidPassingPercent(exam.PassingPercent);
                Assert.True(exam.AvailableFrom < DateTime.UtcNow && exam.AvailableUntil > DateTime.UtcNow.AddMinutes(15));
                Assert.NotEqual(Guid.Empty, exam.ClassSubjectId);
                Assert.Single(exam.AssignedStudents);
                Assert.NotEqual(Guid.Empty, exam.AssignedStudents[0].StudentId);
                rules.EnsureValidQuestions(exam.Questions.Select(q => new Question
                {
                    Position=q.Position, Text=q.Text, Type=q.Type, Points=q.Points, Required=q.Required,
                    Options=q.Options.Select(o => new QuestionOption { Position=o.Position, Text=o.Text, IsCorrect=o.IsCorrect }).ToList()
                }).ToList());
                break;
            case SaveExamAnswersDto save:
                Assert.Equal(1, save.ClientRevision); Assert.Single(save.Answers); break;
            case SubmitExamDto submit:
                Assert.Equal(2, submit.ClientRevision); Assert.Single(submit.Answers); break;
        }
    }
}
