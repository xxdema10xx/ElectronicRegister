using System;
using System.Collections.Generic;
using ElectronicRegisterAPI.Business.Services;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Unit;

public class MembershipAuthorizationTests
{
    [Theory]
    [InlineData(MembershipStatus.Pending)]
    [InlineData(MembershipStatus.Suspended)]
    [InlineData(MembershipStatus.Left)]
    public void NonActiveMembershipIsNeverSelectable(MembershipStatus status) =>
        Assert.False(MembershipAuthorizationRules.IsSelectable(Current(status: status)));

    [Theory]
    [InlineData(InstitutionStatus.Provisioning)]
    [InlineData(InstitutionStatus.Suspended)]
    [InlineData(InstitutionStatus.Archived)]
    public void NonActiveInstitutionIsNeverSelectable(InstitutionStatus status) =>
        Assert.False(MembershipAuthorizationRules.IsSelectable(Current(institution: status)));

    [Fact]
    public void RolesAreCopiedAndCannotBeChangedAfterContextCreation()
    {
        var current = Current();
        var list = new List<InstitutionRole> { InstitutionRole.Teacher };
        var context = new TenantContext(current.UserId, current.InstitutionId, current.MembershipId, list);
        list.Add(InstitutionRole.InstitutionAdmin);
        Assert.Single(context.Roles);
        Assert.True(MembershipAuthorizationRules.Matches(context, current));
        Assert.False(MembershipAuthorizationRules.Matches(context, current with { Roles = list }));
    }

    [Fact]
    public void SameRolesInAnotherInstitutionOrIdentityDoNotAuthorize()
    {
        var row = Current();
        var token = new TenantContext(row.UserId, row.InstitutionId, row.MembershipId, row.Roles);
        Assert.False(MembershipAuthorizationRules.Matches(token, row with { UserId = Guid.NewGuid() }));
        Assert.False(MembershipAuthorizationRules.Matches(token, row with { InstitutionId = Guid.NewGuid() }));
        Assert.False(MembershipAuthorizationRules.Matches(token, row with { MembershipId = Guid.NewGuid() }));
        Assert.False(MembershipAuthorizationRules.IsSelectable(row with { Roles = Array.Empty<InstitutionRole>() }));
    }

    [Fact]
    public void EmptyInstitutionIsNotAnImplicitTenant() =>
        Assert.Throws<ArgumentException>(() => new TenantContext(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), new[] { InstitutionRole.Student }));

    private static MembershipAuthorization Current(MembershipStatus status = MembershipStatus.Active, InstitutionStatus institution = InstitutionStatus.Active) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Unit", institution, status, new[] { InstitutionRole.Teacher });
}
