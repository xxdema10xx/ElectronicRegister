namespace ElectronicRegisterAPI.Domain.DTOs;
public class ExamDto
{
    public Guid Id { get; set; }
    public Guid ClassSubjectId { get; set; }
    public string? ClassSubjectName { get; set; }
    public DateOnly Date { get; set; }
}