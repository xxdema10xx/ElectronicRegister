using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.DTOs;

public sealed record EditionClassDto(
    Guid ClassId,
    string Name,
    Guid BienniumStudyPathId,
    Guid CourseEditionId
);
