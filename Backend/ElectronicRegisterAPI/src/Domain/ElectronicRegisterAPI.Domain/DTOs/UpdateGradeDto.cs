namespace ElectronicRegisterAPI.Domain.DTOs;

public class UpdateGradeDto
{
    public Guid SubjectId { get; set; }
    public decimal Value { get; set; }
    public DateOnly Date { get; set; }
    /// <summary>Optional: omitted (null) keeps the stored description, blank clears it.</summary>
    public string? Description { get; set; }
}
