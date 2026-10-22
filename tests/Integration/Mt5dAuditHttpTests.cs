using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace ElectronicRegisterAPI.Tests.Integration;

public sealed partial class Mt5cOnboardingTests
{
    private async Task<(PlatformResult Owner,Guid Target,string AdminToken,Guid AdminMember)> AuditFixture()
    {
        var a=await Active();var invite=Guid.NewGuid();
        await Expect(await Http("/api/institution/admin/invitations",a.Token,new{EventId=invite,RecipientUserId=users[1],Roles=new[]{"Teacher"},Reason="Synthetic audited nomination"}),HttpStatusCode.OK);
        var accepted=await Accept(new(a.P.InstitutionId,invite,"pending"),1);
        return(a.P,accepted.MembershipId!.Value,a.Token,a.Member);
    }
    [Fact]
    public async Task Governance_AuditHttpIsOwnerFilteredPaginatedAndContainsOnlyIamProjection()
    {
        var a=await AuditFixture();var b=await Active(2);var eventId=Guid.NewGuid();
        await Expect(await Http($"/api/institution/admin/members/{a.Target}/mutations",a.AdminToken,new{EventId=eventId,ExpectedRevision=1,Action="AddRole",Role="Student",Reason="Synthetic audit read test"}),HttpStatusCode.OK);
        using var list=await Http("/api/institution/admin/audit?page=1&size=1&action=AddRole&target="+a.Target,a.AdminToken);await Expect(list,HttpStatusCode.OK);
        var rows=(await list.Content.ReadFromJsonAsync<MembershipAuditEntry[]>())!;Assert.Single(rows);Assert.Equal(eventId,rows[0].EventId);Assert.Equal(users[0],rows[0].ActorUserId);Assert.Equal(a.Target,rows[0].TargetMembershipId);Assert.Equal(new[]{"Teacher"},rows[0].PreviousRoles);Assert.Contains("Student",rows[0].NewRoles);
        using var detail=await Http("/api/institution/admin/audit/"+eventId,a.AdminToken);await Expect(detail,HttpStatusCode.OK);var body=await detail.Content.ReadAsStringAsync();
        foreach(var sensitive in new[]{"requestHash","intentHash","signature","tokenHash","challenge","operatorId","password"})Assert.DoesNotContain(sensitive,body,StringComparison.OrdinalIgnoreCase);
        await Expect(await Http("/api/institution/admin/audit/"+eventId,b.Token),HttpStatusCode.NotFound);
        await Expect(await Http("/api/institution/admin/audit/"+Guid.NewGuid(),b.Token),HttpStatusCode.NotFound);
        using var other=await Http("/api/institution/admin/audit",b.Token);await Expect(other,HttpStatusCode.OK);Assert.Empty((await other.Content.ReadFromJsonAsync<MembershipAuditEntry[]>())!);
        using var next=await Http("/api/institution/admin/audit?page=2&size=1",a.AdminToken);await Expect(next,HttpStatusCode.OK);Assert.Empty((await next.Content.ReadFromJsonAsync<MembershipAuditEntry[]>())!);
    }
    [Fact]
    public async Task Governance_AuditHttpRejectsTeacherAndRevalidatesRevokedAdminJwt()
    {
        var a=await AuditFixture();var teacher=await Access(a.Owner.InstitutionId,a.Target,1);
        await Expect(await Http("/api/institution/admin/audit",teacher),HttpStatusCode.Forbidden);
        await Expect(await Http("/api/institution/admin/audit"),HttpStatusCode.Unauthorized);
        await Expect(await Http("/api/institution/admin/audit",await Ticket()),HttpStatusCode.Unauthorized);
        await Expect(await Http($"/api/institution/admin/members/{a.Target}/mutations",a.AdminToken,new{EventId=Guid.NewGuid(),ExpectedRevision=1,Action="AddRole",Role="InstitutionAdmin",Reason="Synthetic administrator audit permission"}),HttpStatusCode.OK);
        var admin=await Access(a.Owner.InstitutionId,a.Target,1);await Expect(await Http("/api/institution/admin/audit",admin),HttpStatusCode.OK);
        await Expect(await Http($"/api/institution/admin/members/{a.Target}/mutations",a.AdminToken,new{EventId=Guid.NewGuid(),ExpectedRevision=2,Action="RemoveRole",Role="InstitutionAdmin",Reason="Synthetic permission revocation"}),HttpStatusCode.OK);
        await Expect(await Http("/api/institution/admin/audit",admin),HttpStatusCode.Forbidden);
        await Expect(await Http("/api/institution/admin/audit",teacher),HttpStatusCode.Forbidden);
    }
    [Theory][InlineData("page=0")][InlineData("size=101")][InlineData("action=bad")][InlineData("action=")][InlineData("target=not-a-guid")]
    public async Task Governance_AuditHttpRejectsMalformedFiltersWithoutChangingData(string query)
    {
        var a=await Active();await Expect(await Http("/api/institution/admin/audit?"+query,a.Token),HttpStatusCode.BadRequest);
        Assert.Equal(0,await Count("SELECT COUNT(*) FROM institution_membership_event WHERE institution_id=@p0",a.P.InstitutionId));
    }

    [Fact]
    public async Task Governance_AuditRolloutIsClosedAndNoAuditMutationRouteExists()
    {
        var a=await Active();
        await Expect(await Http("/api/institution/admin/audit",a.Token,new{}),HttpStatusCode.MethodNotAllowed);
        host.Host.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()["Multitenancy:MembershipAdminApi:Enabled"]="false";
        await Expect(await Http("/api/institution/admin/audit",a.Token),HttpStatusCode.Forbidden);
        await Expect(await Http("/api/institution/admin/audit/"+Guid.NewGuid(),a.Token),HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Governance_AuditRepositoryMasksRowsIfAuthorizationWasRevokedAfterTheManagerCheck()
    {
        var a=await AuditFixture();var eventId=Guid.NewGuid();
        await Expect(await Http($"/api/institution/admin/members/{a.Target}/mutations",a.AdminToken,new{EventId=eventId,ExpectedRevision=1,Action="AddRole",Role="Student",Reason="Audit confidentiality race fixture"}),HttpStatusCode.OK);
        var invite=Guid.NewGuid();await Expect(await Http("/api/institution/admin/invitations",a.AdminToken,new{EventId=invite,RecipientUserId=users[2],Roles=new[]{"InstitutionAdmin"},Reason="Second valid administrator"}),HttpStatusCode.OK);
        var admin2=await Accept(new(a.Owner.InstitutionId,invite,"pending"),2);var token2=await Access(a.Owner.InstitutionId,admin2.MembershipId!.Value,2);
        var actor=new TenantContext(users[0],a.Owner.InstitutionId,a.AdminMember,new[]{ElectronicRegisterAPI.Domain.Enums.InstitutionRole.InstitutionAdmin},1);
        await using var scope=host.Host.Services.CreateAsyncScope();var repository=scope.ServiceProvider.GetRequiredService<IMembershipAuditRepository>();
        using var capture=new AuditReadCapture();using var subscription=System.Diagnostics.DiagnosticListener.AllListeners.Subscribe(capture);
        Assert.Single(await repository.ListAsync(actor,1,20,null,null,System.Threading.CancellationToken.None));
        var sql=capture.Commands.Last(x=>x.Contains("institution_membership_event",StringComparison.Ordinal));
        Assert.Contains("EXISTS",sql,StringComparison.OrdinalIgnoreCase);Assert.Contains("security_generation",sql,StringComparison.Ordinal);
        Assert.DoesNotContain("request_hash",sql,StringComparison.Ordinal);
        await Expect(await Http($"/api/institution/admin/members/{a.AdminMember}/mutations",token2,new{EventId=Guid.NewGuid(),ExpectedRevision=1,Action="Suspend",Reason="Authorized revocation between read checks"}),HttpStatusCode.OK);
        Assert.Empty(await repository.ListAsync(actor,1,20,null,null,System.Threading.CancellationToken.None));
        Assert.Null(await repository.GetAsync(actor,eventId,System.Threading.CancellationToken.None));
    }

    private sealed class AuditReadCapture : IObserver<System.Diagnostics.DiagnosticListener>,IObserver<System.Collections.Generic.KeyValuePair<string,object?>>,IDisposable
    {
        private readonly System.Collections.Generic.List<IDisposable> subscriptions=[];
        internal readonly System.Collections.Concurrent.ConcurrentQueue<string> Commands=new();
        public void OnNext(System.Diagnostics.DiagnosticListener listener){if(listener.Name=="Microsoft.EntityFrameworkCore")subscriptions.Add(listener.Subscribe(this));}
        public void OnNext(System.Collections.Generic.KeyValuePair<string,object?> value)
        {if(value.Value is Microsoft.EntityFrameworkCore.Diagnostics.CommandExecutedEventData data && data.Command.CommandText.TrimStart().StartsWith("SELECT",StringComparison.OrdinalIgnoreCase))Commands.Enqueue(data.Command.CommandText);}
        public void OnCompleted(){}
        public void OnError(Exception error)=>throw error;
        public void Dispose(){foreach(var item in subscriptions)item.Dispose();}
    }
}
