namespace ElectronicRegisterAPI.Domain.DTOs
{
    public class UserDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public Guid? StudentId { get; set; }
        public string? StudentFirstName { get; set; }
        public string? StudentLastName { get; set; }
        public Guid? TeacherId { get; set; }
        public string? TeacherFirstName { get; set; }
        public string? TeacherLastName { get; set; }
    }
}
