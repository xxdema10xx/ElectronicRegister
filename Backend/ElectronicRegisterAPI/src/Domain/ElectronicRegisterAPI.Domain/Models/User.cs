// Domain/Models/User.cs
using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Domain.Models;

public class User
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public UserRole Role { get; set; }
    public Guid? StudentId { get; set; }
    public Guid? TeacherId { get; set; }
}