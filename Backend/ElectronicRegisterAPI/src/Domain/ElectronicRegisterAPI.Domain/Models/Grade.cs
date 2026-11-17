namespace ElectronicRegisterAPI.Domain.Models;

public class Grade
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public Guid ClassSubjectId { get; set; }
    public decimal Value { get; set; }
    public DateOnly Date { get; set; }
    public string? Description { get; set; }
    /// <summary>Client event of the batch that created this grade; null for single or exam grades.</summary>
    public Guid? BatchEventId { get; set; }
}
