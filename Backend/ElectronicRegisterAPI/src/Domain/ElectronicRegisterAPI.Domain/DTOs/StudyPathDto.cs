namespace ElectronicRegisterAPI.Domain.DTOs;

public class StudyPathDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
}
