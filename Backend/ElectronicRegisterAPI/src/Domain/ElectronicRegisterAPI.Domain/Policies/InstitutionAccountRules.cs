using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Policies;

// Decision 3: InstitutionAdmin creates accounts of every role of its institution.
// Staff creates only Student or Teacher accounts (one role, never Staff or
// InstitutionAdmin), the same people for whom it already creates profiles.
// Platform identity: first name, last name and fiscal code are always required (E2, P4).
public static class InstitutionAccountRules
{
    public static void Validate(CreateInstitutionAccount r)
    {
        ArgumentNullException.ThrowIfNull(r);
        var student = r.Roles?.Contains(InstitutionRole.Student) == true;
        var teacher = r.Roles?.Contains(InstitutionRole.Teacher) == true;
        Require(r.EventId != Guid.Empty, "EventId obbligatorio.");
        Require(r.Roles is { Length: >= 1 and <= 4 } && r.Roles.All(Enum.IsDefined) && r.Roles.Distinct().Count() == r.Roles.Length, "Ruoli non validi: da uno a quattro ruoli distinti.");
        Require(!string.IsNullOrWhiteSpace(r.Reason) && r.Reason.Length <= 500 && !r.Reason.Any(char.IsControl), "Motivazione obbligatoria, massimo 500 caratteri, senza caratteri di controllo.");
        Require(Name(r.FirstName), "Nome obbligatorio, massimo 100 caratteri, senza caratteri di controllo.");
        Require(Name(r.LastName), "Cognome obbligatorio, massimo 100 caratteri, senza caratteri di controllo.");
        Require(FiscalCodeRules.IsValid(FiscalCodeRules.Normalize(r.FiscalCode)), "Codice fiscale non valido.");
        Require(!(student && teacher), "Un account non può avere insieme i ruoli Student e Teacher.");
        Require(!r.StudentId.HasValue || student && r.StudentId != Guid.Empty, "StudentId richiede il ruolo Student e un identificativo valido.");
        Require(!r.TeacherId.HasValue || teacher && r.TeacherId != Guid.Empty, "TeacherId richiede il ruolo Teacher e un identificativo valido.");
        Require(!r.ClassId.HasValue || student && !r.StudentId.HasValue && r.ClassId != Guid.Empty, "ClassId è ammesso solo per una nuova scheda Student.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new ArgumentException(message);
    }

    private static bool Name(string? value) => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 100 && !value.Any(char.IsControl);

    public static void RequireAllowed(TenantContext actor, IReadOnlyCollection<InstitutionRole> requested)
    {
        if (actor.Roles.Contains(InstitutionRole.InstitutionAdmin))
            return;
        if (actor.Roles.Contains(InstitutionRole.Staff) && requested.Count == 1 && requested.Single() is InstitutionRole.Student or InstitutionRole.Teacher)
            return;
        throw new UnauthorizedAccessException("Creazione account non autorizzata per questi ruoli.");
    }
}
