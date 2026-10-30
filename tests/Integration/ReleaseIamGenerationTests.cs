using System;
using System.Data.Common;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.DTOs.Membership;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

public sealed partial class InstitutionClassCrudTests
{
    private async Task<string> IamSnapshot()
    {
        using var scope=fixture.ScopeFactory.CreateScope();var db=scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
        return JsonSerializer.Serialize(new {
            Members=await db.InstitutionMemberships.AsNoTracking().Where(m=>m.InstitutionId==_a).OrderBy(m=>m.Id).Select(m=>new{m.Id,m.Status}).ToListAsync(),
            States=await db.InstitutionMembershipSecurityStates.AsNoTracking().Where(s=>s.InstitutionId==_a).OrderBy(s=>s.MembershipId).ToListAsync(),
            Roles=await db.InstitutionMembershipRoles.AsNoTracking().Where(r=>db.InstitutionMemberships.Any(m=>m.Id==r.MembershipId&&m.InstitutionId==_a)).OrderBy(r=>r.MembershipId).ThenBy(r=>r.Role).Select(r=>new{r.MembershipId,r.Role}).ToListAsync(),
            Events=await db.InstitutionMembershipEvents.AsNoTracking().Where(e=>e.InstitutionId==_a).OrderBy(e=>e.Id).Select(e=>e.Id).ToListAsync(),
            Acquisitions=await db.Database.SqlQuery<Guid>($"SELECT id AS Value FROM institution_profile_acquisition WHERE institution_id={_a} ORDER BY id").ToListAsync()
        });
    }

    private async Task<(string Route,object Body)> IamIntent(string kind,Mt3cHosting.ProgramHost setup,string token)
    {
        bool replay=kind.StartsWith("replay-",StringComparison.Ordinal);kind=kind.Replace("replay-","");
        string route;object body;
        if(kind=="acquisition")
        {
            await Execute("UPDATE user SET student_id=@s WHERE id=@u",("@s",Id("student")),("@u",_studentUser));
            long revision=0;await Db(async db=>revision=await db.InstitutionMembershipSecurityStates.Where(s=>s.MembershipId==Id("student-membership")).Select(s=>s.SecurityGeneration).SingleAsync());
            route="/api/institution/admin/members/"+Id("student-membership")+"/profile-acquisitions";
            body=new AcquireHistoricalProfile(Guid.NewGuid(),revision,Id("student"),"Student","Release generation proof");
        }
        else
        {
            long revision=0;await Db(async db=>revision=await db.InstitutionMembershipSecurityStates.Where(s=>s.MembershipId==Id("teacher-membership")).Select(s=>s.SecurityGeneration).SingleAsync());
            route="/api/institution/admin/members/"+Id("teacher-membership")+"/mutations";
            body=new {EventId=Guid.NewGuid(),ExpectedRevision=revision,Action=kind=="suspend"?"Suspend":kind=="remove-role"?"RemoveRole":"AddRole",Role=kind=="suspend"?null:kind=="remove-role"?"Teacher":"Staff",Reason="Release generation proof"};
        }
        if(replay)await Expect(await Request(route,token,body,"POST",setup),HttpStatusCode.OK);
        return(route,body);
    }

    [Theory]
    [InlineData("suspend",false)][InlineData("role",false)][InlineData("acquisition",false)]
    [InlineData("replay-role",false)][InlineData("replay-acquisition",false)]
    [InlineData("suspend",true)][InlineData("role",true)][InlineData("acquisition",true)]
    [InlineData("replay-role",true)][InlineData("replay-acquisition",true)]
    [InlineData("remove-role",false)][InlineData("remove-role",true)]
    public async Task Release_PasswordRotationLinearizesAdministrativeIam(string kind,bool mutationFirst)
        => await RunIamGeneration(kind,mutationFirst,false);

    [Theory]
    [InlineData("suspend")][InlineData("role")][InlineData("acquisition")]
    [InlineData("replay-role")][InlineData("replay-acquisition")]
    [InlineData("remove-role")]
    public async Task Release_OriginalIamGenerationOmissionIsReproducedByTestOnlyMutant(string kind)
        => await RunIamGeneration(kind,false,true);

    private async Task RunIamGeneration(string kind,bool mutationFirst,bool omitGeneration)
    {
        await using var setup=await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString,_a,[],enabled:false,school:true,membershipAdmin:true,membershipWrite:true);
        var token=await Access();var ticket=await Ticket();var intent=await IamIntent(kind,setup,token);var before=await IamSnapshot();
        var reached=Phase3.Signal();var release=Phase3.Signal();
        await using var host=await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString,_a,[],enabled:false,school:true,membershipAdmin:true,membershipWrite:true,configureServices:services=>
        {
            if(!mutationFirst)services.AddDbContext<ElectronicRegisterContext>(o=>o.AddInterceptors(new IamBeforeMutex(reached,release)));
            if(mutationFirst||omitGeneration)
            {
                var d=services.Last(s=>s.ServiceType==typeof(IIdentityAccountRepository));
                services.AddScoped<IIdentityAccountRepository>(sp=>GenerationPause.Wrap((IIdentityAccountRepository)ActivatorUtilities.CreateInstance(sp,d.ImplementationType!),reached,release,omitGeneration));
            }
        });
        var mutation=SqlLockEvidence.Track(()=>Request(intent.Route,token,intent.Body,"POST",host));
        await reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var rotation=SqlLockEvidence.Track(()=>Request("/api/identity/account/password",ticket,new ChangeIdentityPasswordDto(Password,"ReleaseRotated!123"),"PUT",host));
        try
        {
            if(mutationFirst)await SqlLockEvidence.WaitAsync(fixture.ConnectionString,rotation,mutation);
            else await Expect(await rotation,HttpStatusCode.NoContent);
        }
        finally{release.TrySetResult(true);}
        await Expect(await mutation,mutationFirst||omitGeneration?HttpStatusCode.OK:HttpStatusCode.Forbidden);
        await Expect(await rotation,HttpStatusCode.NoContent);
        if(!mutationFirst&&!omitGeneration||kind.StartsWith("replay-",StringComparison.Ordinal))Assert.Equal(before,await IamSnapshot());
        else Assert.NotEqual(before,await IamSnapshot());
        await Expect(await Request(intent.Route,token,intent.Body,"POST",setup),HttpStatusCode.Unauthorized);
    }

    private sealed class IamBeforeMutex(TaskCompletionSource<bool> reached,TaskCompletionSource<bool> release):DbCommandInterceptor
    {
        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,CommandEventData data,InterceptionResult<int> result,CancellationToken ct=default)
        {
            if(command.CommandText.StartsWith("SELECT id FROM institution WHERE",StringComparison.Ordinal)&&command.CommandText.Contains("FOR UPDATE",StringComparison.Ordinal))
            {reached.TrySetResult(true);await release.Task.WaitAsync(TimeSpan.FromSeconds(15),ct);}
            return result;
        }
    }

    [Fact]
    public async Task Release_ReciprocalAcquisitionsOrderBothGlobalIdentities()
    {
        await Execute("INSERT INTO institution_membership_role(membership_id,role) VALUES(@teacher,'InstitutionAdmin'),(@admin,'Teacher')",("@teacher",Id("teacher-membership",true)),("@admin",Id("admin-membership",true)));
        long a=0,b=0;await Db(async db=>{a=await db.InstitutionMembershipSecurityStates.Where(s=>s.MembershipId==Id("teacher-membership")).Select(s=>s.SecurityGeneration).SingleAsync();b=await db.InstitutionMembershipSecurityStates.Where(s=>s.MembershipId==Id("admin-membership",true)).Select(s=>s.SecurityGeneration).SingleAsync();});
        var tokenA=await Access();var tokenB=await Access("teacher",true);
        var reached=Phase3.Signal();var release=Phase3.Signal();
        await using var host=await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString,_a,[],enabled:false,school:true,membershipAdmin:true,membershipWrite:true,configureServices:services=>
        {var d=services.Last(s=>s.ServiceType==typeof(IIdentityAccountRepository));services.AddScoped<IIdentityAccountRepository>(sp=>GenerationPause.Wrap((IIdentityAccountRepository)ActivatorUtilities.CreateInstance(sp,d.ImplementationType!),reached,release));});
        Task<System.Net.Http.HttpResponseMessage> A()=>Request("/api/institution/admin/members/"+Id("teacher-membership")+"/profile-acquisitions",tokenA,new AcquireHistoricalProfile(Guid.NewGuid(),a,Id("teacher"),"Teacher","Reciprocal identity lock A"),"POST",host);
        Task<System.Net.Http.HttpResponseMessage> B()=>Request("/api/institution/admin/members/"+Id("admin-membership",true)+"/profile-acquisitions",tokenB,new AcquireHistoricalProfile(Guid.NewGuid(),b,Id("teacher-other",true),"Teacher","Reciprocal identity lock B"),"POST",host);
        var adminFirst=StringComparer.Ordinal.Compare(_adminUser.ToString("D"),_teacherUser.ToString("D"))<0;
        var first=SqlLockEvidence.Track(()=>adminFirst?A():B());await reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var second=SqlLockEvidence.Track(()=>adminFirst?B():A());
        try{await SqlLockEvidence.WaitAsync(fixture.ConnectionString,second,first);}finally{release.TrySetResult(true);}
        await Expect(await first,HttpStatusCode.OK);await Expect(await second,HttpStatusCode.OK);
    }
    public class GenerationPause:DispatchProxy
    {
        private IIdentityAccountRepository inner=null!;private TaskCompletionSource<bool> reached=null!,release=null!;private bool omit;
        internal static IIdentityAccountRepository Wrap(IIdentityAccountRepository inner,TaskCompletionSource<bool> reached,TaskCompletionSource<bool> release,bool omit=false)
        {var p=Create<IIdentityAccountRepository,GenerationPause>();var self=(GenerationPause)(object)p;self.inner=inner;self.reached=reached;self.release=release;self.omit=omit;return p;}
        protected override object? Invoke(MethodInfo? method,object?[]? args)
        {
            if(omit&&method!.Name==nameof(IIdentityAccountRepository.RequireGenerationAsync))return Task.CompletedTask;
            var result=method!.Invoke(inner,args);
            return method.Name==nameof(IIdentityAccountRepository.RequireGenerationAsync)?Pause((Task)result!):result;
        }
        private async Task Pause(Task locked){await locked;reached.TrySetResult(true);await release.Task.WaitAsync(TimeSpan.FromSeconds(15));}
    }
}
