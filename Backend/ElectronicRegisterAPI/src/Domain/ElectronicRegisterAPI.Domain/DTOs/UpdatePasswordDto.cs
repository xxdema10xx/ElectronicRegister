namespace ElectronicRegisterAPI.Domain.DTOs;

public class UpdatePasswordDto
{
    public required string OldPassword { get; set; }
    public required string NewPassword { get; set; }
}