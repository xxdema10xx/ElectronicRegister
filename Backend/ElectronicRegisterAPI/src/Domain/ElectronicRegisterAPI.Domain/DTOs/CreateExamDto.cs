namespace ElectronicRegisterAPI.Domain.DTOs;
public class CreateExamDto
{
    public Guid ClassSubjectId { get; set; }
    public DateOnly Date { get; set; }
}