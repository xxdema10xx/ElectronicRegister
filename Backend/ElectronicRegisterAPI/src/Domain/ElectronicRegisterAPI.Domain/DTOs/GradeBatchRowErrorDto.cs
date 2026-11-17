namespace ElectronicRegisterAPI.Domain.DTOs;

/// <summary>Validation error of one batch row; Index is the zero-based position in Grades.</summary>
public class GradeBatchRowErrorDto
{
    public int Index { get; set; }
    public Guid StudentId { get; set; }
    public required string Code { get; set; }
    public required string Message { get; set; }
}
