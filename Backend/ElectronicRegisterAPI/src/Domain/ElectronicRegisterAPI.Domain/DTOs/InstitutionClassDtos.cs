using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.DTOs;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CreateInstitutionClassDto
{
    public string? Name { get; set; }
    public Guid BienniumStudyPathId { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class UpdateInstitutionClassDto
{
    public string? Name { get; set; }
}

public sealed record InstitutionClassDto(Guid Id, string Name, Guid BienniumStudyPathId,
    Guid? CourseEditionId, long Revision);
public sealed record InstitutionClassPageDto(IReadOnlyList<InstitutionClassDto> Items,
    int TotalCount, int PageNumber, int PageSize);
