namespace ElectronicRegisterAPI.Domain.DTOs
{
    public class GradeStatisticsDto
    {
        public decimal YearlyAverage { get; set; }
        public decimal?[] MonthlyAverage { get; set; } = new decimal?[12];
    }
}