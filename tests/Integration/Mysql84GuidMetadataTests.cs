using System;
using System.Reflection;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Infrastructure.Persistence.Migrations;
using MySqlConnector;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public sealed class Mysql84GuidMetadataTests(IntegrationFixture fixture)
{
    private static Task Verify(MySqlConnection connection) => (Task)typeof(Mt3aOwnershipMigration)
        .GetMethod("VerifyGuidTypesAsync", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [connection])!;

    [Fact]
    public async Task Real84SingletonIntAndAsciiGuidColumnsAreAccepted()
    {
        await using var c = new MySqlConnection(fixture.ConnectionString); await c.OpenAsync();
        Assert.True(await Mysql84FixtureTarget.VerifyAsync(c));
        await using var cmd = new MySqlCommand("SELECT COLUMN_TYPE FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='institution_email_mutex' AND COLUMN_NAME='id'", c);
        Assert.Equal("int", await cmd.ExecuteScalarAsync());
        await Verify(c);
    }

    [Theory]
    [InlineData("char(35) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL")]
    [InlineData("char(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL")]
    [InlineData("int NOT NULL PRIMARY KEY")]
    public async Task IncompatibleResourceIdentifierIsRejected(string declaration)
    {
        await using var c = new MySqlConnection(fixture.ConnectionString); await c.OpenAsync();
        Assert.True(await Mysql84FixtureTarget.VerifyAsync(c));
        var table = "er84_guid_probe_" + Guid.NewGuid().ToString("N");
        await using var create = new MySqlCommand($"CREATE TABLE `{table}` (id {declaration})", c);
        await create.ExecuteNonQueryAsync();
        try
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Verify(c));
            Assert.Contains("Guid types/collations differ", error.Message);
            Assert.Contains(table, error.Message);
        }
        finally
        {
            Assert.True(await Mysql84FixtureTarget.VerifyAsync(c));
            await using var drop = new MySqlCommand($"DROP TABLE `{table}`", c); await drop.ExecuteNonQueryAsync();
        }
    }
}
