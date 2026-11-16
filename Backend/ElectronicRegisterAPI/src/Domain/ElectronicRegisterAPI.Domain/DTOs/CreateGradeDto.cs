// Indirizzo del file nel progetto, per evitare confusioni tra nomi uguali. (vedi GUIDA §5)
namespace ElectronicRegisterAPI.Domain.DTOs;

// DTO = "scheda" con i soli dati che il frontend invia per CREARE un voto. (vedi GUIDA §5.3)
public class CreateGradeDto
{
    // Identificatore dello studente che riceve il voto (Guid = codice unico lungo).
    public Guid StudentId { get; set; }
    // Identificatore della materia a cui si riferisce il voto.
    public Guid SubjectId { get; set; }
    // Il voto, numero con la virgola (es. 7.5). I limiti 1-10 sono controllati in GradeRules.
    public decimal Value { get; set; }
    // La data del voto, senza orario.
    public DateOnly Date { get; set; }
}
