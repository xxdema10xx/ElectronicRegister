// Domain/Models/Teacher.cs
namespace ElectronicRegisterAPI.Domain.Models;

public class Teacher
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
}