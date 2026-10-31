using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.DTOs;

public sealed record InstitutionClassSubjectDto(
    Guid Id,
    Guid ClassId,
    string ClassName,
    Guid SubjectId,
    string SubjectName,
    Guid TeacherId,
    string TeacherFirstName,
    string TeacherLastName,
    long Revision
)
{
    public Guid ClassSubjectId => Id;
}
