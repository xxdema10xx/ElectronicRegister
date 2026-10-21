using System;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Models;
using Xunit;
namespace ElectronicRegisterAPI.Tests.Unit;

public sealed class MembershipMutationRulesTests
{
    private static MembershipMutation Request()=>new(Guid.NewGuid(),Guid.NewGuid(),0,MembershipMutationAction.AddRole,InstitutionRole.Teacher,"Synthetic reason");
    [Theory]
    [InlineData("member")][InlineData("event")][InlineData("revision")][InlineData("action")][InlineData("role")]
    [InlineData("missing-role")][InlineData("extra-role")][InlineData("empty-reason")][InlineData("long-reason")][InlineData("control")]
    public void InvalidIntentsAreRejected(string kind)
    {
        var r=Request();r=kind switch {
            "member"=>r with{MembershipId=Guid.Empty},"event"=>r with{EventId=Guid.Empty},"revision"=>r with{ExpectedRevision=-1},
            "action"=>r with{Action=(MembershipMutationAction)99},"role"=>r with{Role=(InstitutionRole)99},"missing-role"=>r with{Role=null},
            "extra-role"=>r with{Action=MembershipMutationAction.Suspend},"empty-reason"=>r with{Reason=" "},
            "long-reason"=>r with{Reason=new string('x',501)},_=>r with{Reason="reason\n"}};
        Assert.Throws<ArgumentException>(()=>MembershipMutationRules.Validate(r));
    }
    [Theory]
    [InlineData(true,0,true)][InlineData(true,1,false)][InlineData(false,0,false)]
    public void LastAdminRequiresAnotherActiveAdmin(bool removes,long others,bool denied)
    {
        if(denied)Assert.Throws<MembershipConflictException>(()=>MembershipMutationRules.RequireLastAdmin(removes,others));
        else MembershipMutationRules.RequireLastAdmin(removes,others);
    }
    [Theory]
    [InlineData(MembershipStatus.Pending)][InlineData(MembershipStatus.Left)]
    public void IamDoesNotImplementOnboardingOrRejoin(MembershipStatus status)=>
        Assert.Throws<MembershipConflictException>(()=>MembershipMutationRules.RequireTransition(Request(),status,Array.Empty<InstitutionRole>()));
    [Fact]
    public void NoopRoleAndStatusChangesAreConflicts()
    {
        Assert.Throws<MembershipConflictException>(()=>MembershipMutationRules.RequireTransition(Request(),MembershipStatus.Active,new[]{InstitutionRole.Teacher}));
        Assert.Throws<MembershipConflictException>(()=>MembershipMutationRules.RequireTransition(Request() with{Action=MembershipMutationAction.RemoveRole},MembershipStatus.Active,Array.Empty<InstitutionRole>()));
        Assert.Throws<MembershipConflictException>(()=>MembershipMutationRules.RequireTransition(Request() with{Action=MembershipMutationAction.Reactivate,Role=null},MembershipStatus.Active,new[]{InstitutionRole.Teacher}));
    }
}
