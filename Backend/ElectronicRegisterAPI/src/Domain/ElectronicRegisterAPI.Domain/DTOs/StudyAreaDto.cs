namespace ElectronicRegisterAPI.Domain.DTOs;

public class StudyAreaDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
}
