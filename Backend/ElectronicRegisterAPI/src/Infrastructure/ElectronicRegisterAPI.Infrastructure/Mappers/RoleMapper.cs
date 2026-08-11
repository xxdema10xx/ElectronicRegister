using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Infrastructure.Mappers;

internal static class RoleMapper
{
    public static string ToDbString(UserRole role) => role switch
    {
        UserRole.Admin => "admin",
        UserRole.Teacher => "teacher",
        UserRole.Student => "student",
        _ => throw new ArgumentOutOfRangeException(nameof(role), $"Ruolo non valido: {role}")
    };

    public static UserRole FromDbString(string role) => role.ToLowerInvariant() switch
    {
        "admin" => UserRole.Admin,
        "teacher" => UserRole.Teacher,
        "student" => UserRole.Student,
        _ => throw new ArgumentOutOfRangeException(nameof(role), $"Ruolo non valido: {role}")
    };
}