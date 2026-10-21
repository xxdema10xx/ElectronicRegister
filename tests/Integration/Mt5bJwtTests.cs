using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Infrastructure.Security;
using Microsoft.IdentityModel.Tokens;
using MySqlConnector;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

[Collection("Mt5aIdentityOnly")]
public sealed class Mt5bJwtTests : IAsyncLifetime
{
    private readonly Guid _a=Guid.NewGuid(), _b=Guid.NewGuid(), _user=Guid.NewGuid(), _ma=Guid.NewGuid(), _mb=Guid.NewGuid();
    private string _connection=null!;
    private Mt3cHosting.ProgramHost _host=null!;
    private const string Password="Mt5bc-synthetic-only!";
    private const string State="institution_membership_security_state";
    private string Email=>_user.ToString("N")+"@mt5bc.example.test";

    private async Task<MySqlConnection> Open()
    {
        var c=new MySqlConnection(_connection);await c.OpenAsync();
        await using var cmd=new MySqlCommand("SELECT VERSION(),@@port,REPLACE(@@datadir,CHAR(92),'/'),DATABASE()",c);
        await using var r=await cmd.ExecuteReaderAsync();Assert.True(await r.ReadAsync());
        Assert.Equal("5.7.24",r.GetString(0));Assert.Equal(13306,r.GetInt32(1));
        Assert.EndsWith("/migrations/mt4/private/mt5a-mamp-13306/data/",r.GetString(2),StringComparison.OrdinalIgnoreCase);
        Assert.Equal("electronic_register_test",r.GetString(3));return c;
    }
    private async Task Exec(string sql,params (string,object)[] args)
    {
        await using var c=await Open();await using var cmd=new MySqlCommand(sql,c);
        foreach(var (name,value) in args)cmd.Parameters.AddWithValue(name,value);
        await cmd.ExecuteNonQueryAsync();
    }
    private async Task<long> Generation(Guid member)
    {
        await using var c=await Open();await using var cmd=new MySqlCommand($"SELECT security_generation FROM {State} WHERE membership_id=@m",c);
        cmd.Parameters.AddWithValue("@m",member);var value=await cmd.ExecuteScalarAsync();Assert.NotNull(value);return Convert.ToInt64(value);
    }
    public async Task InitializeAsync()
    {
        _connection=IntegrationFixture.ValidateLocalConnectionString(Environment.GetEnvironmentVariable("TEST_MYSQL_CONNECTION")??throw new InvalidOperationException("Explicit isolated target required"));
        var target=new MySqlConnectionStringBuilder(_connection);Assert.Equal("127.0.0.1",target.Server);Assert.Equal((uint)13306,target.Port);
        target.AllowUserVariables=true;_connection=target.ConnectionString;
        await Exec("INSERT INTO institution(id,code,name,status) VALUES(@a,@ca,'Synthetic JWT A','active'),(@b,@cb,'Synthetic JWT B','active'); INSERT INTO user(id,email,password_hash,role) VALUES(@u,@email,@hash,'admin'); INSERT INTO institution_membership(id,institution_id,user_id,status) VALUES(@ma,@a,@u,'active'),(@mb,@b,@u,'active'); INSERT INTO institution_membership_role(membership_id,role) VALUES(@ma,'Teacher'),(@mb,'Student')",
            ("@a",_a),("@b",_b),("@ca","MT5BP_"+_a.ToString("N")),("@cb","MT5BP_"+_b.ToString("N")),("@u",_user),("@email",Email),("@hash",BCrypt.Net.BCrypt.HashPassword(Password)),("@ma",_ma),("@mb",_mb));
        _host=await Mt3cHosting.ProgramHost.StartAsync(_connection,_a,Array.Empty<Guid>(),enabled:false,school:false,course:false,exam:false,membershipAdmin:false);
    }
    public async Task DisposeAsync()
    {
        if(_host is not null)await _host.DisposeAsync();
        if(_connection is null)return;
        await Exec("SET @er_mt5b_fixture_owners=@owners; DELETE r FROM institution_membership_role r JOIN institution_membership m ON m.id=r.membership_id WHERE m.institution_id IN (@a,@b); DELETE FROM institution_membership WHERE institution_id IN (@a,@b); DELETE FROM institution WHERE id IN (@a,@b); DELETE FROM user WHERE id=@u; SET @er_mt5b_fixture_owners=NULL",
            ("@owners",_a+","+_b),("@a",_a),("@b",_b),("@u",_user));
    }
    private async Task<HttpResponseMessage> Request(string route,string? token=null,object? body=null)
    {
        using var req=new HttpRequestMessage(body is null?HttpMethod.Get:HttpMethod.Post,route);
        if(token is not null)req.Headers.Authorization=new("Bearer",token);
        if(body is not null)req.Content=JsonContent.Create(body);
        return await _host.Client.SendAsync(req);
    }
    private async Task<string> Ticket()
    {
        using var response=await Request("/api/Auth/identity/login",body:new LoginDto{Email=Email,Password=Password});
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);return (await response.Content.ReadFromJsonAsync<IdentityTicketDto>())!.Ticket;
    }
    private async Task<string> Access(bool b=false)
    {
        using var response=await Request("/api/Auth/select-institution",await Ticket(),new SelectInstitutionDto{InstitutionId=b?_b:_a,MembershipId=b?_mb:_ma});
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);return (await response.Content.ReadFromJsonAsync<InstitutionAccessDto>())!.Token;
    }
    private static string Rewrite(string token,Action<JsonObject> mutate,bool duplicate=false)
    {
        var parts=token.Split('.');var json=JsonNode.Parse(Base64UrlEncoder.Decode(parts[1]))!.AsObject();mutate(json);
        var text=json.ToJsonString();
        if(duplicate)text=text[..^1]+",\"security_generation\":1}";
        var unsigned=parts[0]+"."+Base64UrlEncoder.Encode(text);
        return unsigned+"."+Base64UrlEncoder.Encode(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Mt3cHosting.Key),Encoding.ASCII.GetBytes(unsigned)));
    }

    [Fact]
    public async Task RealProgramIssuesPersistedGenerationAndNeverGlobalAdminPrivilege()
    {
        foreach(var b in new[]{false,true})
        {
            var token=await Access(b);var jwt=new JwtSecurityTokenHandler().ReadJwtToken(token);
            var claim=Assert.Single(jwt.Claims.Where(c=>c.Type==InstitutionTokenContract.SecurityGeneration));
            Assert.Equal((await Generation(b?_mb:_ma)).ToString(),claim.Value);
            Assert.DoesNotContain(jwt.Claims,c=>c.Type=="role"||c.Value=="InstitutionAdmin");
            Assert.Equal(HttpStatusCode.OK,(await Request("/api/Auth/context",token)).StatusCode);
        }
    }

    [Theory]
    [InlineData("missing")][InlineData("negative")][InlineData("malformed")][InlineData("overflow")]
    [InlineData("duplicate-property")][InlineData("duplicate-array")][InlineData("singleton-array")]
    [InlineData("fraction")][InlineData("null")][InlineData("boolean")][InlineData("string")]
    [InlineData("negative-zero")][InlineData("exponent")]
    public async Task InvalidGenerationClaimsAreRejectedBeforeAuthorization(string fault)
    {
        var token=await Access();
        var bad=Rewrite(token,p=>
        {
            var key=InstitutionTokenContract.SecurityGeneration;
            switch(fault)
            {
                case "missing":p.Remove(key);break;
                case "negative":p[key]=-1;break;
                case "malformed":p[key]="not-a-generation";break;
                case "overflow":p[key]=JsonNode.Parse("9223372036854775808");break;
                case "duplicate-array":p[key]=new JsonArray(1,1);break;
                case "singleton-array":p[key]=new JsonArray(1);break;
                case "fraction":p[key]=1.5;break;
                case "null":p[key]=null;break;
                case "boolean":p[key]=true;break;
                case "string":p[key]="1";break;
                case "negative-zero":p[key]=JsonNode.Parse("-0");break;
                case "exponent":p[key]=JsonNode.Parse("1e0");break;
            }
        },fault=="duplicate-property");
        Assert.Equal(HttpStatusCode.Unauthorized,(await Request("/api/Auth/context",bad)).StatusCode);
    }

    [Theory]
    [InlineData("role")][InlineData("status")]
    public async Task RestoringRolesOrStatusNeverRevivesOldTokenAndDoesNotRevokeB(string change)
    {
        var a=await Access();var b=await Access(true);var initial=await Generation(_ma);
        if(change=="role")await Exec("DELETE FROM institution_membership_role WHERE membership_id=@m AND role='Teacher'",("@m",_ma));
        else await Exec("UPDATE institution_membership SET status='suspended' WHERE id=@m",("@m",_ma));
        Assert.Equal(HttpStatusCode.Forbidden,(await Request("/api/Auth/context",a)).StatusCode);
        if(change=="role")await Exec("INSERT INTO institution_membership_role(membership_id,role) VALUES(@m,'Teacher')",("@m",_ma));
        else await Exec("UPDATE institution_membership SET status='active' WHERE id=@m",("@m",_ma));
        Assert.Equal(initial+2,await Generation(_ma));
        Assert.Equal(HttpStatusCode.Forbidden,(await Request("/api/Auth/context",a)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await Request("/api/Auth/context",b)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await Request("/api/Auth/context",await Access())).StatusCode);
    }

    [Fact]
    public async Task StatusNoopAndRolledBackMutationKeepIssuedTokenValid()
    {
        var token=await Access();var generation=await Generation(_ma);
        await Exec("UPDATE institution_membership SET status=status WHERE id=@m",("@m",_ma));
        await using(var c=await Open())
        {
            await using var tx=await c.BeginTransactionAsync();await using var cmd=new MySqlCommand("UPDATE institution_membership SET status='suspended' WHERE id=@m",c,tx);
            cmd.Parameters.AddWithValue("@m",_ma);await cmd.ExecuteNonQueryAsync();await tx.RollbackAsync();
        }
        Assert.Equal(generation,await Generation(_ma));Assert.Equal(HttpStatusCode.OK,(await Request("/api/Auth/context",token)).StatusCode);
    }

    [Fact]
    public async Task SignedTenantMismatchAndStaleOrFutureGenerationAreForbidden()
    {
        var token=await Access();
        var mismatch=Rewrite(token,p=>p[InstitutionTokenContract.Institution]=_b.ToString());
        Assert.Equal(HttpStatusCode.Forbidden,(await Request("/api/Auth/context",mismatch)).StatusCode);
        foreach(var value in new[]{0L,2L,long.MaxValue})
            Assert.Equal(HttpStatusCode.Forbidden,(await Request("/api/Auth/context",Rewrite(token,p=>p[InstitutionTokenContract.SecurityGeneration]=value))).StatusCode);
    }

    [Fact]
    public async Task MissingStateDeniesExistingTokenAndNewSelection()
    {
        var token=await Access();var generation=await Generation(_ma);
        // Explicit privileged synthetic fault injection; never an operational recovery path.
        await Exec($"SET @er_mt5b_fixture_owners=@o; DELETE FROM {State} WHERE membership_id=@m; SET @er_mt5b_fixture_owners=NULL",("@o",_a.ToString()),("@m",_ma));
        try
        {
            Assert.Equal(HttpStatusCode.Forbidden,(await Request("/api/Auth/context",token)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,(await Request("/api/Auth/select-institution",await Ticket(),new SelectInstitutionDto{InstitutionId=_a,MembershipId=_ma})).StatusCode);
        }
        finally
        {
            // Restore this owned fixture for normal guarded cleanup, using active triggers.
            await Exec($"INSERT INTO {State}(institution_id,membership_id,security_generation) VALUES(@o,@m,0); UPDATE {State} SET security_generation=@g WHERE membership_id=@m",("@o",_a),("@m",_ma),("@g",generation));
        }
    }

    [Fact]
    public async Task RemovedMembershipAndGenerationBearingSelectionTicketFailClosed()
    {
        var token=await Access();
        await Exec("SET @er_mt5b_fixture_owners=@o; DELETE FROM institution_membership_role WHERE membership_id=@m; DELETE FROM institution_membership WHERE id=@m; SET @er_mt5b_fixture_owners=NULL",("@o",_a.ToString()),("@m",_ma));
        Assert.Equal(HttpStatusCode.Forbidden,(await Request("/api/Auth/context",token)).StatusCode);
        var mixed=Rewrite(await Ticket(),p=>p[InstitutionTokenContract.SecurityGeneration]=0);
        Assert.Equal(HttpStatusCode.Unauthorized,(await Request("/api/Auth/institutions",mixed)).StatusCode);
    }

    [Fact]
    public async Task InstitutionAccessNeverOpensLegacyEndpointGate()
    {
        var response=await Request("/api/Auth/me",await Access());
        Assert.Contains(response.StatusCode,new[]{HttpStatusCode.Unauthorized,HttpStatusCode.Forbidden});
    }
}
