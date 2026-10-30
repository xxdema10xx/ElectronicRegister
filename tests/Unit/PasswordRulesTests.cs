#nullable enable
using System;
using ElectronicRegisterAPI.Domain.Policies;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Unit;

public sealed class PasswordRulesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short!")]
    [InlineData("NoSpecialCharacter")]
    public void InvalidPasswordsAreRejected(string? value) => Assert.Throws<ArgumentException>(() => PasswordRules.Validate(value));

    [Fact]
    public void BcryptLimitIsMeasuredInUtf8BytesAndNeverSilentlyTruncates()
    {
        PasswordRules.Validate(new string('a', 71) + "!");
        Assert.Throws<ArgumentException>(() => PasswordRules.Validate(new string('a', 72) + "!"));
        PasswordRules.Validate(new string('é', 35) + "!");
        Assert.Throws<ArgumentException>(() => PasswordRules.Validate(new string('é', 36) + "!"));
    }
}
