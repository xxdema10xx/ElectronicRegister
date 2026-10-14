// Domain/Models/Teacher.cs
namespace ElectronicRegisterAPI.Domain.Models;

public class Teacher
{
    public Guid Id { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
}