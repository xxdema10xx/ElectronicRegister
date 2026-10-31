using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Domain.Models;

public sealed record MembershipMigrationAssignment(Guid UserId, IReadOnlyList<InstitutionRole> Roles);
