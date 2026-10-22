#nullable enable
using System;
using System.Text.Json;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Models;
using Xunit;
namespace ElectronicRegisterAPI.Tests.Unit;

public sealed class InstitutionClassRulesTests
{
    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("  \t\r\n ")] [InlineData("123456789012345678901")]
    public void InvalidNamesAreRejected(string? name) => Assert.Throws<ArgumentException>(() => InstitutionClassRules.Name(name));
    [Fact]
    public void NameIsTrimmedBeforeLengthValidation() => Assert.Equal("12345678901234567890", InstitutionClassRules.Name("  12345678901234567890  "));
    [Theory]
    [InlineData(0, 20)] [InlineData(1, 0)] [InlineData(1, 101)] [InlineData(int.MaxValue, 100)]
    public void InvalidPaginationIsRejected(int page, int size) => Assert.Throws<ArgumentException>(() => InstitutionClassRules.Page(page, size));
    [Fact]
    public void BoundaryPaginationIsAccepted() { InstitutionClassRules.Page(1, 1); InstitutionClassRules.Page(1, 100); }
    [Theory]
    [InlineData("InstitutionId")] [InlineData("CourseEditionId")] [InlineData("Id")] [InlineData("TeacherId")]
    public void UnknownBodyMembersFailClosed(string member)
    {
        var json = "{\"Name\":\"A\",\"" + member + "\":\"" + Guid.NewGuid() + "\"}";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CreateInstitutionClassDto>(json));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<UpdateInstitutionClassDto>(json));
    }
}
