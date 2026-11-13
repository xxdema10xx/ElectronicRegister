using System.Threading.Tasks;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

public sealed class UnavailableFixtureGuardTests
{
    [Theory]
    [InlineData("constraints")]
    [InlineData("legacy-writer")]
    [InlineData("preflight")]
    public async Task UninitializedFixtureFailsBeforeConnectorOrHost(string path)
    {
        var fixture = new IntegrationFixture(); // Deliberately never initialize a database/container.
        Assert.False(fixture.IsAvailable);
        Assert.Empty(fixture.ConnectionString);
        var course = new Mt4aCourseStructureTests(fixture);
        var error = await Assert.ThrowsAsync<Xunit.Sdk.TrueException>(() => path switch
        {
            "constraints" => course.InformationSchemaHasCompositeRestrictConstraints(),
            "legacy-writer" => course.LegacyEfWritersStillReceiveTriggerDerivedOwners(),
            _ => new Mt3cPreflightTests(fixture).DefaultRolloutNeverOpensSchoolRoutesWithoutAuthorization()
        });
        Assert.Contains(fixture.UnavailableReason, error.Message);
        Assert.DoesNotContain("MySqlConnection.Open", error.StackTrace);
        Assert.DoesNotContain("ProgramHost.StartAsync", error.StackTrace);
    }
}
