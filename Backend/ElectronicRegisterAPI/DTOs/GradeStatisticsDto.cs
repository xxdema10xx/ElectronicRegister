namespace ElectronicRegisterAPI.DTOs
{
    public class GradeStatisticsDto
    {
        public decimal yearlyAverage { get; set; }
        public decimal?[] monthlyAverage { get; set; } = new decimal?[12];
    }
}