using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Domain.DTOs;

public sealed record ClassSubjectChoiceDto(Guid ClassSubjectId, Guid ClassId, string ClassName, Guid SubjectId,
    string SubjectName, Guid TeacherId, string TeacherFirstName, string TeacherLastName);
public sealed class SubmissionSessionDto
{
    public Guid SubmissionId { get; set; }
    public Guid ExamId { get; set; }
    public SubmissionStatus Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime DeadlineAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public long CurrentRevision { get; set; }
    public required ExamForStudentDto Exam { get; set; }
    public List<AnswerInputDto> Answers { get; set; } = [];
}
public sealed class InstitutionViolationDto
{
    public Guid EventId { get; set; }
    public string Type { get; set; } = "";
    public string? Metadata { get; set; }
}
public sealed record ViolationReceiptDto(Guid EventId, int ViolationCount, bool Duplicate);
public sealed record ViolationEventDto(Guid EventId, string Type, DateTime OccurredAt, string? Metadata);
