using Xunit;

namespace ElectronicRegisterAPI.Tests.Unit;

public sealed class CodeOrganizationPathTests
{
    [Theory]
    [InlineData("DTOs/Calendar", "ElectronicRegisterAPI.Domain.DTOs.Calendar")]
    [InlineData("DTOs\\Calendar", "ElectronicRegisterAPI.Domain.DTOs.Calendar")]
    [InlineData("DTOs/Calendar\\Import", "ElectronicRegisterAPI.Domain.DTOs.Calendar.Import")]
    [InlineData("Interfaces", "ElectronicRegisterAPI.Domain.Interfaces")]
    [InlineData(".", "ElectronicRegisterAPI.Domain")]
    [InlineData("", "ElectronicRegisterAPI.Domain")]
    public void ExpectedNamespaceIsIndependentOfPathSeparators(string directory, string expected)
    {
        Assert.Equal(expected, NamespacePath.ExpectedNamespace("ElectronicRegisterAPI.Domain", directory));
    }

    [Theory]
    [InlineData("Controllers/Admin")]
    [InlineData("Controllers\\Admin")]
    public void ApiNamespaceUsesTheSuppliedRoot(string directory)
    {
        Assert.Equal("ElectronicRegisterAPI.Api.Controllers.Admin", NamespacePath.ExpectedNamespace("ElectronicRegisterAPI.Api", directory));
    }
}
