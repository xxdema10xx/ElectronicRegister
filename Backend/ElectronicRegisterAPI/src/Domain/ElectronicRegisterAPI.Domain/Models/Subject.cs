// Domain/Models/Subject.cs
namespace ElectronicRegisterAPI.Domain.Models;

public class Subject
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
}