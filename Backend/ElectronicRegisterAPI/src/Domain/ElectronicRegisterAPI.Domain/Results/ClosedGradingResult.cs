using System;
using System.Collections.Generic;
using System.Text;

namespace ElectronicRegisterAPI.Domain.Results
{
    public class ClosedGradingResult
    {
        public decimal ClosedScore { get; set; }

        public decimal OpenMaxPoints { get; set; }

        public decimal MaxScore { get; set; }

        public decimal PassingScore { get; set; }
    }
}
