using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Domain.Models;

// Institution-scoped account creation. The institution always comes from the
// authenticated tenant, never from the request.
public sealed record CreateInstitutionAccount(
    Guid EventId,
    string Email,
    InstitutionRole[] Roles,
    Guid? StudentId,
    Guid? TeacherId,
    string? FirstName,
    string? LastName,
    Guid? ClassId,
    string Reason
);
