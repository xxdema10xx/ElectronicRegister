namespace ElectronicRegisterAPI.Domain.DTOs;
public class CreateExamDto
{
    public Guid SubjectId { get; set; }
    public DateOnly Date { get; set; }
}