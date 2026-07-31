namespace ElectronicRegisterAPI.Domain.DTOs
{
    public class CreateSubjectDto
    {
        public required string Name { get; set; }
        public Guid TeacherId { get; set; }
    }
}