using System.Text.Json;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Exceptions;

public sealed class ImportInputException(string code, string message) : ArgumentException(message)
{
    public string Code { get; } = code;
}
