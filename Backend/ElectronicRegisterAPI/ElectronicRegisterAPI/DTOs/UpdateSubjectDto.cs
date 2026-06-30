namespace ElectronicRegisterAPI.DTOs
{
    public class UpdateSubjectDto
    {
        public required string Name { get; set; }
        public Guid TeacherId { get; set; }
        public string? TeacherFirstName { get; set; }
        public string? TeacherLastName { get; set; }
    }
}
