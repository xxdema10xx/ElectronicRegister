namespace ElectronicRegisterAPI.Domain.DTOs;

public class UpdateGradeDto
{
    public Guid SubjectId { get; set; }
    public decimal Value { get; set; }
    public DateOnly Date { get; set; }
    /// <summary>Optional. Absent and null are the same (System.Text.Json): the stored description is kept.
    /// "" or whitespace clears it; any other text replaces it (trimmed).</summary>
    public string? Description { get; set; }
}
