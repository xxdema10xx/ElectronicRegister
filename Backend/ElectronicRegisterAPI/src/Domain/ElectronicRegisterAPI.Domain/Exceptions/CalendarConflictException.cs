using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Exceptions;

public sealed class CalendarConflictException(string code, object? details = null) : Exception(code)
{
    public string Code { get; } = code;
    public object? Details { get; } = details;
}
