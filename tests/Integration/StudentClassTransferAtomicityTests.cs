using ElectronicRegisterAPI.Api.Filters;
#nullable enable
using System;
using System.Data.Common;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace ElectronicRegisterAPI.Tests.Integration;
public sealed partial class InstitutionClassCrudTests
{
    [Theory][InlineData("SourceEnrollment")][InlineData("DestinationEnrollment")][InlineData("EnrollmentAudit")][InlineData("Student")][InlineData("TransferAudit")]
    public async Task TransferFaultAfterEachCriticalMutationRollsBackEntireUnit(string phase)
    {
        await SeedTransfer(true);var token=await Access();var fault=new TransferSqlFault(phase);
        await using var host=await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString,_a,Array.Empty<Guid>(),enabled:false,school:true,course:true,configureServices:services=>services.ConfigureDbContext<ElectronicRegisterContext>((_,options)=>options.AddInterceptors(fault)));
        await Expect(await Request(TransferRoute(),token,TransferInput(),"POST",host:host),HttpStatusCode.InternalServerError);Assert.True(fault.Triggered);
        await AssertTransferUnchanged();await Db(async db=>{Assert.Single(await db.StudentEnrollments.Where(e=>e.StudentId==Id("student")).ToListAsync());Assert.False(await db.CourseAdministrationEvents.AnyAsync(e=>e.InstitutionId==_a));});
    }
    private sealed class TransferSqlFault(string phase):DbCommandInterceptor
    {
        private int _hit;public bool Triggered=>_hit!=0;
        private bool Matches(DbCommand command)
        {
            var sql=command.CommandText.TrimStart();var match=phase switch{
                "SourceEnrollment"=>sql.StartsWith("UPDATE `student_enrollment`",StringComparison.OrdinalIgnoreCase),
                "DestinationEnrollment"=>sql.StartsWith("INSERT INTO student_enrollment",StringComparison.OrdinalIgnoreCase),
                "EnrollmentAudit"=>sql.StartsWith("INSERT INTO course_administration_event",StringComparison.OrdinalIgnoreCase),
                "Student"=>sql.StartsWith("UPDATE `student`",StringComparison.OrdinalIgnoreCase),
                "TransferAudit"=>sql.Contains("INSERT INTO `student_class_transfer_event`",StringComparison.OrdinalIgnoreCase),_=>false};
            return match&&Interlocked.CompareExchange(ref _hit,1,0)==0;
        }
        public override ValueTask<int> NonQueryExecutedAsync(DbCommand command,CommandExecutedEventData data,int result,CancellationToken ct=default)
        {if(Matches(command))throw new InvalidOperationException("Injected after critical transfer mutation");return ValueTask.FromResult(result);}
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,CommandExecutedEventData data,DbDataReader result,CancellationToken ct=default)
        {if(Matches(command)){await result.DisposeAsync();throw new InvalidOperationException("Injected after transfer audit insertion");}return result;}
    }
    [Fact]
    public async Task TransferConcurrentReplayReturnsOnePersistedReceipt()
    {await SeedTransfer(true);var token=await Access();var input=TransferInput();var responses=await Task.WhenAll(Request(TransferRoute(),token,input,"POST"),Request(TransferRoute(),token,input,"POST"));foreach(var response in responses)await Expect(response,HttpStatusCode.OK);Assert.Equal(await responses[0].Content.ReadAsStringAsync(),await responses[1].Content.ReadAsStringAsync());await Db(async db=>{Assert.Single(await db.StudentClassTransferEvents.Where(e=>e.InstitutionId==_a).ToListAsync());Assert.Equal(2,await db.StudentEnrollments.CountAsync(e=>e.StudentId==Id("student")));Assert.Equal(2,await db.CourseAdministrationEvents.CountAsync(e=>e.InstitutionId==_a));});}
    [Fact]
    public async Task TransferRoutesCarrySchoolPolicyAdminRoleAndCourseWriteGuard()
    {
        var type=typeof(ElectronicRegisterAPI.Api.Controllers.InstitutionStudentTransferController);Assert.Single(type.GetCustomAttributes(typeof(ElectronicRegisterAPI.Api.Security.InstitutionSchoolEndpointAttribute),false));var auth=type.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute),false).Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().ToArray();Assert.Equal("InstitutionAdmin",Assert.Single(auth.Where(a=>a.Roles is not null)).Roles);Assert.Equal(ElectronicRegisterAPI.Api.Security.InstitutionSchoolEndpointAttribute.PolicyName,Assert.Single(auth.Where(a=>a.Policy is not null)).Policy);
        Assert.Single(type.GetMethod("Transfer")!.GetCustomAttributes(typeof(ElectronicRegisterAPI.Api.Filters.RequireTransferCourseApiAttribute),false));Assert.Empty(type.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute),false));
    }
    [Theory][InlineData(0,20)][InlineData(1,0)][InlineData(1,101)][InlineData(2147483647,100)]
    public async Task TransferHistoryRejectsInvalidPagination(int page,int size)=>await Expect(await Request(TransferHistory()+$"?pageNumber={page}&pageSize={size}",await Access()),HttpStatusCode.BadRequest);
    [Theory][InlineData("EventId")][InlineData("ExpectedRevision")][InlineData("EffectiveDate")][InlineData("ToClassId")]
    public async Task TransferRequiresExplicitIntentionDateAndRevision(string field)
    {await SeedTransfer();var body=System.Text.Json.JsonSerializer.SerializeToNode(TransferInput())!.AsObject();body.Remove(field);await Expect(await Request(TransferRoute(),await Access(),body,"POST"),HttpStatusCode.BadRequest);await AssertTransferUnchanged();}
    [Fact]
    public async Task TransferReplayCannotBeClaimedByAnotherAdministrator()
    {
        await SeedTransfer();var input=TransferInput();await Expect(await Request(TransferRoute(),await Access(),input,"POST"),HttpStatusCode.OK);
        var login=await Request("/api/Auth/identity/login",body:new LoginDto{Email=_admin2.ToString("N")+"@mtclass.example.test",Password=Password},method:"POST");await Expect(login,HttpStatusCode.OK);var ticket=(await login.Content.ReadFromJsonAsync<IdentityTicketDto>())!.Ticket;
        var select=await Request("/api/Auth/select-institution",ticket,new SelectInstitutionDto{InstitutionId=_a,MembershipId=Id("admin2-membership")},"POST");await Expect(select,HttpStatusCode.OK);var token=(await select.Content.ReadFromJsonAsync<InstitutionAccessDto>())!.Token;
        await Expect(await Request(TransferRoute(),token,input,"POST"),HttpStatusCode.Conflict);await Db(async db=>Assert.Single(await db.StudentClassTransferEvents.Where(e=>e.InstitutionId==_a).ToListAsync()));
    }
    [Fact]
    public async Task TransferParallelTenantContextsCannotBleedAcrossRequests()
    {
        await SeedTransfer();var a=await Access();var b=await Access(b:true);var before=await Digest(true);var responses=await Task.WhenAll(Request(TransferRoute(),a,TransferInput(),"POST"),Request(TransferRoute(),b,TransferInput(),"POST"));await Expect(responses[0],HttpStatusCode.OK);await Expect(responses[1],HttpStatusCode.NotFound);Assert.Equal(before,await Digest(true));
    }
    [Fact]
    public async Task TransferRejectsOversizedPayloadBeforeAnyMutation()
    {await SeedTransfer();await Expect(await Request(TransferRoute(),await Access(),TransferInput(reason:new string('x',20000)),"POST"),HttpStatusCode.RequestEntityTooLarge);await AssertTransferUnchanged();}

}
