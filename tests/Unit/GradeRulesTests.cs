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

        [Theory]
        [InlineData(0, true)]
        [InlineData(30, true)]
        [InlineData(24.00, true)]
        [InlineData(17.5, false)]
        [InlineData(-1, false)]
        [InlineData(31, false)]
        public void IsValidValue_MatchesEnsureValidValue(decimal value, bool valid)
        {
            Assert.Equal(valid, GradeRules.IsValidValue(value));
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("", null)]
        [InlineData("   ", null)]
        [InlineData("  Verifica scritta  ", "Verifica scritta")]
        public void NormalizeDescription_TrimsAndTreatsBlankAsAbsent(string input, string expected)
        {
            Assert.Equal(expected, GradeRules.NormalizeDescription(input));
        }

        [Fact]
        public void NormalizeDescription_AcceptsTwoHundredCharactersAndRejectsMore()
        {
            Assert.Equal(200, GradeRules.NormalizeDescription(new string('a', 200))!.Length);
            Assert.Throws<ArgumentException>(() => GradeRules.NormalizeDescription(new string('a', 201)));
        }

        [Theory]
        [InlineData("riga\nnuova")]
        [InlineData("tab\there")]
        [InlineData("bell\u0007")]
        public void NormalizeDescription_RejectsControlCharacters(string input)
        {
            Assert.Throws<ArgumentException>(() => GradeRules.NormalizeDescription(input));
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
