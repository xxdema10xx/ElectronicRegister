namespace ElectronicRegisterAPI.Domain.DTOs
{
    public class CreateUserDto
    {
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string Role { get; set; } = null!;
        public Guid? StudentId { get; set; }
        public Guid? TeacherId { get; set; }
    }
}
