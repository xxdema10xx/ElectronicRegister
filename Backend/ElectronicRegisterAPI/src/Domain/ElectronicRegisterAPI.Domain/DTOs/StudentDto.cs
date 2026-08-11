namespace ElectronicRegisterAPI.Domain.DTOs
{
    public class StudentDto
    {
        public Guid Id { get; set; }
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
    }
}
