using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Xunit;
namespace ElectronicRegisterAPI.Tests.Integration;

public sealed partial class InstitutionClassCrudTests
{
    [Fact]
    public async Task EmailPolicy_TenantsHaveIndependentConventionsAndForeignRecipientsAreNotFound()
    {
        await using var host=await EmailHost();var a=await Access();var b=await Access(b:true);
        await PublishEmail(host,a,EmailConfig("a_{nome}"));
        await PublishEmail(host,b,EmailConfig("b_{cognome}",_b.ToString("N")+".example.test"));
        var pa=(await(await Request(EmailPolicies+"/Student",a,host:host)).Content.ReadFromJsonAsync<EmailPolicyVersion>())!;
        var pb=(await(await Request(EmailPolicies+"/Student",b,host:host)).Content.ReadFromJsonAsync<EmailPolicyVersion>())!;
        Assert.NotEqual(pa.PolicyId,pb.PolicyId);Assert.NotEqual(pa.Template.Domain,pb.Template.Domain);Assert.NotEqual(pa.Template.LocalPartTemplate,pb.Template.LocalPartTemplate);
        var aa=await ReserveEmail(host,a,Id("student"));var bb=await ReserveEmail(host,b,Id("student",true));Assert.NotEqual(aa.Email,bb.Email);
        await Db(async db=>Assert.Equal(_studentUser.ToString("N")+"@mtclass.example.test",(await db.Users.AsNoTracking().SingleAsync(u=>u.Id==_studentUser)).Email));
        await Expect(await Request(EmailAssignments+"/"+aa.Id,await Access("student"),host:host),HttpStatusCode.OK);
        await Expect(await Request(EmailAssignments+"/"+bb.Id,await Access("student",true),host:host),HttpStatusCode.OK);
        await Expect(await Request(EmailAssignments,a,new EmailReserve(Guid.NewGuid(),EmailCategory.Student,1,new(Id("student",true),null,null)),"POST",host),HttpStatusCode.NotFound);
        await Expect(await Request(EmailAssignments+"/create-person",a,new EmailCreatePerson(Guid.NewGuid(),EmailCategory.Student,1,"Mario","Rossi",Id("class",true)),"POST",host),HttpStatusCode.NotFound);
    }
    [Fact]
    public async Task EmailPolicy_OneInstitutionCanPublishDifferentTemplatesForAllCategories()
    {
        await using var host=await EmailHost();var token=await Access();
        foreach(var category in Enum.GetValues<EmailCategory>())await PublishEmail(host,token,EmailConfig(category.ToString().ToLowerInvariant()+"_{nome}.{cognome}"),category:category);
        var policies=(await(await Request(EmailPolicies,token,host:host)).Content.ReadFromJsonAsync<EmailPolicyVersion[]>())!;
        Assert.Equal(4,policies.Length);Assert.Equal(4,policies.Select(p=>p.Template.LocalPartTemplate).Distinct().Count());
        var staffPolicies=(await(await Request(EmailPolicies,await StaffToken(),host:host)).Content.ReadFromJsonAsync<EmailPolicyVersion[]>())!;
        Assert.Equal(new[]{EmailCategory.Student,EmailCategory.Teacher},staffPolicies.Select(p=>p.Category).OrderBy(p=>p).ToArray());
    }
    [Fact]
    public async Task EmailPolicy_CrossTenantNamespaceCollisionReturnsOnlyGenericConflict()
    {
        await using var host=await EmailHost();var a=await Access();var b=await Access(b:true);var config=EmailConfig("shared");
        await PublishEmail(host,a,config);await PublishEmail(host,b,config);var first=await ReserveEmail(host,a,Id("student"));
        var collision=await Request(EmailAssignments,b,new EmailReserve(Guid.NewGuid(),EmailCategory.Student,1,new(Id("student",true),null,null)),"POST",host);
        await Expect(collision,HttpStatusCode.Conflict);var body=await collision.Content.ReadAsStringAsync();Assert.DoesNotContain(first.Email,body);Assert.DoesNotContain(first.Id.ToString(),body);Assert.DoesNotContain(_a.ToString(),body);
        await Expect(await Request(EmailAssignments+"/"+first.Id,b,host:host),HttpStatusCode.NotFound);
    }
    [Theory]
    [InlineData(EmailCategory.Student)][InlineData(EmailCategory.Teacher)][InlineData(EmailCategory.Staff)][InlineData(EmailCategory.InstitutionAdmin)]
    public async Task EmailPolicy_CategoriesAreIndependentAndDoNotChangeIamRoles(EmailCategory category)
    {
        await using var host=await EmailHost();var a=await Access();await PublishEmail(host,a,EmailConfig(category+"_{nome:iniziale}.{cognome}"),category:category);
        var policies=(await(await Request(EmailPolicies,a,host:host)).Content.ReadFromJsonAsync<EmailPolicyVersion[]>())!;Assert.Single(policies);Assert.Equal(category,policies[0].Category);Assert.Equal("ConfiguredUnverified",policies[0].DomainVerificationState);
        // Fixture deliberately gives the institutional Admin a legacy Student role.
        await Db(async db=>{Assert.Equal("student",(await db.Users.SingleAsync(u=>u.Id==_adminUser)).Role);Assert.False(await db.InstitutionMembershipRoles.AnyAsync(r=>r.MembershipId==Id("student-membership")&&r.Role=="InstitutionAdmin"));});
    }
    [Theory]
    [InlineData("Student")][InlineData("Teacher")]
    public async Task EmailPolicy_OwnAddressVisibilityUsesRealProfiles(string category)
    {
        var cat=Enum.Parse<EmailCategory>(category);await using var host=await EmailHost();var a=await Access();await PublishEmail(host,a,category:cat);
        var recipient=cat==EmailCategory.Student?new EmailRecipient(Id("student"),null,null):new EmailRecipient(null,Id("teacher"),null);
        var response=await Request(EmailAssignments,a,new EmailReserve(Guid.NewGuid(),cat,1,recipient),"POST",host);await Expect(response,HttpStatusCode.OK);var assigned=(await response.Content.ReadFromJsonAsync<EmailAssignment>())!;
        var own=await Access(category.ToLowerInvariant());await Expect(await Request(EmailAssignments+"/"+assigned.Id,own,host:host),HttpStatusCode.OK);
        var other=await Access(cat==EmailCategory.Student?"teacher":"student");await Expect(await Request(EmailAssignments+"/"+assigned.Id,other,host:host),HttpStatusCode.NotFound);
        var page=(await(await Request(EmailAssignments,other,host:host)).Content.ReadFromJsonAsync<EmailPage<EmailAssignment>>())!;Assert.Equal(0,page.TotalCount);Assert.Empty(page.Items);
    }
    [Theory]
    [InlineData("mailboxVerified")][InlineData("domainVerified")][InlineData("userId")][InlineData("institutionId")][InlineData("role")]
    public async Task EmailPolicy_ForbiddenFieldsCannotCreateCredentialsOrVerification(string field)
    {
        await using var host=await EmailHost();var a=await Access();await PublishEmail(host,a);var body=new JsonObject{["eventId"]=Guid.NewGuid(),["category"]="Student",["policyRevision"]=1,["recipient"]=new JsonObject{["studentId"]=Id("student")},[field]="forged"};
        await Expect(await Request(EmailAssignments,await StaffToken(),body,"POST",host),HttpStatusCode.BadRequest);
        await Db(async db=>Assert.False(await db.Set<InstitutionEmailAssignment>().AnyAsync(p=>p.InstitutionId==_a)));
    }
    [Fact]
    public async Task EmailPolicy_RetiredAddressCannotBeReservedForAnotherPerson()
    {
        await using var host=await EmailHost();var a=await Access();await PublishEmail(host,a,EmailConfig("fixed"));var first=await ReserveEmail(host,a,Id("student"));
        await Expect(await Request(EmailAssignments+"/"+first.Id+"/retire",a,new EmailRetire(Guid.NewGuid(),1,"No reuse"),"POST",host),HttpStatusCode.OK);
        await Expect(await Request(EmailAssignments,a,new EmailReserve(Guid.NewGuid(),EmailCategory.Student,1,new(Id("student-other"),null,null)),"POST",host),HttpStatusCode.Conflict);
    }
    [Fact]
    public async Task EmailPolicy_ConcurrentGlobalWriterAndAssignmentCannotDuplicateAddress()
    {
        await using var host=await EmailHost();var a=await Access();var config=EmailConfig("racing");await PublishEmail(host,a,config);var user=Guid.NewGuid();
        async Task<bool> GlobalWriter(){try{await Execute("INSERT INTO user(id,email,password_hash,role) VALUES(@id,@email,'not-a-password','student')",("@id",user),("@email","racing@"+config.Domain));return true;}catch(MySqlException e)when(e.Number==1062){return false;}}
        var global=GlobalWriter();var institutional=Request(EmailAssignments,a,new EmailReserve(Guid.NewGuid(),EmailCategory.Student,1,new(Id("student"),null,null)),"POST",host);
        var result=await institutional;var globalWon=await global;Assert.True(result.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict);Assert.NotEqual(globalWon,result.StatusCode==HttpStatusCode.OK);
        await Db(async db=>Assert.Equal(1,await db.Set<InstitutionEmailReservation>().CountAsync(r=>r.Address=="racing@"+config.Domain)));
    }
}
