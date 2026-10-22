using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.DTOs;
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateInstitutionClassSubjectDto(Guid ClassId, Guid SubjectId, Guid TeacherId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateInstitutionClassSubjectDto(Guid TeacherId);
public sealed record InstitutionClassSubjectDto(Guid Id, Guid ClassId, string ClassName, Guid SubjectId, string SubjectName, Guid TeacherId, string TeacherFirstName, string TeacherLastName, long Revision)
{
    public Guid ClassSubjectId => Id;
}
