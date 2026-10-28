using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Exceptions;

public sealed class ImportConflictException(string code) : Exception("Il contesto dell'importazione è cambiato; verificare o rigenerare la preview.")
{
    public string Code { get; } = code;
}
