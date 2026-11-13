using ElectronicRegisterAPI.Domain.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Unit;

public class MembershipAuthorizationTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    public void EveryValidCombinedMembershipRequiresExactIdentityGenerationAndRoleSet(int mask)
    {
        var roles = Enum.GetValues<InstitutionRole>().Where(r => (mask & (1 << (int)r)) != 0).ToArray();
        var row = Current() with { Roles = roles, SecurityGeneration = 7 };
        var token = new TenantContext(row.UserId, row.InstitutionId, row.MembershipId, roles, 7);

        Assert.True(MembershipAuthorizationRules.Matches(token, row));
        Assert.False(MembershipAuthorizationRules.Matches(token, row with { UserId = Guid.NewGuid() }));
        Assert.False(MembershipAuthorizationRules.Matches(token, row with { InstitutionId = Guid.NewGuid() }));
        Assert.False(MembershipAuthorizationRules.Matches(token, row with { MembershipId = Guid.NewGuid() }));
        Assert.False(MembershipAuthorizationRules.Matches(token, row with { SecurityGeneration = 8 }));
        Assert.False(MembershipAuthorizationRules.Matches(token, row with { SecurityGeneration = null }));
        Assert.False(MembershipAuthorizationRules.Matches(token, row with { Status = MembershipStatus.Suspended }));
        Assert.False(MembershipAuthorizationRules.Matches(token, row with { InstitutionStatus = InstitutionStatus.Suspended }));
        Assert.False(MembershipAuthorizationRules.Matches(token, row with { Roles = roles.Append(roles[0]).ToArray() }));
        Assert.False(MembershipAuthorizationRules.Matches(token, row with { Roles = roles.Append((InstitutionRole)999).ToArray() }));
        Assert.False(MembershipAuthorizationRules.Matches(token, row with { Roles = roles.Skip(1).ToArray() }));
    }

    [Fact]
    public void MissingMembershipFailsClosed()
    {
        var row = Current();
        var token = new TenantContext(row.UserId, row.InstitutionId, row.MembershipId, row.Roles, row.SecurityGeneration);
        Assert.False(MembershipAuthorizationRules.Matches(token, null));
    }

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
        var context = new TenantContext(current.UserId, current.InstitutionId, current.MembershipId, list, current.SecurityGeneration);
        list.Add(InstitutionRole.InstitutionAdmin);
        Assert.Single(context.Roles);
        Assert.True(MembershipAuthorizationRules.Matches(context, current));
        Assert.False(MembershipAuthorizationRules.Matches(context, current with { Roles = list }));
    }

    [Fact]
    public void SameRolesInAnotherInstitutionOrIdentityDoNotAuthorize()
    {
        var row = Current();
        var token = new TenantContext(row.UserId, row.InstitutionId, row.MembershipId, row.Roles, row.SecurityGeneration);
        Assert.False(MembershipAuthorizationRules.Matches(token, row with { UserId = Guid.NewGuid() }));
        Assert.False(MembershipAuthorizationRules.Matches(token, row with { InstitutionId = Guid.NewGuid() }));
        Assert.False(MembershipAuthorizationRules.Matches(token, row with { MembershipId = Guid.NewGuid() }));
        Assert.False(MembershipAuthorizationRules.IsSelectable(row with { Roles = Array.Empty<InstitutionRole>() }));
    }

    [Fact]
    public void EmptyInstitutionIsNotAnImplicitTenant() =>
        Assert.Throws<ArgumentException>(() => new TenantContext(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), new[] { InstitutionRole.Student }));

    private static MembershipAuthorization Current(MembershipStatus status = MembershipStatus.Active, InstitutionStatus institution = InstitutionStatus.Active) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Unit", institution, status, new[] { InstitutionRole.Teacher }, 0);

    [Fact]
    public void MissingSecurityStateOrContextNeverDefaultsToZero()
    {
        var row = Current();
        Assert.False(MembershipAuthorizationRules.IsSelectable(row with { SecurityGeneration = null }));
        Assert.False(MembershipAuthorizationRules.IsSelectable(row with { SecurityGeneration = -1 }));
        Assert.False(MembershipAuthorizationRules.Matches(new(row.UserId, row.InstitutionId, row.MembershipId, row.Roles), row));
    }

    [Fact]
    public void SameRolesNeverReviveAnOlderGeneration()
    {
        var row = Current();
        var token = new TenantContext(row.UserId, row.InstitutionId, row.MembershipId, row.Roles, 0);
        Assert.False(MembershipAuthorizationRules.Matches(token, row with { SecurityGeneration = 2 }));
        Assert.True(MembershipAuthorizationRules.Matches(token, row));
        Assert.Throws<ArgumentException>(() => new TenantContext(row.UserId, row.InstitutionId, row.MembershipId, row.Roles, -1));
    }
}
