using System.ComponentModel.DataAnnotations;

namespace ElectronicRegisterAPI.Domain.DTOs;

public sealed record InstitutionAccessDto(string Token, int ExpiresInSeconds);
