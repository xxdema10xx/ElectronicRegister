// Domain/Models/User.cs
using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Domain.Models;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public UserRole Role { get; set; }
    public Guid? StudentId { get; set; }
    public Guid? TeacherId { get; set; }
}