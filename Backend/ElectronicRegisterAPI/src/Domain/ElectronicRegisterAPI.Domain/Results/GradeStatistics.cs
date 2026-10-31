using System;
using System.Collections.Generic;
using System.Text;

namespace ElectronicRegisterAPI.Domain.Results;

public class GradeStatistics
{
    public decimal YearlyAverage { get; set; }
    public decimal? [] MonthlyAverage { get; set; } = new decimal? [12];
}
