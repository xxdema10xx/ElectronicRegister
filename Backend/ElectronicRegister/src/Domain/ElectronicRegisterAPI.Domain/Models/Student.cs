// Domain/Models/Student.cs
namespace ElectronicRegisterAPI.Domain.Models;

public class Student
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
}