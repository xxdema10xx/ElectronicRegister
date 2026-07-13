namespace ElectronicRegisterAPI.DTOs
{
    public class AddSubjectDto
    {
        public required string Name { get; set; }
        public Guid TeacherId { get; set; }
    }
}