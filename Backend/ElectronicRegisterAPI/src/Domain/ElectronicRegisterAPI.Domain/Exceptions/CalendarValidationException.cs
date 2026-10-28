using System.Globalization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Exceptions;

// Calendar values are independent of persistence and never authorize an HTTP operation.
public sealed class CalendarValidationException(string code) : ArgumentException(code)
{
    public string Code { get; } = code;
}
