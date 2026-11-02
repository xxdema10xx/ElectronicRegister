using System;
using System.Reflection;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Infrastructure.Persistence.Migrations;
using MySqlConnector;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public sealed class Mysql84ForeignKeyMetadataTests(IntegrationFixture fixture)
{
    private static Task<string[]> Verify(MySqlConnection c, string name) => (Task<string[]>)typeof(Mt3aOwnershipMigration).Assembly
        .GetType("ElectronicRegisterAPI.Infrastructure.Persistence.Migrations." + name, true)!
        .GetMethod("ValidateIfPresentAsync", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [c, null])!;

    [Theory]
    [InlineData("Mt5bSecurityStateIntegrity")]
    [InlineData("Mt5dMembershipEventIntegrity")]
    [InlineData("Mt5GovernanceIntegrity")]
    [InlineData("InstitutionClassIntegrity")]
    [InlineData("InstitutionAdministrationIntegrity")]
    [InlineData("StudentClassTransferIntegrity")]
    public async Task Exact84ImplicitRulesAndIntactGraphAreAccepted(string validator)
    {
        await using var c = new MySqlConnection(fixture.ConnectionString); await c.OpenAsync();
        Assert.True(await Mysql84FixtureTarget.VerifyAsync(c));
        Assert.NotEmpty(await Verify(c, validator));
    }

    [Theory]
    [InlineData("RESTRICT")]
    [InlineData("CASCADE")]
    public async Task ExplicitDifferentForeignKeyActionsRemainRejected(string action)
    {
        await using var c = new MySqlConnection(fixture.ConnectionString); await c.OpenAsync();
        Assert.True(await Mysql84FixtureTarget.VerifyAsync(c));
        Assert.NotEmpty(await Verify(c, "Mt5bSecurityStateIntegrity"));
        const string drop = "ALTER TABLE institution_membership_security_state DROP FOREIGN KEY fk_membership_security_owner";
        const string restore = "ALTER TABLE institution_membership_security_state ADD CONSTRAINT fk_membership_security_owner FOREIGN KEY (institution_id,membership_id) REFERENCES institution_membership(institution_id,id)";
        async Task Execute(string sql)
        {
            Assert.True(await Mysql84FixtureTarget.VerifyAsync(c));
            await using var command = new MySqlCommand(sql, c); await command.ExecuteNonQueryAsync();
        }
        // MySQL rejects reusing a dropped FK name in the same ALTER. This
        // fixture has no concurrent writer; each DDL checks the owned identity.
        await Execute(drop);
        try
        {
            await Execute(restore + " ON UPDATE " + action + " ON DELETE " + action);
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Verify(c, "Mt5bSecurityStateIntegrity"));
            Assert.Contains("Invalid MT5B security ownership/inventory", error.Message);
        }
        finally
        {
            await using var count = new MySqlCommand("SELECT COUNT(*) FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND CONSTRAINT_NAME='fk_membership_security_owner'", c);
            if(Convert.ToInt32(await count.ExecuteScalarAsync()) == 1) await Execute(drop);
            await Execute(restore);
        }
        Assert.NotEmpty(await Verify(c, "Mt5bSecurityStateIntegrity"));
    }
}
