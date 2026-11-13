using System;
using System.IO;
using System.Reflection;
using System.Text;
using ElectronicRegisterAPI.Api.Controllers;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

public sealed partial class StagingCalendarFeatureTests
{
    [Fact]
    public void GeneratedStagingRuntimeKeepsCalendarControllerDiscoverable()
    {
        var json = Python("import json; from infra.staging.host import runtime_configuration; print(json.dumps(runtime_configuration('staging.example.invalid', '172.30.252.1')))");
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var configuration = new ConfigurationBuilder().AddJsonStream(stream).Build();
        var type = typeof(InstitutionCalendarController).Assembly.GetType("ElectronicRegisterAPI.Api.Security.CalendarControllerFeatureProvider", throwOnError: true)!;
        var provider = (IApplicationFeatureProvider<ControllerFeature>)Activator.CreateInstance(type, configuration)!;
        void Check(bool retained)
        {
            var feature = new ControllerFeature();
            feature.Controllers.Add(typeof(InstitutionCalendarController).GetTypeInfo());
            provider.PopulateFeature(Array.Empty<ApplicationPart>(), feature);
            Assert.Equal(retained, feature.Controllers.Contains(typeof(InstitutionCalendarController).GetTypeInfo()));
        }
        Check(true);
        // Negative controls prove this test exercises the real fail-closed gate.
        configuration["Multitenancy:InstitutionCalendar:Enabled"] = "false";
        Check(false);
        configuration["Multitenancy:InstitutionCalendar:Enabled"] = null;
        Check(false);
    }
}
