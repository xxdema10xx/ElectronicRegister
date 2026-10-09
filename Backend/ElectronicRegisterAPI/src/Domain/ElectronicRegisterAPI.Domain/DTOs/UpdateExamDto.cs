namespace ElectronicRegisterAPI.Domain.DTOs;
public class UpdateExamDto
{
    public Guid ClassSubjectId { get; set; }
    public DateOnly Date { get; set; }
}