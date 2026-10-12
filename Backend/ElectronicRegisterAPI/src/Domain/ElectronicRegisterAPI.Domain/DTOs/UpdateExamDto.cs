namespace ElectronicRegisterAPI.Domain.DTOs;
public class UpdateExamDto
{
    public Guid SubjectId { get; set; }
    public DateOnly Date { get; set; }
}