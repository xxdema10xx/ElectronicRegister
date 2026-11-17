namespace ElectronicRegisterAPI.Domain.DTOs;

public class GradeDto
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public Guid SubjectId { get; set; }
    public string? SubjectName { get; set; }
    public Guid TeacherId { get; set; }
    public decimal Value { get; set; }
    public DateOnly Date { get; set; }
    public string? Description { get; set; }
    /// <summary>"exam" for the official grade of an exam submission, "manual" otherwise (single or batch).</summary>
    public string Source { get; set; } = GradeSources.Manual;
    /// <summary>Exam and submission of an exam grade; null for manual grades.</summary>
    public Guid? ExamId { get; set; }
    public Guid? SubmissionId { get; set; }
    public StudentDto? Student { get; set; }
}
