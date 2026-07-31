namespace ElectronicRegisterAPI.Domain.DTOs
{
    public class UpdateGradeDto
    {
        public Guid SubjectId { get; set; }
        public decimal Value { get; set; }
        public DateOnly Date { get; set; }
    }
}
