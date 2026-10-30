using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using ElectronicRegisterAPI.Domain.Configuration;
using ElectronicRegisterAPI.Infrastructure.Services;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

public sealed class RuntimeFeatureAvailabilityTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void CallbackOverridesSnapshotAndIsEvaluatedForEveryRead(bool snapshot, bool initialAvailability)
    {
        var available = initialAvailability;
        var calls = 0;
        var settings = new InstitutionImportFeature(snapshot, snapshot, new ImportParserLimits());
        var feature = new InstitutionImportRuntimeFeature(settings, new RuntimeFeatureAvailability(() =>
            {
                calls++;
                return available;
            }));

        Assert.Equal(initialAvailability, feature.IsEnabled);
        available = !available;
        Assert.Equal(!initialAvailability, feature.IsEnabled);
        Assert.Equal(2, calls);
        new InstitutionImportFeature(snapshot, snapshot, new ImportParserLimits()).Validate();
        Assert.Equal(2, calls);
    }


    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CalendarAndPlanObserveEveryRuntimeChange(bool initial)
    {
        var current = initial;
        var calendar = new CalendarFeature(() => current);
        var plan = new FormativePlanFeature(() => current);
        Assert.Equal(initial, calendar.Enabled);
        Assert.Equal(initial, plan.Enabled);
        current = !current;
        Assert.Equal(!initial, calendar.Enabled);
        Assert.Equal(!initial, plan.Enabled);
    }
    [Theory]
    [InlineData(null)]
    [InlineData("invalid")]
    [InlineData("false")]
    public void RegisteredPortsFailClosedAndReReadConfiguration(string? closed)
    {
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new System.Collections.Generic.Dictionary<string, string?>()).Build();
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        ElectronicRegisterAPI.Infrastructure.DependencyInjection.ServiceCollectionExtensions.AddInfrastructure(services, configuration);
        using var provider = services.BuildServiceProvider();
        var calendar = provider.GetRequiredService<ElectronicRegisterAPI.Domain.Interfaces.Services.ICalendarFeature>();
        var plan = provider.GetRequiredService<ElectronicRegisterAPI.Domain.Interfaces.Services.IFormativePlanFeature>();
        var import = provider.GetRequiredService<ElectronicRegisterAPI.Domain.Interfaces.Services.IInstitutionImportFeature>();
        Assert.False(calendar.Enabled); Assert.False(plan.Enabled); Assert.False(import.IsEnabled);
        foreach (var key in new[] { "SchoolApi", "CourseApi", "InstitutionCalendar", "FormativePlans", "InstitutionImport" })
            configuration["Multitenancy:" + key + ":Enabled"] = "true";
        Assert.True(calendar.Enabled); Assert.True(plan.Enabled); Assert.True(import.IsEnabled);
        configuration["Multitenancy:SchoolApi:Enabled"] = closed;
        Assert.False(calendar.Enabled); Assert.False(plan.Enabled); Assert.False(import.IsEnabled);
        configuration["Multitenancy:SchoolApi:Enabled"] = "true";
        configuration["Multitenancy:CourseApi:Enabled"] = closed;
        Assert.True(calendar.Enabled); Assert.False(plan.Enabled); Assert.True(import.IsEnabled);
    }    [Fact]
    public void RuntimeAvailabilityCannotMutateOrLeakIntoSerializedSnapshot()
    {
        var settings = new InstitutionImportFeature(false, false, new ImportParserLimits());
        var runtime = new InstitutionImportRuntimeFeature(settings, new RuntimeFeatureAvailability(() => true));
        Assert.True(runtime.IsEnabled);
        using var json = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(runtime.Settings));
        Assert.False(json.RootElement.TryGetProperty("CurrentAvailability", out _));
        Assert.False(json.RootElement.GetProperty("Enabled").GetBoolean());
        Assert.False(json.RootElement.GetProperty("SchoolEnabled").GetBoolean());
        Assert.False(json.RootElement.GetProperty("IsEnabled").GetBoolean());
        Assert.Same(settings, runtime.Settings);
    }}
