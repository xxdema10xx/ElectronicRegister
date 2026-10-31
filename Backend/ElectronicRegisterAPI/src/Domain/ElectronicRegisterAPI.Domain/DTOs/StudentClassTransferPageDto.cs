using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.DTOs;

public sealed record StudentClassTransferPageDto(
    IReadOnlyList<StudentClassTransferHistoryDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize,
    long CurrentRevision
);
