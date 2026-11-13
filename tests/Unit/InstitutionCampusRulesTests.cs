using ElectronicRegisterAPI.Domain.Policies;
#nullable enable
using System;
using ElectronicRegisterAPI.Domain.Models;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Unit;
public sealed class InstitutionCampusRulesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    public void CampusNameMustHaveContent(string? name) => Assert.Throws<ArgumentException>(() => InstitutionCampusRules.Name(name));
    [Fact]
    public void CampusNameUsesActualLimitAndTrims()
    {
        Assert.Equal("Roma", InstitutionCampusRules.Name(" Roma "));
        Assert.Equal(200, InstitutionCampusRules.Name(new string ('x', 200)).Length);
        Assert.Throws<ArgumentException>(() => InstitutionCampusRules.Name(new string ('x', 201)));
    }

    [Theory]
    [InlineData(100)]
    [InlineData(300)]
    public void OptionalCampusFieldsRespectTheirLimit(int maximum)
    {
        Assert.Null(InstitutionCampusRules.Optional("  ", maximum));
        Assert.Null(InstitutionCampusRules.Optional(null, maximum));
        Assert.Equal(maximum, InstitutionCampusRules.Optional(new string ('x', maximum), maximum)!.Length);
        Assert.Throws<ArgumentException>(() => InstitutionCampusRules.Optional(new string ('x', maximum + 1), maximum));
    }
}
