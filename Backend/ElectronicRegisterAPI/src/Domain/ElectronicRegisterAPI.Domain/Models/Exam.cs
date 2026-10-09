namespace ElectronicRegisterAPI.Domain.Models;
public class Exam
{
    public Guid Id { get; set; }
    public Guid ClassSubjectId { get; set; }
    public DateOnly Date { get; set; }
}