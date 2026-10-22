using System;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

public sealed class LocalDatabasePortabilityTests
{
    [Theory]
    [InlineData("Server=127.0.0.1;Port=3306;Database=electronic_register_test")]
    [InlineData("Server=localhost;Port=13306;Database=electronic_register_test")]
    [InlineData("Server=remote.example.test;Port=13306;Database=electronic_register_test")]
    [InlineData("Server=127.0.0.1;Port=23306;Database=electronic_register_test")]
    [InlineData("Server=127.0.0.1;Port=13306;Database=operational")]
    [InlineData("Server=127.0.0.1;Port=13306")]
    public void LocalTargetRejectsOtherPortsHostsAndDatabasesBeforeConnecting(string connection)
        => Assert.Throws<InvalidOperationException>(() => IntegrationFixture.ValidateLocalConnectionString(connection));

    [Fact]
    public void ExactIsolatedTargetDoesNotRequireMachineSpecificPasswordOrPath()
    {
        const string connection="Server=127.0.0.1;Port=13306;Database=electronic_register_test;User ID=root";
        Assert.NotEmpty(IntegrationFixture.ValidateLocalConnectionString(connection));
    }
}
