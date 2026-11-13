using System;
using Xunit;
using ElectronicRegisterAPI.Business.Services;

namespace ElectronicRegisterAPI.Tests.Unit
{
    public class SubmissionGradingServiceTests
    {
        private readonly SubmissionGradingService _svc = new SubmissionGradingService();

        [Theory]
        [InlineData(0, 100, 60, 1.00)]
        [InlineData(20, 100, 60, 2.67)]
        [InlineData(40, 100, 60, 4.33)]
        [InlineData(50, 100, 60, 5.17)]
        [InlineData(60, 100, 60, 6.00)]
        [InlineData(70, 100, 60, 7.00)]
        [InlineData(80, 100, 60, 8.00)]
        [InlineData(90, 100, 60, 9.00)]
        [InlineData(100, 100, 60, 10.00)]
        public void ConvertToGrade_BasicExamples(decimal total, decimal max, decimal passingPercent, decimal expected)
        {
            var actual = _svc.ConvertToGrade(total, max, passingPercent);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ConvertToGrade_BelowThreshold_StaysBelowSix()
        {
            var actual = _svc.ConvertToGrade(59.99m, 100m, 60m);
            Assert.True(actual < 6m);
        }

        [Fact]
        public void ConvertToGrade_ExactThreshold_IsSix()
        {
            var actual = _svc.ConvertToGrade(60m, 100m, 60m);
            Assert.Equal(6.00m, actual);
        }

        [Fact]
        public void ConvertToGrade_12of20_with60pct_IsSix()
        {
            var actual = _svc.ConvertToGrade(12m, 20m, 60m);
            Assert.Equal(6.00m, actual);
        }

        [Fact]
        public void ConvertToGrade_14of20_with60pct_IsSeven()
        {
            var actual = _svc.ConvertToGrade(14m, 20m, 60m);
            Assert.Equal(7.00m, actual);
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
