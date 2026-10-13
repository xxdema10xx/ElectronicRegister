namespace ElectronicRegisterAPI.Domain.DTOs;
public class UpdateExamDto
{
    public Guid ClassId { get; set; }
    public Guid SubjectId { get; set; }
    public DateOnly Date { get; set; }
    public bool IsRetake { get; set; }
}