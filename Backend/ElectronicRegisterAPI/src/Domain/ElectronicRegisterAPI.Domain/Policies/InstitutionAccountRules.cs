using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Policies;

// Decision 3: InstitutionAdmin creates accounts of every role of its institution.
// Staff creates only Student or Teacher accounts (one role, never Staff or
// InstitutionAdmin), the same people for whom it already creates profiles.
public static class InstitutionAccountRules
{
    public static void Validate(CreateInstitutionAccount r)
    {
        var profileFields = r.FirstName is not null || r.LastName is not null || r.ClassId.HasValue;
        var student = r.Roles.Contains(InstitutionRole.Student);
        var teacher = r.Roles.Contains(InstitutionRole.Teacher);
        if (r.EventId == Guid.Empty || r.Roles.Length is < 1 or > 4 || r.Roles.Any(x => !Enum.IsDefined(x)) || r.Roles.Distinct().Count() != r.Roles.Length
            || string.IsNullOrWhiteSpace(r.Reason) || r.Reason.Length > 500 || r.Reason.Any(char.IsControl)
            || student && teacher
            || (r.StudentId.HasValue && (!student || r.StudentId == Guid.Empty || profileFields))
            || (r.TeacherId.HasValue && (!teacher || r.TeacherId == Guid.Empty || profileFields))
            || (student && !r.StudentId.HasValue && !Person(r, requireClass: false))
            || (teacher && !r.TeacherId.HasValue && (!Person(r, requireClass: false) || r.ClassId.HasValue))
            || (!student && !teacher && profileFields))
            throw new ArgumentException("Richiesta di creazione account non valida.");
    }

    private static bool Person(CreateInstitutionAccount r, bool requireClass) =>
        Name(r.FirstName) && Name(r.LastName) && (!requireClass || r.ClassId.HasValue) && r.ClassId != Guid.Empty;

    private static bool Name(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 100 && !value.Any(char.IsControl);

    public static void RequireAllowed(TenantContext actor, IReadOnlyCollection<InstitutionRole> requested)
    {
        if (actor.Roles.Contains(InstitutionRole.InstitutionAdmin))
            return;
        if (actor.Roles.Contains(InstitutionRole.Staff) && requested.Count == 1 && requested.Single() is InstitutionRole.Student or InstitutionRole.Teacher)
            return;
        throw new UnauthorizedAccessException("Creazione account non autorizzata per questi ruoli.");
    }
}
