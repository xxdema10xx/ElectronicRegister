using System;
using System.Threading.Tasks;
using MySqlConnector;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public sealed class ReleaseLockObserverTests(IntegrationFixture fixture)
{
    [Theory]
    [InlineData(true)][InlineData(false)]
    public async Task Release_ObserverRejectsThirdWaiterAndCompletedContender(bool completed)
    {
        await using var blocker=new MySqlConnection(fixture.ConnectionString);await blocker.OpenAsync();
        await using var tx=await blocker.BeginTransactionAsync();
        await using(var cmd=new MySqlCommand("SELECT version FROM er_schema_release WHERE version='er-quality-1' FOR UPDATE",blocker,tx))await cmd.ExecuteScalarAsync();
        await using var third=new MySqlConnection(fixture.ConnectionString);await third.OpenAsync();
        await using var waitCommand=new MySqlCommand("SELECT version FROM er_schema_release WHERE version='er-quality-1' FOR UPDATE",third);
        var thirdTask=SqlLockEvidence.Track(third,()=>waitCommand.ExecuteScalarAsync());
        var release=Phase3.Signal();await using var innocent=new MySqlConnection(fixture.ConnectionString);await innocent.OpenAsync();
        var contender=SqlLockEvidence.Track(innocent,async()=>{if(!completed)await release.Task;return 1;});
        try
        {
            await SqlLockEvidence.WaitAsync(fixture.ConnectionString,thirdTask,blocker);
            // A third edge is already proven. It must not satisfy this pending
            // operation; the observer's watchdog only bounds the negative check.
            if(!completed){await Assert.ThrowsAnyAsync<Xunit.Sdk.XunitException>(()=>SqlLockEvidence.WaitAsync(fixture.ConnectionString,contender,blocker));release.TrySetResult(true);}
            await contender;
            await Assert.ThrowsAnyAsync<Xunit.Sdk.XunitException>(()=>SqlLockEvidence.WaitAsync(fixture.ConnectionString,contender,blocker));
            await Assert.ThrowsAnyAsync<Xunit.Sdk.XunitException>(()=>SqlLockEvidence.WaitAsync(fixture.ConnectionString,Task.CompletedTask,blocker.ServerThread));
        }
        finally{release.TrySetResult(true);await tx.RollbackAsync();await thirdTask;}
    }
}
