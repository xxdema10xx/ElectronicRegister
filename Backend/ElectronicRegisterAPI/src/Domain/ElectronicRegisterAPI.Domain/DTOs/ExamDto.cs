namespace ElectronicRegisterAPI.Domain.DTOs;
public class ExamDto
{
    public Guid Id { get; set; }
    public Guid SubjectId { get; set; }
    public string? SubjectName { get; set; }
    public DateOnly Date { get; set; }
}