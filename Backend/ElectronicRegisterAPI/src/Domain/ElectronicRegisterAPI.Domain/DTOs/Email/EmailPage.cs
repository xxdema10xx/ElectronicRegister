using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Email;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmailPage<T>(
    int PageNumber,
    int PageSize,
    long TotalCount,
    IReadOnlyList<T> Items
);
