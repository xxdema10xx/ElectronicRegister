namespace ElectronicRegisterAPI.Domain.DTOs;
public class ExamDto
{
    public Guid Id { get; set; }
    public Guid ClassId { get; set; }
    public string? ClassName { get; set; }
    public Guid? StudyPathId { get; set; }
    public string? StudyPathName { get; set; }
    public Guid SubjectId { get; set; }
    public string? SubjectName { get; set; }
    public DateOnly Date { get; set; }
    public bool IsRetake { get; set; }
}