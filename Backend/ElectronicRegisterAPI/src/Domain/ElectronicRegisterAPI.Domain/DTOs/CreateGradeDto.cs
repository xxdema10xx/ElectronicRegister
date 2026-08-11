namespace ElectronicRegisterAPI.Domain.DTOs
{
    public class CreateGradeDto
    {
        public Guid StudentId { get; set; }
        public Guid SubjectId { get; set; }
        public decimal Value { get; set; }
        public DateOnly Date { get; set; }
    }
}