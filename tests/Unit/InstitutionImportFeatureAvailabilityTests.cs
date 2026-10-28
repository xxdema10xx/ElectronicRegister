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

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void CallbackOverridesSnapshotAndIsEvaluatedForEveryRead(bool snapshot, bool initialAvailability)
    {
        var available = initialAvailability;
        var calls = 0;
        var feature = new InstitutionImportFeature(snapshot, snapshot, new ImportParserLimits())
        {
            CurrentAvailability = () =>
            {
                calls++;
                return available;
            }
        };

        Assert.Equal(initialAvailability, feature.IsEnabled);
        available = !available;
        Assert.Equal(!initialAvailability, feature.IsEnabled);
        Assert.Equal(2, calls);
        feature.Validate();
        Assert.Equal(2, calls);
    }

    [Fact]
    public void CallbackIsExcludedFromJsonWhileComputedAvailabilityIsRetained()
    {
        var feature = new InstitutionImportFeature(false, false, new ImportParserLimits())
        {
            CurrentAvailability = () => true
        };

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(feature));

        Assert.False(json.RootElement.TryGetProperty("CurrentAvailability", out _));
        Assert.True(json.RootElement.GetProperty("IsEnabled").GetBoolean());
        Assert.False(json.RootElement.GetProperty("Enabled").GetBoolean());
        Assert.False(json.RootElement.GetProperty("SchoolEnabled").GetBoolean());
    }
}
