namespace ElectronicRegisterAPI.Domain.DTOs
{
    public class RegisterForAdminDto
    {
        public required string Email { get; set; }
        public required string Password { get; set; }
        public required string Role { get; set; }
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
    }
}