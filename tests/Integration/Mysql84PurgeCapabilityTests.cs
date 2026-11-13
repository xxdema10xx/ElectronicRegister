using System;
using System.Threading.Tasks;
using MySqlConnector;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public sealed class Mysql84PurgeCapabilityTests(IntegrationFixture fixture)
{
    [Theory]
    [InlineData("127.0.0.1",3307,"electronic_register")]
    [InlineData("127.0.0.1",13384,"electronic_register_test")]
    [InlineData("ec2.invalid",3306,"electronic_register_test")]
    public async Task UnownedTargetsAreRefusedBeforeAnyConnection(string host,uint port,string database)
    {
        await using var c=new MySqlConnection(new MySqlConnectionStringBuilder{Server=host,Port=port,Database=database,UserID="root"}.ConnectionString);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>MembershipFixturePurge.AllowAsync(c,[Guid.NewGuid()]));
        Assert.Equal(System.Data.ConnectionState.Closed,c.State);
    }

    [Fact]
    public async Task RootNeedsTheExactCapabilityAndExplicitOwnerAndRestoresScope()
    {
        await using var c=new MySqlConnection(fixture.ConnectionString);await c.OpenAsync();Assert.True(await Mysql84FixtureTarget.VerifyAsync(c));
        var owner=Guid.NewGuid();var user=Guid.NewGuid();var member=Guid.NewGuid();
        async Task Execute(string sql)
        { Assert.True(await Mysql84FixtureTarget.VerifyAsync(c));await using var cmd=new MySqlCommand(sql,c);await cmd.ExecuteNonQueryAsync(); }
        await Execute($"INSERT INTO institution(id,code,name,status) VALUES('{owner}','MT2_{owner:N}','Capability test','active');INSERT INTO user(id,email,password_hash,role) VALUES('{user}','{user:N}@example.test','synthetic','admin');INSERT INTO institution_membership(id,institution_id,user_id,status) VALUES('{member}','{owner}','{user}','active')");
        var remove=$"DELETE FROM institution_membership WHERE id='{member}'";
        await Execute($"SET @er_mt5b_fixture_owners='{owner}';SET @er_mt5b_fixture_capability='{new string('0',64)}'");
        var wrong=await Assert.ThrowsAsync<MySqlException>(()=>Execute(remove));Assert.Equal(1451,wrong.Number);
        await using(var scope=await MembershipFixturePurge.AllowAsync(c,[Guid.NewGuid()]))
        { var outside=await Assert.ThrowsAsync<MySqlException>(()=>Execute(remove));Assert.Equal(1451,outside.Number); }
        var secret=await Mysql84FixtureTarget.PurgeCapabilityAsync(c);
        var apiTarget=new MySqlConnectionStringBuilder(fixture.ConnectionString){UserID="fixture_api",Password="synthetic-fixture-only"};
        await using(var api=new MySqlConnection(apiTarget.ConnectionString))
        {
            await api.OpenAsync();
            await using var attempt=new MySqlCommand($"SET @er_mt5b_fixture_owners='{owner}';SET @er_mt5b_fixture_capability=@cap;"+remove,api);
            attempt.Parameters.AddWithValue("@cap",secret);
            var denied=await Assert.ThrowsAsync<MySqlException>(()=>attempt.ExecuteNonQueryAsync());Assert.Equal(1451,denied.Number);
        }
        await using(var scope=await MembershipFixturePurge.AllowAsync(c,[owner])) await Execute(remove);
        await using var count=new MySqlCommand($"SELECT (SELECT COUNT(*) FROM institution_membership WHERE id='{member}')+(SELECT COUNT(*) FROM institution_membership_security_state WHERE membership_id='{member}')",c);
        Assert.Equal(0L,Convert.ToInt64(await count.ExecuteScalarAsync()));
        await using var capability=new MySqlCommand("SELECT @er_mt5b_fixture_capability",c);Assert.Equal(new string('0',64),await capability.ExecuteScalarAsync());
    }
}
