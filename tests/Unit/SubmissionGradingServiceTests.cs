using System;
using Xunit;
using ElectronicRegisterAPI.Business.Services;
using ElectronicRegisterAPI.Domain.Policies;

namespace ElectronicRegisterAPI.Tests.Unit
{
    public class SubmissionGradingServiceTests
    {
        private readonly SubmissionGradingService _svc = new SubmissionGradingService();

        [Theory]
        [InlineData(0, 100, 60, 0)]
        [InlineData(20, 100, 60, 6)]
        [InlineData(40, 100, 60, 12)]
        [InlineData(50, 100, 60, 15)]
        [InlineData(60, 100, 60, 18)]
        [InlineData(70, 100, 60, 21)]
        [InlineData(80, 100, 60, 24)]
        [InlineData(90, 100, 60, 27)]
        [InlineData(100, 100, 60, 30)]
        [InlineData(0, 100, 50, 0)]
        [InlineData(25, 100, 50, 9)]
        [InlineData(50, 100, 50, 18)]
        [InlineData(75, 100, 50, 24)]
        [InlineData(100, 100, 50, 30)]
        [InlineData(70, 100, 80, 16)]
        [InlineData(80, 100, 80, 18)]
        [InlineData(90, 100, 80, 24)]
        [InlineData(100, 100, 80, 30)]
        public void ConvertToGrade_BasicExamples(decimal total, decimal max, decimal passingPercent, decimal expected)
        {
            var actual = _svc.ConvertToGrade(total, max, passingPercent);
            Assert.Equal(expected, actual);
            Assert.Equal(decimal.Truncate(actual), actual);
        }

        [Fact]
        public void ConvertToGrade_ZeroPoints_IsZero()
        {
            Assert.Equal(0m, _svc.ConvertToGrade(0m, 10m, 60m));
        }

        [Fact]
        public void ConvertToGrade_JustBelowThreshold_IsSeventeenNeverEighteen()
        {
            // 59.99 / 100 * 30 = 17.997 would round to 18: the cap keeps it insufficient.
            var actual = _svc.ConvertToGrade(59.99m, 100m, 60m);
            Assert.Equal(17m, actual);
            Assert.False(GradeRules.IsPassing(actual));
        }

        [Fact]
        public void ConvertToGrade_ExactThreshold_IsEighteen()
        {
            Assert.Equal(18m, _svc.ConvertToGrade(60m, 100m, 60m));
            Assert.Equal(18m, _svc.ConvertToGrade(12m, 20m, 60m));
        }

        [Fact]
        public void ConvertToGrade_14of20_with60pct_IsTwentyOne()
        {
            Assert.Equal(21m, _svc.ConvertToGrade(14m, 20m, 60m));
        }

        [Fact]
        public void ConvertToGrade_FullScore_IsThirty()
        {
            Assert.Equal(30m, _svc.ConvertToGrade(7m, 7m, 60m));
        }

        [Fact]
        public void ConvertToGrade_AboveThreshold_RoundsMidpointAwayFromZero()
        {
            // p = 0.65, s = 0.6 -> 18 + 12 * 0.125 = 19.5 -> 20
            Assert.Equal(20m, _svc.ConvertToGrade(65m, 100m, 60m));
        }

        [Theory]
        [InlineData(1, 0, 60)]
        [InlineData(-1, 100, 60)]
        [InlineData(101, 100, 60)]
        [InlineData(50, 100, 0)]
        [InlineData(50, 100, 100)]
        public void ConvertToGrade_InvalidInputs_Throw(decimal total, decimal max, decimal passingPercent)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _svc.ConvertToGrade(total, max, passingPercent));
        }
    }
}
