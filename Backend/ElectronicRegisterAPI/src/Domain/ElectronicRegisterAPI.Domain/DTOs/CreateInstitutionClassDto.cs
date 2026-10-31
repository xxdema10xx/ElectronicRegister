using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.DTOs;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CreateInstitutionClassDto
{
    public string? Name { get; set; }
    public Guid BienniumStudyPathId { get; set; }
}
