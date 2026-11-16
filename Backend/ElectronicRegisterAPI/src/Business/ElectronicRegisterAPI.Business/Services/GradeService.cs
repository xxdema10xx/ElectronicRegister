// "using" = porto qui gli strumenti definiti altrove (come import in JavaScript). (vedi GUIDA §5)
using ElectronicRegisterAPI.Domain.Policies;               // le regole (GradeRules)
using ElectronicRegisterAPI.Domain.Exceptions;             // tipi di errore personalizzati
using ElectronicRegisterAPI.Domain.Interfaces.Repositories; // i "contratti" per leggere il database
using ElectronicRegisterAPI.Domain.Interfaces.Services;    // il contratto di questo servizio
using ElectronicRegisterAPI.Domain.Models;                 // i modelli (Grade, Student, ClassSubject…)

namespace ElectronicRegisterAPI.Business.Services;

// SERVIZIO DI BUSINESS: contiene le REGOLE sui voti ("chi può fare cosa").
// ": IGradeService" = questa classe rispetta il contratto IGradeService (elenco di azioni promesse).
public class GradeService : IGradeService
{
    // Il repository è l'unico che parla col database: qui serve per cercare le materie di una classe.
    // "private readonly" = visibile solo qui dentro e assegnato una volta sola.
    private readonly IClassSubjectRepository _classSubjectRepository;

    // Costruttore: il sistema consegna il repository già pronto (iniezione delle dipendenze, GUIDA §5.1).
    public GradeService(IClassSubjectRepository classSubjectRepository)
    {
        _classSubjectRepository = classSubjectRepository;
    }

    // Controlla che il voto sia valido (tra 1 e 10). Delega alla classe delle regole GradeRules.
    // "=>" = scrittura corta di una funzione (GUIDA §5).
    public void EnsureValidGradeValue(decimal value) => GradeRules.EnsureValidValue(value);

    // Verifica che il docente insegni davvero quella materia in quella classe.
    // "async Task" = operazione lenta (accesso al database) che si aspetta con await (GUIDA §4.2).
    public async Task EnsureTeacherTeachesClassSubjectAsync(Guid teacherId, Guid classSubjectId)
    {
        // Cerca nel database l'abbinamento classe+materia.
        var classSubject = await _classSubjectRepository.GetByIdAsync(classSubjectId);
        // Se non esiste, o il docente assegnato è un altro, si lancia un errore e l'operazione si ferma.
        if (classSubject is null || classSubject.TeacherId != teacherId)
        {
            throw new UnauthorizedAccessException("Il docente non insegna questa materia nella classe specificata.");
        }
    }

    // Verifica che il docente sia "proprietario" del voto: può modificarlo solo se insegna quella materia.
    public async Task EnsureTeacherOwnsGradeAsync(Guid teacherId, Grade grade)
    {
        // Si risale alla materia-classe a cui il voto appartiene.
        var classSubject = await _classSubjectRepository.GetByIdAsync(grade.ClassSubjectId);
        if (classSubject is null || classSubject.TeacherId != teacherId)
        {
            throw new UnauthorizedAccessException("Il docente non è autorizzato a modificare questo voto.");
        }
    }

    // Verifica che lo studente frequenti la classe a cui appartiene la materia (non si danno voti a studenti di altre classi).
    public async Task EnsureStudentBelongsToClassSubjectAsync(Student student, ClassSubject classSubject)
    {
        // ClassId è "nullable" (Guid?): lo studente può non avere classe. HasValue = "ce l'ha?".
        if (!student.ClassId.HasValue || student.ClassId.Value != classSubject.ClassId)
        {
            throw new BusinessRuleException("Lo studente non appartiene alla classe della materia specificata.");
        }
    }
}
