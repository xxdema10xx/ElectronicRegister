namespace ElectronicRegisterAPI.Domain.Enums;

// Separate from historical global roles. Future roles require an explicit schema/authorization change.
public enum InstitutionRole
{
    InstitutionAdmin = 0,
    Staff = 3,
    Teacher = 1,
    Student = 2
}
