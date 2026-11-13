using ElectronicRegisterAPI.Domain.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Unit;

public class InstitutionStaffPermissionsTests
{
    public static IEnumerable<object[]> StaffCombinations()
    {
        foreach (var extra in new[] { Array.Empty<InstitutionRole>(), new[] { InstitutionRole.Teacher },
                     new[] { InstitutionRole.Student }, new[] { InstitutionRole.Teacher, InstitutionRole.Student } })
        foreach (var permission in Enum.GetValues<InstitutionPermission>())
            yield return new object[] { new[] { InstitutionRole.Staff }.Concat(extra).ToArray(), permission };
    }

    [Theory, MemberData(nameof(StaffCombinations))]
    public void StaffCannotRecoverDestructiveOrGovernancePowersFromOtherRoles(InstitutionRole[] roles, InstitutionPermission permission)
    {
        var expected = permission is InstitutionPermission.UserRead or InstitutionPermission.UserCreate or InstitutionPermission.UserUpdate or
            InstitutionPermission.GradeRead or InstitutionPermission.GradeCreate or InstitutionPermission.GradeUpdate or
            InstitutionPermission.ExamRead or InstitutionPermission.ExamCreate or InstitutionPermission.ExamUpdate or
            InstitutionPermission.ExamAssign or InstitutionPermission.ExamPublish or InstitutionPermission.ExamClose or InstitutionPermission.SubmissionRead ||
            roles.Contains(InstitutionRole.Teacher) && permission is InstitutionPermission.SubmissionReview or InstitutionPermission.SubmissionFinalize;
        Assert.Equal(expected, InstitutionPermissions.Can(roles, permission));
    }

    [Fact]
    public void PersistedInstitutionRoleOrdinalsArePreserved()
    {
        Assert.Equal(0, (int)InstitutionRole.InstitutionAdmin);
        Assert.Equal(1, (int)InstitutionRole.Teacher);
        Assert.Equal(2, (int)InstitutionRole.Student);
        Assert.Equal(3, (int)InstitutionRole.Staff);
        Assert.Equal(4, Enum.GetNames<InstitutionRole>().Length);
    }

    [Fact]
    public void AdminPlusStaffKeepsExplicitAdministrativePowers()
    {
        foreach (var permission in Enum.GetValues<InstitutionPermission>())
            Assert.True(InstitutionPermissions.Can(new[] { InstitutionRole.InstitutionAdmin, InstitutionRole.Staff }, permission));
    }

    [Fact]
    public void UnknownCatalogValuesFailClosedEvenAlongsideAdmin()
    {
        Assert.False(InstitutionPermissions.Can(new[] { InstitutionRole.InstitutionAdmin, (InstitutionRole)999 }, InstitutionPermission.ExamDelete));
        Assert.False(InstitutionPermissions.Can(new[] { InstitutionRole.InstitutionAdmin }, (InstitutionPermission)999));
        Assert.False(InstitutionPermissions.Can(Array.Empty<InstitutionRole>(), InstitutionPermission.UserRead));
    }

    [Fact]
    public void StaffPrincipalNeedsNeitherProfileNorLegacyIdentity()
    {
        var actor = new ExamPrincipal(new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new[] { InstitutionRole.Staff }, 0), false, null, null);
        Assert.True(actor.IsStaff);
        Assert.False(actor.IsTeacher);
        Assert.False(actor.IsAdmin);
        Assert.Null(actor.TeacherId);
        Assert.Null(actor.StudentId);
        Assert.NotNull(actor.Tenant);
        Assert.True(actor.Can(InstitutionPermission.ExamCreate));
        Assert.False(actor.CanDeleteExam);
        Assert.False(actor.CanCancelExam);
    }

    [Theory]
    [InlineData(InstitutionRole.InstitutionAdmin)]
    [InlineData(InstitutionRole.Teacher)]
    [InlineData(InstitutionRole.Student)]
    [InlineData(InstitutionRole.Staff)]
    public void InstitutionalPrincipalPreservesRolesWithoutGlobalProjection(InstitutionRole role)
    {
        var student = role == InstitutionRole.Student;
        var actor = new ExamPrincipal(new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new[] { role }, 7),
            student, student ? Guid.NewGuid() : null, role == InstitutionRole.Teacher ? Guid.NewGuid() : null);

        Assert.Contains(role, actor.Tenant.Roles);
        Assert.Equal(7, actor.Tenant.SecurityGeneration);
        Assert.Null(typeof(ExamPrincipal).GetProperty("LegacyCaller"));
    }

    [Fact]
    public void RetiredGlobalPrincipalAdapterIsAbsent()
    {
        Assert.Null(typeof(ExamPrincipal).GetMethod("Legacy"));
        Assert.Null(typeof(ExamPrincipal).GetProperty("LegacyCaller"));
        Assert.Null(typeof(ExamPrincipal).Assembly.GetType("ElectronicRegisterAPI.Domain.DTOs.ClaimsContext"));
        Assert.Null(typeof(ExamPrincipal).Assembly.GetType("ElectronicRegisterAPI.Domain.Enums.UserRole"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TeacherDestructionIsBlockedOnlyWhenStaffIsAlsoPresent(bool staff)
    {
        var roles = staff ? new[] { InstitutionRole.Staff, InstitutionRole.Teacher } : new[] { InstitutionRole.Teacher };
        var actor = new ExamPrincipal(new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), roles, 0), false, null, Guid.NewGuid());
        Assert.Equal(!staff, actor.CanDeleteExam);
        Assert.Equal(!staff, actor.CanCancelExam);
    }
}
