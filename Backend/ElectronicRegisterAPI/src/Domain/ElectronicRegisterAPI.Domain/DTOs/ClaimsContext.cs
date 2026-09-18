using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Domain.DTOs;

public record ClaimsContext(Guid UserId, UserRole Role, Guid? StudentId, Guid? TeacherId);
