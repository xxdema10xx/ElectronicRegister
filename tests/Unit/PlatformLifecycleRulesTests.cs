using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Policies;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Unit;

public sealed class PlatformLifecycleRulesTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);
    private const int Months = 36;

    private static LifecycleMembership M(MembershipStatus status, InstitutionStatus institution, DateTime joined, params InstitutionRole[] roles) => new(status, institution, roles, joined);
    private static LifecycleDecision Evaluate(AccountLifecycleState state, bool invitation, params LifecycleMembership[] memberships) =>
        PlatformLifecycleRules.Evaluate(new LifecycleSnapshot(state, memberships, invitation), Now, Months);
    private static LifecycleDecision Evaluate(params LifecycleMembership[] memberships) => Evaluate(AccountLifecycleState.Active, false, memberships);

    [Theory]
    [InlineData(InstitutionRole.Teacher)]
    [InlineData(InstitutionRole.Staff)]
    [InlineData(InstitutionRole.InstitutionAdmin)]
    public void StaffTeachersAndAdminsCloseAtTheFirstPassWithoutActiveMemberships(InstitutionRole role)
    {
        var yesterday = Now.AddDays(-1);
        Assert.Equal(new LifecycleDecision(LifecycleAction.Close, LifecycleRuleCodes.NoActiveMembership), Evaluate(M(MembershipStatus.Left, InstitutionStatus.Active, yesterday, role)));
        Assert.Equal(new LifecycleDecision(LifecycleAction.Close, LifecycleRuleCodes.NoActiveMembership), Evaluate(M(MembershipStatus.Suspended, InstitutionStatus.Active, yesterday, role)));
        Assert.Equal(new LifecycleDecision(LifecycleAction.Close, LifecycleRuleCodes.NoActiveMembership), Evaluate(M(MembershipStatus.Active, InstitutionStatus.Suspended, yesterday, role)));
        Assert.Equal(LifecycleAction.Keep, Evaluate(M(MembershipStatus.Active, InstitutionStatus.Active, yesterday, role)).Action);
    }

    [Fact]
    public void AccountsWithoutAnyMembershipCloseImmediately() =>
        Assert.Equal(new LifecycleDecision(LifecycleAction.Close, LifecycleRuleCodes.NoActiveMembership), Evaluate());

    [Fact]
    public void StudentsCloseOnlyAfterTheConfiguredPeriodFromTheFirstStudentMembership()
    {
        var start = Now.AddMonths(-Months);
        var before = Evaluate(M(MembershipStatus.Left, InstitutionStatus.Active, start.AddSeconds(1), InstitutionRole.Student));
        Assert.Equal(LifecycleAction.Keep, before.Action);
        Assert.Equal(Now.AddSeconds(1), before.NotBeforeUtc);
        Assert.Equal(new LifecycleDecision(LifecycleAction.Close, LifecycleRuleCodes.StudentPeriodElapsed), Evaluate(M(MembershipStatus.Left, InstitutionStatus.Active, start, InstitutionRole.Student)));
        // The earliest Student membership counts, even when a later one exists elsewhere.
        Assert.Equal(LifecycleAction.Close, Evaluate(
            M(MembershipStatus.Left, InstitutionStatus.Active, start.AddYears(-1), InstitutionRole.Student),
            M(MembershipStatus.Suspended, InstitutionStatus.Active, Now.AddDays(-10), InstitutionRole.Student)).Action);
    }

    [Fact]
    public void StudentsWithAnActiveMembershipAreNeverClosedEvenAfterThePeriod() =>
        Assert.Equal(LifecycleAction.Keep, Evaluate(M(MembershipStatus.Active, InstitutionStatus.Active, Now.AddYears(-6), InstitutionRole.Student)).Action);

    [Fact]
    public void MultipleRolesApplyTheMostRestrictiveRule()
    {
        // Former teacher who was also a student two years ago: the student period still runs.
        var decision = Evaluate(
            M(MembershipStatus.Left, InstitutionStatus.Active, Now.AddYears(-5), InstitutionRole.Teacher),
            M(MembershipStatus.Left, InstitutionStatus.Active, Now.AddYears(-2), InstitutionRole.Student, InstitutionRole.Staff));
        Assert.Equal(LifecycleAction.Keep, decision.Action);
        Assert.Equal(LifecycleAction.Close, Evaluate(
            M(MembershipStatus.Left, InstitutionStatus.Active, Now.AddYears(-5), InstitutionRole.Teacher),
            M(MembershipStatus.Left, InstitutionStatus.Archived, Now.AddYears(-4), InstitutionRole.Student, InstitutionRole.Staff)).Action);
        // An active teacher membership keeps a long-finished student open.
        Assert.Equal(LifecycleAction.Keep, Evaluate(
            M(MembershipStatus.Active, InstitutionStatus.Active, Now.AddDays(-1), InstitutionRole.Teacher),
            M(MembershipStatus.Left, InstitutionStatus.Active, Now.AddYears(-9), InstitutionRole.Student)).Action);
    }

    [Fact]
    public void PendingInvitationsPreventClosure() =>
        Assert.Equal(LifecycleAction.Keep, Evaluate(AccountLifecycleState.Active, true, M(MembershipStatus.Left, InstitutionStatus.Active, Now.AddYears(-1), InstitutionRole.Teacher)).Action);

    [Fact]
    public void ClosedAccountsReactivateOnlyWithAnActiveMembershipInAnActiveInstitution()
    {
        Assert.Equal(new LifecycleDecision(LifecycleAction.Reactivate, LifecycleRuleCodes.ActiveMembershipFound),
            Evaluate(AccountLifecycleState.Closed, false, M(MembershipStatus.Active, InstitutionStatus.Active, Now, InstitutionRole.Teacher)));
        Assert.Equal(LifecycleAction.Keep, Evaluate(AccountLifecycleState.Closed, false, M(MembershipStatus.Active, InstitutionStatus.Suspended, Now, InstitutionRole.Teacher)).Action);
        Assert.Equal(LifecycleAction.Keep, Evaluate(AccountLifecycleState.Closed, true, M(MembershipStatus.Pending, InstitutionStatus.Active, Now, InstitutionRole.Teacher)).Action);
    }

    [Fact]
    public void InvalidParametersAreRejected()
    {
        var snapshot = new LifecycleSnapshot(AccountLifecycleState.Active, [], false);
        Assert.Throws<ArgumentException>(() => PlatformLifecycleRules.Evaluate(snapshot, DateTime.Now, Months));
        Assert.Throws<ArgumentException>(() => PlatformLifecycleRules.Evaluate(snapshot, Now, 0));
    }
}
