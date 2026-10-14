// Domain/Models/Student.cs
namespace ElectronicRegisterAPI.Domain.Models;

public class Student
{
    public Guid Id { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public Guid? ClassId { get; set; }
}