using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Api.Security;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MySqlConnector;
using Xunit;
namespace ElectronicRegisterAPI.Tests.Integration;

[Collection("Mt5aIdentityOnly")]
public sealed class Mt5eIamHttpTests : IAsyncLifetime
{
    private const string Directory="/api/institution/admin/members",Password="Mt5be-synthetic-only!";
    private readonly Guid _a=Guid.NewGuid(),_b=Guid.NewGuid();
    private readonly Guid[] _users=Enumerable.Range(0,5).Select(_=>Guid.NewGuid()).ToArray();
    private readonly Guid[] _members=Enumerable.Range(0,7).Select(_=>Guid.NewGuid()).ToArray();
    private string _connection=null!;
    private Mt3cHosting.ProgramHost _host=null!;
    private string Route(int i=2)=>Directory+"/"+_members[i]+"/mutations";
    private async Task<MySqlConnection> Open()
    {
        var c=new MySqlConnection(_connection);await c.OpenAsync();
        await using var cmd=new MySqlCommand("SELECT VERSION(),@@hostname,@@port,REPLACE(@@datadir,CHAR(92),'/'),DATABASE(),SUBSTRING_INDEX(USER(),'@',1)",c);
        await using var r=await cmd.ExecuteReaderAsync();Assert.True(await r.ReadAsync());
        Assert.Equal("5.7.24",r.GetString(0));Assert.Equal(Environment.MachineName,r.GetString(1),ignoreCase:true);Assert.Equal((int)TestMySqlTarget.Port,r.GetInt32(2));
        Assert.EndsWith(TestMySqlTarget.DataDirectorySuffix,r.GetString(3),StringComparison.OrdinalIgnoreCase);
        Assert.Equal("electronic_register_test",r.GetString(4));Assert.Equal("root",r.GetString(5));return c;
    }
    private async Task Exec(string sql,params(string,object)[] args)
    {await using var c=await Open();await using var cmd=new MySqlCommand(sql,c);foreach(var(k,v)in args)cmd.Parameters.AddWithValue(k,v);await cmd.ExecuteNonQueryAsync();}
    private async Task<long> Scalar(string sql,params(string,object)[] args)
    {await using var c=await Open();await using var cmd=new MySqlCommand(sql,c);foreach(var(k,v)in args)cmd.Parameters.AddWithValue(k,v);return Convert.ToInt64(await cmd.ExecuteScalarAsync());}
    private Task<long> Generation(int i=2)=>Scalar("SELECT security_generation FROM institution_membership_security_state WHERE membership_id=@m",("@m",_members[i]));
    private Task<long> Events()=>Scalar("SELECT COUNT(*) FROM institution_membership_event WHERE institution_id IN (@a,@b)",("@a",_a),("@b",_b));
    private int UserIndex(int member)=>member switch{4=>2,5=>0,6=>3,_=>member};
    private string Email(int user)=>_users[user].ToString("N")+"@mt5e.example.test";
    public async Task InitializeAsync()
    {
        _connection=IntegrationFixture.ValidateLocalConnectionString(Environment.GetEnvironmentVariable("TEST_MYSQL_CONNECTION")??throw new InvalidOperationException("Explicit isolated database required"));
        var builder=new MySqlConnectionStringBuilder(_connection);Assert.Equal("127.0.0.1",builder.Server);Assert.Equal(TestMySqlTarget.Port,builder.Port);builder.AllowUserVariables=true;_connection=builder.ConnectionString;
        await Exec("INSERT INTO institution(id,code,name,status) VALUES(@a,@ca,'Synthetic HTTP IAM A','active'),(@b,@cb,'Synthetic HTTP IAM B','active')",("@a",_a),("@b",_b),("@ca","MT5BP_"+_a.ToString("N")),("@cb","MT5BP_"+_b.ToString("N")));
        var hash=BCrypt.Net.BCrypt.HashPassword(Password);
        for(var i=0;i<_users.Length;i++)await Exec("INSERT INTO user(id,email,password_hash,role) VALUES(@u,@email,@hash,'admin')",("@u",_users[i]),("@email",Email(i)),("@hash",hash));
        for(var i=0;i<_members.Length;i++)await Exec("INSERT INTO institution_membership(id,institution_id,user_id,status) VALUES(@m,@o,@u,'active'); INSERT INTO institution_membership_role(membership_id,role) VALUES(@m,@r)",
            ("@m",_members[i]),("@o",i>=4?_b:_a),("@u",_users[UserIndex(i)]),("@r",i is 0 or 1 or 5?"InstitutionAdmin":i is 3 or 6?"Student":"Teacher"));
        _host=await Host();
    }
    private Task<Mt3cHosting.ProgramHost> Host(Action<IServiceCollection>? configure=null)=>
        Mt3cHosting.ProgramHost.StartAsync(_connection,_a,Array.Empty<Guid>(),enabled:false,school:false,exam:false,course:false,membershipAdmin:true,membershipWrite:true,configureServices:configure);
    public async Task DisposeAsync()
    {
        if(_host is not null)await _host.DisposeAsync();if(_connection is null)return;
        await Exec("SET @er_mt5b_fixture_owners=@owners; DELETE FROM institution_membership_event WHERE institution_id IN (@a,@b); DELETE r FROM institution_membership_role r JOIN institution_membership m ON m.id=r.membership_id WHERE m.institution_id IN (@a,@b); DELETE FROM institution_membership WHERE institution_id IN (@a,@b); DELETE FROM institution WHERE id IN (@a,@b); DELETE FROM user WHERE id IN (@u0,@u1,@u2,@u3,@u4); SET @er_mt5b_fixture_owners=NULL",
            ("@owners",_a+","+_b),("@a",_a),("@b",_b),("@u0",_users[0]),("@u1",_users[1]),("@u2",_users[2]),("@u3",_users[3]),("@u4",_users[4]));
    }
    private async Task<HttpResponseMessage> Send(string path,string? token=null,object? body=null,string method="GET",Mt3cHosting.ProgramHost? host=null)
    {
        using var req=new HttpRequestMessage(new HttpMethod(method),path);
        if(token is not null)req.Headers.Authorization=new("Bearer",token);
        if(body is string raw)req.Content=new StringContent(raw,Encoding.UTF8,"application/json");
        else if(body is not null)req.Content=JsonContent.Create(body);
        return await(host??_host).Client.SendAsync(req);
    }
    private static async Task Expect(HttpResponseMessage response,HttpStatusCode status)
    {Assert.True(response.StatusCode==status,$"Expected {status}, got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");}
    private async Task<string> Ticket(int user=0)
    {
        using var r=await Send("/api/Auth/identity/login",body:new LoginDto{Email=Email(user),Password=Password},method:"POST");
        await Expect(r,HttpStatusCode.OK);return(await r.Content.ReadFromJsonAsync<IdentityTicketDto>())!.Ticket;
    }
    private async Task<string> Access(int member=0)
    {
        using var r=await Send("/api/Auth/select-institution",await Ticket(UserIndex(member)),new SelectInstitutionDto{InstitutionId=member>=4?_b:_a,MembershipId=_members[member]},"POST");
        await Expect(r,HttpStatusCode.OK);return(await r.Content.ReadFromJsonAsync<InstitutionAccessDto>())!.Token;
    }
    private async Task<JsonObject> Intent(string action="AddRole",string? role="Student",int target=2)=>new()
    { ["eventId"]=Guid.NewGuid().ToString(),["expectedRevision"]=await Generation(target),["action"]=action,["role"]=role,["reason"]="Synthetic HTTP IAM mutation" };

    [Fact]
    public async Task FourOperationsPersistAuditAndMonotoneRevocationWithoutAffectingB()
    {
        var admin=await Access();var original=await Access(2);var other=await Access(4);var b=await Generation(4);
        var actions=new[]{("AddRole","Student"),("RemoveRole","Student"),("Suspend",(string?)null),("Reactivate",(string?)null)};
        for(var i=0;i<actions.Length;i++)
        {
            using var r=await Send(Route(),admin,await Intent(actions[i].Item1,actions[i].Item2),"POST");await Expect(r,HttpStatusCode.OK);
            var result=(await r.Content.ReadFromJsonAsync<JsonObject>())!;Assert.Equal(i+2,result["revision"]!.GetValue<long>());
            Assert.Equal(i==2?"suspended":"active",result["status"]!.GetValue<string>());
            await Expect(await Send("/api/Auth/context",original),HttpStatusCode.Forbidden);
            await Expect(await Send("/api/Auth/context",other),HttpStatusCode.OK);
        }
        await Expect(await Send("/api/Auth/context",await Access(2)),HttpStatusCode.OK);
        Assert.Equal(b,await Generation(4));Assert.Equal(5,await Generation());Assert.Equal(4,await Events());
        Assert.Equal(4,await Scalar("SELECT COUNT(*) FROM institution_membership_event WHERE institution_id=@a AND actor_user_id=@u AND actor_membership_id=@actor AND target_membership_id=@target AND revision_after=revision_before+1 AND occurred_at_utc BETWEEN UTC_TIMESTAMP()-INTERVAL 2 MINUTE AND UTC_TIMESTAMP()+INTERVAL 2 MINUTE",("@a",_a),("@u",_users[0]),("@actor",_members[0]),("@target",_members[2])));
        await Expect(await Send(Directory,admin),HttpStatusCode.OK);await Expect(await Send(Directory+"/"+_members[2],admin),HttpStatusCode.OK);
        using var legacy=await Send("/api/Auth/me",admin);
        Assert.Contains(legacy.StatusCode,new[]{HttpStatusCode.Unauthorized,HttpStatusCode.Forbidden});
    }
    [Theory]
    [InlineData(2)][InlineData(3)]
    public async Task NonTenantAdminCannotWriteEvenWithGlobalAdminRole(int member)
    {await Expect(await Send(Route(),await Access(member),await Intent(),"POST"),HttpStatusCode.Forbidden);Assert.Equal(0,await Events());Assert.Equal(1,await Generation());}
    [Theory]
    [InlineData("none")][InlineData("malformed")][InlineData("selection")][InlineData("global")]
    public async Task WrongAuthenticationCannotEnterIam(string type)
    {
        string? token=type switch{"none"=>null,"malformed"=>"malformed.jwt", "selection"=>await Ticket(4),_=>new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("mt2-tests","mt2-client",new[]{new Claim("sub",_users[4].ToString()),new Claim(ClaimTypes.Role,"admin")},expires:DateTime.UtcNow.AddMinutes(5),signingCredentials:new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Mt3cHosting.Key)),SecurityAlgorithms.HmacSha256)))};
        await Expect(await Send(Route(),token,await Intent(),"POST"),HttpStatusCode.Unauthorized);Assert.Equal(0,await Events());
    }
    [Fact]
    public async Task ForeignAndAbsentTargetsAreIndistinguishableAndLeaveBothTenantsUntouched()
    {
        var token=await Access();var body=await Intent();
        using var foreign=await Send(Route(4),token,body,"POST");using var absent=await Send(Directory+"/"+Guid.NewGuid()+"/mutations",token,body,"POST");
        await Expect(foreign,HttpStatusCode.NotFound);await Expect(absent,HttpStatusCode.NotFound);Assert.Equal(await foreign.Content.ReadAsStringAsync(),await absent.Content.ReadAsStringAsync());
        Assert.Equal(0,await Events());Assert.Equal(1,await Generation());Assert.Equal(1,await Generation(4));
    }
    [Fact]
    public async Task IdempotenceReturnsHistoricalBodyAndConflictsNeverWrite()
    {
        var token=await Access();var body=await Intent();using var first=await Send(Route(),token,body,"POST");await Expect(first,HttpStatusCode.OK);
        using var change=await Send(Route(),token,await Intent("Suspend",null),"POST");await Expect(change,HttpStatusCode.OK);
        using var replay=await Send(Route(),token,body,"POST");await Expect(replay,HttpStatusCode.OK);Assert.Equal(await first.Content.ReadAsStringAsync(),await replay.Content.ReadAsStringAsync());
        body["reason"]="Different intention";await Expect(await Send(Route(),token,body,"POST"),HttpStatusCode.Conflict);
        var stale=await Intent("Reactivate",null);stale["expectedRevision"]=0;
        await Expect(await Send(Route(),token,stale,"POST"),HttpStatusCode.Conflict);
        Assert.Equal(2,await Events());Assert.Equal(3,await Generation());
    }
    [Fact]
    public async Task ConcurrentIdenticalEventsApplyExactlyOnce()
    {
        var token=await Access();var body=(await Intent()).ToJsonString();
        var responses=await Task.WhenAll(Send(Route(),token,body,"POST"),Send(Route(),token,body,"POST"));
        foreach(var r in responses)await Expect(r,HttpStatusCode.OK);
        Assert.Equal(await responses[0].Content.ReadAsStringAsync(),await responses[1].Content.ReadAsStringAsync());Assert.Equal(1,await Events());Assert.Equal(2,await Generation());
    }
    [Fact]
    public async Task RevokedAdminCannotRetryAndRestoringRoleRequiresANewJwt()
    {
        var original=await Access();var peer=await Access(1);
        await Expect(await Send(Route(0),peer,await Intent("RemoveRole","InstitutionAdmin",0),"POST"),HttpStatusCode.OK);
        await Expect(await Send(Route(),original,await Intent(),"POST"),HttpStatusCode.Forbidden);
        await Expect(await Send(Route(0),peer,await Intent("AddRole","InstitutionAdmin",0),"POST"),HttpStatusCode.OK);
        await Expect(await Send(Route(),original,await Intent(),"POST"),HttpStatusCode.Forbidden);
        await Expect(await Send(Route(),await Access(),await Intent(),"POST"),HttpStatusCode.OK);Assert.Equal(3,await Events());
    }
    [Fact]
    public async Task MutualAdminSuspensionsLeaveOneAdminAndDenyRevokedRetry()
    {
        var a=await Access();var b=await Access(1);var ra=await Intent("Suspend",null,1);var rb=await Intent("Suspend",null,0);
        var responses=await Task.WhenAll(Send(Route(1),a,ra,"POST"),Send(Route(0),b,rb,"POST"));
        Assert.Equal(1,responses.Count(r=>r.StatusCode==HttpStatusCode.OK));Assert.Equal(1,responses.Count(r=>r.StatusCode==HttpStatusCode.Forbidden));Assert.Equal(1,await Events());
        Assert.Equal(1,await Scalar("SELECT COUNT(*) FROM institution_membership m JOIN institution_membership_role r ON r.membership_id=m.id JOIN user u ON u.id=m.user_id WHERE m.institution_id=@a AND m.status='active' AND r.role='InstitutionAdmin'",("@a",_a)));
        var winner=responses[0].IsSuccessStatusCode?0:1;
        // A different administrator has been revoked, so neither old context nor replay can authorize it.
        await Expect(await Send(winner==0?Route(0):Route(1),winner==0?b:a,winner==0?rb:ra,"POST"),HttpStatusCode.Forbidden);
    }
    [Theory]
    [InlineData("AddRole","Teacher")][InlineData("RemoveRole","InstitutionAdmin")][InlineData("Suspend",null)]
    public async Task SoleAdminAndSelfAdministrationAreProtected(string action,string? role)
    {
        await Exec("DELETE FROM institution_membership_role WHERE membership_id=@m AND role='InstitutionAdmin'",("@m",_members[1]));
        var token=await Access();await Expect(await Send(Route(0),token,await Intent(action,role,0),"POST"),HttpStatusCode.Forbidden);
        Assert.Equal(0,await Events());Assert.Equal(1,await Generation(0));
    }
    [Theory]
    [InlineData("AddRole","Teacher")][InlineData("RemoveRole","Student")][InlineData("Reactivate",null)]
    public async Task NoopIsConflictWithoutAuditOrGeneration(string action,string? role)
    {await Expect(await Send(Route(),await Access(),await Intent(action,role),"POST"),HttpStatusCode.Conflict);Assert.Equal(0,await Events());Assert.Equal(1,await Generation());}
    [Theory]
    [InlineData("event-missing")][InlineData("event-empty")][InlineData("revision-missing")][InlineData("revision-negative")][InlineData("revision-fraction")][InlineData("revision-range")]
    [InlineData("action-missing")][InlineData("action-unknown")][InlineData("action-numeric")][InlineData("action-numeric-string")][InlineData("action-case")]
    [InlineData("role-missing")][InlineData("role-unknown")][InlineData("role-numeric")][InlineData("role-case")][InlineData("role-incompatible")]
    [InlineData("reason-missing")][InlineData("reason-null")][InlineData("reason-empty")][InlineData("reason-long")][InlineData("reason-control")]
    [InlineData("institutionId")][InlineData("actorUserId")][InlineData("membershipId")][InlineData("securityGeneration")][InlineData("json")][InlineData("null")]
    public async Task InvalidBodiesAre400AndNeverWrite(string fault)
    {
        var b=await Intent();string? raw=null;
        switch(fault)
        {
            case "event-missing":b.Remove("eventId");break;case "event-empty":b["eventId"]=Guid.Empty.ToString();break;
            case "revision-missing":b.Remove("expectedRevision");break;case "revision-negative":b["expectedRevision"]=-1;break;
            case "revision-fraction":b["expectedRevision"]=0.5;break;case "revision-range":b["expectedRevision"]=JsonNode.Parse("9223372036854775808");break;
            case "action-missing":b.Remove("action");break;case "action-unknown":b["action"]="CreateMembership";break;
            case "action-numeric":b["action"]=0;break;case "action-numeric-string":b["action"]="0";break;case "action-case":b["action"]="addrole";break;
            case "role-missing":b.Remove("role");break;case "role-unknown":b["role"]="admin";break;case "role-numeric":b["role"]=0;break;case "role-case":b["role"]="teacher";break;
            case "role-incompatible":b["action"]="Suspend";break;case "reason-missing":b.Remove("reason");break;case "reason-null":b["reason"]=null;break;
            case "reason-empty":b["reason"]=" ";break;case "reason-long":b["reason"]=new string('x',501);break;case "reason-control":b["reason"]="reason\n";break;
            case "json":raw="{";break;case "null":raw="null";break;default:b[fault]=Guid.NewGuid().ToString();break;
        }
        await Expect(await Send(Route(),await Access(),raw??b.ToJsonString(),"POST"),HttpStatusCode.BadRequest);Assert.Equal(0,await Events());Assert.Equal(1,await Generation());
    }
    [Theory]
    [InlineData(false,null)][InlineData(false,"false")][InlineData(false,"invalid")]
    [InlineData(true,null)][InlineData(true,"false")][InlineData(true,"invalid")]
    public async Task BothRolloutFlagsAreRequiredAndGetRemainsReadOnly(bool directory,string? value)
    {
        var token=await Access();var cfg=_host.Host.Services.GetRequiredService<IConfiguration>();
        cfg[directory?InstitutionMembershipAdminEndpointAttribute.RolloutKey:InstitutionMembershipWriteEndpointAttribute.RolloutKey]=value;
        await Expect(await Send(Route(),token,await Intent(),"POST"),HttpStatusCode.Forbidden);
        await Expect(await Send(Directory,token),directory?HttpStatusCode.Forbidden:HttpStatusCode.OK);Assert.Equal(0,await Events());
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task RealHttpErrorsRollBackBeforeAndAfterAuditAndStayOpaque(bool afterAppend)
    {
        var fault=new HttpAuditFault(afterAppend);
        await using var host=await Host(services=>services.AddDbContext<ElectronicRegisterContext>(o=>o.AddInterceptors(fault)));
        var token=await Access();using var response=await Send(Route(),token,await Intent(),"POST",host);
        await Expect(response,HttpStatusCode.InternalServerError);Assert.True(fault.Reached);
        var text=await response.Content.ReadAsStringAsync();Assert.DoesNotContain("synthetic-sensitive",text);Assert.DoesNotContain("INSERT",text);Assert.DoesNotContain("stack",text,StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0,await Events());Assert.Equal(1,await Generation());
        Assert.Equal(0,await Scalar("SELECT COUNT(*) FROM institution_membership_role WHERE membership_id=@m AND role='Student'",("@m",_members[2])));
    }
    private sealed class HttpAuditFault(bool after):DbCommandInterceptor
    {
        internal bool Reached { get; private set; }
        private static bool IsAudit(DbCommand c)=>c.CommandText.StartsWith("INSERT INTO institution_membership_event",StringComparison.Ordinal);
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,CommandEventData data,InterceptionResult<int> result,CancellationToken ct=default)
        {if(!after&&IsAudit(command)){Reached=true;throw new InvalidOperationException("synthetic-sensitive SQL before append");}return ValueTask.FromResult(result);}
        public override ValueTask<int> NonQueryExecutedAsync(DbCommand command,CommandExecutedEventData data,int result,CancellationToken ct=default)
        {if(after&&IsAudit(command)){Assert.Equal(1,result);Reached=true;throw new InvalidOperationException("synthetic-sensitive SQL after append");}return ValueTask.FromResult(result);}
    }
}
