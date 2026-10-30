using System.Text.Json;
using ElectronicRegisterAPI.Domain.Configuration;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Unit;

public sealed class InstitutionImportFeatureAvailabilityTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, true)]
    public void WithoutCallbackRequiresBothFlags(bool enabled, bool school, bool expected)
    {
        var feature = new InstitutionImportFeature(enabled, school, new ImportParserLimits());

        Assert.Equal(expected, feature.IsEnabled);
    }

    [Fact]
    public void PureSnapshotSerializationContainsNoRuntimeCallback()
    {
        var feature = new InstitutionImportFeature(true, true, new ImportParserLimits());

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(feature));

        Assert.False(json.RootElement.TryGetProperty("CurrentAvailability", out _));
        Assert.True(json.RootElement.GetProperty("IsEnabled").GetBoolean());
        Assert.True(json.RootElement.GetProperty("Enabled").GetBoolean());
        Assert.True(json.RootElement.GetProperty("SchoolEnabled").GetBoolean());
    }
}
