namespace ElectronicRegisterAPI.DTOs
{
    public class RegisterForAdminDto
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public string Role { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
    }
}