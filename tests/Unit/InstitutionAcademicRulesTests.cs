#nullable enable
using System;
using ElectronicRegisterAPI.Domain.Models;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Unit;
public sealed class InstitutionAcademicRulesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    public void NameRequiresContent(string? value) => Assert.Throws<ArgumentException>(() => InstitutionAcademicRules.Name(value));
    [Fact]
    public void NameTrimsAndEnforcesDatabaseLimit()
    {
        Assert.Equal("Area", InstitutionAcademicRules.Name(" Area "));
        Assert.Throws<ArgumentException>(() => InstitutionAcademicRules.Name(new string ('x', 101)));
    }

    [Fact]
    public void TextLimitCountsUtf8Bytes()
    {
        Assert.Null(InstitutionAcademicRules.Description(null));
        Assert.Equal("", InstitutionAcademicRules.RequiredDescription(""));
        Assert.Throws<ArgumentException>(() => InstitutionAcademicRules.RequiredDescription(null));
        Assert.Throws<ArgumentException>(() => InstitutionAcademicRules.Description(new string ('\u20ac', 21846)));
    }

    [Fact]
    public void DatesPreserveDateOnlyAndAllowEqualBounds()
    {
        InstitutionAcademicRules.Dates(new(1000, 1, 1), new(1000, 1, 1));
        InstitutionAcademicRules.Dates(new(9999, 12, 31), new(9999, 12, 31));
        Assert.Throws<ArgumentException>(() => InstitutionAcademicRules.Dates(new(999, 12, 31), new(1000, 1, 1)));
        Assert.Throws<ArgumentException>(() => InstitutionAcademicRules.Dates(new(2030, 1, 2), new(2030, 1, 1)));
    }

    [Fact]
    public void EmptyReferenceIsInvalid() => Assert.Throws<ArgumentException>(() => InstitutionAcademicRules.Id(Guid.Empty));
}
