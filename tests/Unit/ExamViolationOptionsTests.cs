using ElectronicRegisterAPI.Domain.Configuration;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Unit;

public sealed class ExamViolationOptionsTests
{
    [Fact]
    public void DefaultIsDisabledAndValid()
    {
        var options = new ExamViolationOptions();
        Assert.Equal(0, options.ViolationAutoSubmitThreshold);
        Assert.True(ExamViolationOptions.Valid(options));
        Assert.False(options.ReachesThreshold(128));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(3, true)]
    [InlineData(128, true)]
    [InlineData(-1, false)]
    [InlineData(129, false)]
    [InlineData(int.MaxValue, false)]
    public void OnlyZeroOrOneTo128AreAccepted(int threshold, bool valid) =>
        Assert.Equal(valid, ExamViolationOptions.Valid(new() { ViolationAutoSubmitThreshold = threshold }));

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(4, true)]
    public void ThresholdIsReachedAtOrAboveTheConfiguredCount(int count, bool reached) =>
        Assert.Equal(reached, new ExamViolationOptions { ViolationAutoSubmitThreshold = 3 }.ReachesThreshold(count));
}
