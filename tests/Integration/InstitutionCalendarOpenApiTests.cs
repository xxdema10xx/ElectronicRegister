using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Xunit;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
namespace ElectronicRegisterAPI.Tests.Integration;
public sealed partial class InstitutionFormativePlanTests
{
    private sealed class CalendarOpenApiFailure:IExceptionHandler
    {
        public Exception? Failure {get;private set;}
        public ValueTask<bool> TryHandleAsync(HttpContext context,Exception exception,System.Threading.CancellationToken ct)
        {Failure=exception;return ValueTask.FromResult(false);}
    }
    [Fact] public async Task Calendar_OpenApiDeclaresTypedResponsesForTheEnabledCalendar()
    {
        var diagnostic=new CalendarOpenApiFailure();
        await using var h=await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString,tenant,Array.Empty<Guid>(),enabled:false,school:true,course:true,environment:"Development",extraArguments:new[]{"--Multitenancy:InstitutionCalendar:Enabled=true"},configureServices:s=>s.Insert(0,ServiceDescriptor.Singleton<IExceptionHandler>(diagnostic)));
        var response=await h.Client.GetAsync("/openapi/v1.json");Assert.True(response.IsSuccessStatusCode,diagnostic.Failure?.ToString()??await response.Content.ReadAsStringAsync());
        var payload=await response.Content.ReadAsStringAsync();
        if(Environment.GetEnvironmentVariable("ER_TEST_ISOLATED_DATADIR") is {} dataDirectory)
        {
            var parent=System.IO.Directory.GetParent(dataDirectory.TrimEnd('\\','/'))!.FullName;
            await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(parent,"calendar-openapi-document.json"),payload);
        }
        var doc=JsonNode.Parse(payload)!;
        var schema=doc["paths"]!["/api/institution/calendar/teachers/me"]!["get"]!["responses"]!["200"]!["content"]!["application/json"]!["schema"]!;
        if(schema["$ref"] is {} reference)schema=doc["components"]!["schemas"]![reference.GetValue<string>().Split('/')[^1]]!;
        Assert.Equal("array",schema["type"]!.GetValue<string>());
        Assert.NotNull(doc["paths"]!["/api/institution/calendar/imports/preview"]!["post"]);
    }
}
