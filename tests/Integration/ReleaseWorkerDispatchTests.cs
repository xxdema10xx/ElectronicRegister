using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Persistence;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

public sealed partial class Mt3dHttpTests
{
    [Theory]
    [InlineData("archived",false)][InlineData("suspended",false)][InlineData("holding-active",false)]
    [InlineData("archived",true)][InlineData("suspended",true)][InlineData("holding-active",true)]
    public async Task Release_WorkerDispatchRevalidatesTenantStateUnderLock(string state,bool workerFirst)
    {
        var start=await Start(await Create());await Expire(start.SubmissionId);
        Guid? oldHolding=null;
        if(state=="holding-active")
        {
            await Db(async db=>oldHolding=await db.Database.SqlQuery<Guid>($"SELECT institution_id AS Value FROM mt3a_legacy_scope WHERE scope='legacy'").SingleAsync());
            await Execute("UPDATE institution SET status='provisioning' WHERE id=@t; UPDATE mt3a_legacy_scope SET institution_id=@t WHERE scope='legacy'",("@t",_a));
        }
        var reached=Phase3.Signal();var release=Phase3.Signal();var services=Phase3.Services(fixture,null,new DispatchTenantPause(workerFirst,reached,release));
        if(!workerFirst){var d=services.Last(s=>s.ServiceType==typeof(ISubmissionExpiryStore));services.AddScoped<ISubmissionExpiryStore>(sp=>new DispatchResolvePause((ISubmissionExpiryStore)ActivatorUtilities.CreateInstance(sp,d.ImplementationType!),reached,release));}
        await using var provider=services.BuildServiceProvider();var before=await Digest(false);
        var worker=SqlLockEvidence.Track(()=>Technical(start.SubmissionId,provider:provider));
        await reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var transition=SqlLockEvidence.Track(()=>Execute("UPDATE institution SET status=@s WHERE id=@t",("@s",state=="holding-active"?"active":state),("@t",_a)));
        try
        {
            if(workerFirst)await SqlLockEvidence.WaitAsync(fixture.ConnectionString,transition,worker);
            else await transition;
        }
        finally{release.TrySetResult(true);}
        try
        {
            if(workerFirst||state=="suspended"){await worker;await AssertTechnical(start.SubmissionId);}
            else{await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>worker);Assert.Equal(before,await Digest(false));}
            await transition;
        }
        finally{if(oldHolding.HasValue)await Execute("UPDATE mt3a_legacy_scope SET institution_id=@old WHERE scope='legacy'",("@old",oldHolding.Value));}
    }

    private sealed class DispatchResolvePause(ISubmissionExpiryStore inner,TaskCompletionSource<bool> reached,TaskCompletionSource<bool> release):ISubmissionExpiryStore
    {
        public Task<List<ExpiryCandidate>> SelectAsync(DateTime now,ExpiryCandidate? after,int take,CancellationToken ct)=>inner.SelectAsync(now,after,take,ct);
        public ISubmissionExpiryRepositories Bind(SubmissionExpiryWork work,CancellationToken ct)=>inner.Bind(work,ct);
        public async Task<SubmissionExpiryWork?> ResolveAsync(Guid submission,Guid institution,CancellationToken ct){var work=await inner.ResolveAsync(submission,institution,ct);Assert.NotNull(work);reached.TrySetResult(true);await release.Task.WaitAsync(TimeSpan.FromSeconds(15),ct);return work;}
    }
    private sealed class DispatchTenantPause(bool enabled,TaskCompletionSource<bool> reached,TaskCompletionSource<bool> release):DbCommandInterceptor
    {
        public override async ValueTask<int> NonQueryExecutedAsync(DbCommand command,CommandExecutedEventData data,int result,CancellationToken ct=default)
        {if(enabled&&command.CommandText.StartsWith("SELECT id FROM institution WHERE",StringComparison.Ordinal)&&command.CommandText.Contains("LOCK IN SHARE MODE",StringComparison.Ordinal)){reached.TrySetResult(true);await release.Task.WaitAsync(TimeSpan.FromSeconds(15),ct);}return result;}
    }
}
