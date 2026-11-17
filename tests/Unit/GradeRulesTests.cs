using System;
using ElectronicRegisterAPI.Domain.Policies;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Unit
{
    public class GradeRulesTests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(17)]
        [InlineData(18)]
        [InlineData(30)]
        public void EnsureValidValue_AcceptsWholeGradesInScale(decimal value)
        {
            GradeRules.EnsureValidValue(value);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(31)]
        [InlineData(17.5)]
        [InlineData(0.5)]
        [InlineData(30.01)]
        [InlineData(-0.01)]
        public void EnsureValidValue_RejectsOutOfRangeAndNonIntegerValues(decimal value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => GradeRules.EnsureValidValue(value));
        }

        [Fact]
        public void EnsureValidValue_AcceptsIntegerWithTrailingZeroScale()
        {
            GradeRules.EnsureValidValue(24.00m);
        }

        [Fact]
        public void IsPassing_SplitsAtEighteen()
        {
            Assert.False(GradeRules.IsPassing(17));
            Assert.True(GradeRules.IsPassing(18));
            Assert.True(GradeRules.IsPassing(30));
            Assert.False(GradeRules.IsPassing(0));
        }

        [Fact]
        public void Scale_IsZeroToThirtyPassingEighteen()
        {
            Assert.Equal(0m, GradeRules.Minimum);
            Assert.Equal(30m, GradeRules.Maximum);
            Assert.Equal(18m, GradeRules.Passing);
        }
    }
}
