using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Import;

public sealed record ImportCreatedRow(
    int RowNumber,
    Guid? StudentId,
    Guid? TeacherId,
    Guid? EmailAssignmentId
);
