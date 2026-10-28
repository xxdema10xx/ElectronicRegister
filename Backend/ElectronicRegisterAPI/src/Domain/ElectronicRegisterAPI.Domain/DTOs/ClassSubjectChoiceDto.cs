using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Domain.DTOs;

public sealed record ClassSubjectChoiceDto(
    Guid ClassSubjectId,
    Guid ClassId,
    string ClassName,
    Guid SubjectId,
    string SubjectName,
    Guid TeacherId,
    string TeacherFirstName,
    string TeacherLastName
);
