// Domain/Models/Subject.cs
namespace ElectronicRegisterAPI.Domain.Models;

public class Subject
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public Guid TeacherId { get; set; }
}