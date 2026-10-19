namespace ElectronicRegisterAPI.Domain.Models;

public class Campus
{
    public Guid Id { get; set; }
    public Guid InstitutionId { get; set; }
    public required string Name { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Province { get; set; }
    public bool IsActive { get; set; } = true;
}
