// Strumenti usati da questo file (vedi GUIDA §5).
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Api.Security;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using Microsoft.AspNetCore.Authorization;   // [Authorize]: controllo dei ruoli
using Microsoft.AspNetCore.Mvc;             // controller, [HttpGet], Ok(), NotFound()…

namespace ElectronicRegisterAPI.Api.Controllers;

// CONTROLLER DEI VOTI: la "reception" a cui il frontend bussa per tutto ciò che riguarda i voti.
// Gli attributi tra quadre danno istruzioni al sistema (vedi GUIDA §5.2):
//  - [ApiController]: questa classe risponde a richieste HTTP
//  - StaffBodyContract / InstitutionExamEndpoint: regole di sicurezza/controllo specifiche del progetto
//  - Route: indirizzo base → tutte le richieste partono da /api/institution/grades
// "(IInstitutionExamManager manager)" = il manager viene consegnato dal sistema (iniezione, GUIDA §5.1).
[ApiController, StaffBodyContract, InstitutionExamEndpoint, Route("api/institution/grades")]
public sealed class InstitutionGradeController(IInstitutionExamManager manager) : ControllerBase
{
    // GET /api/institution/grades → elenco di tutti i voti. Possono chiamarlo i quattro ruoli elencati.
    // HttpContext.RequestAborted = segnale "il cliente ha annullato la richiesta", per smettere di lavorare.
    [HttpGet, Authorize(Roles = "InstitutionAdmin,Staff,Teacher,Student")]
    public async Task<ActionResult<List<GradeDto>>> GetAll() => Ok(await manager.GradesAsync(HttpContext.RequestAborted));

    // GET /api/institution/grades/{id} → un voto solo. {id:guid} = nell'indirizzo c'è l'identificatore.
    // "is { } row" = se il risultato esiste lo chiama row e risponde 200 (Ok), altrimenti 404 (NotFound).
    [HttpGet("{id:guid}"), Authorize(Roles = "InstitutionAdmin,Staff,Teacher,Student")]
    public async Task<ActionResult<GradeDto>> GetById(Guid id) => await manager.GradeAsync(id, HttpContext.RequestAborted)is { } row ? Ok(row) : NotFound();

    // GET /api/institution/grades/count → quanti voti ci sono.
    [HttpGet("count"), Authorize(Roles = "InstitutionAdmin,Staff,Teacher,Student")]
    public async Task<ActionResult<int>> Count() => Ok(await manager.GradeCountAsync(HttpContext.RequestAborted));

    // GET /api/institution/grades/paged → i voti a pagine (non tutti insieme), con filtri facoltativi.
    // [FromQuery] = il valore arriva dall'indirizzo (?pageNumber=2). Il "= 1" è il valore predefinito.
    [HttpGet("paged"), Authorize(Roles = "InstitutionAdmin,Staff,Teacher,Student")]
    public async Task<ActionResult<GradePageDto>> Paged(
        [FromQuery] int pageNumber = 1,       // numero di pagina
        [FromQuery] int pageSize = 20,        // quanti voti per pagina
        [FromQuery] Guid? subjectId = null,   // filtro per materia (? = facoltativo)
        [FromQuery] Guid? studentId = null,   // filtro per studente
        [FromQuery] DateOnly? date = null     // filtro per data
    ) => Ok(await manager.GradePageAsync(pageNumber, pageSize, subjectId, studentId, date, HttpContext.RequestAborted));

    // GET .../filters → i valori disponibili per riempire i menu dei filtri.
    [HttpGet("filters"), Authorize(Roles = "InstitutionAdmin,Staff,Teacher,Student")]
    public async Task<ActionResult<GradeFiltersDto>> Filters() => Ok(await manager.GradeFiltersAsync(HttpContext.RequestAborted));

    // GET .../statistics → statistiche (es. media). Se non ci sono dati risponde 404.
    [HttpGet("statistics"), Authorize(Roles = "InstitutionAdmin,Staff,Teacher,Student")]
    public async Task<ActionResult<GradeStatisticsDto>> Statistics() => await manager.GradeStatisticsAsync(HttpContext.RequestAborted)is { } row ? Ok(row) : NotFound();

    // POST /api/institution/grades → CREA un voto. Gli studenti NON possono (ruoli più ristretti).
    // [InstitutionPermission(...)] = serve anche il permesso specifico "GradeCreate".
    // I dati del voto arrivano nel corpo della richiesta come CreateGradeDto.
    // Risposta 201 (CreatedAtAction) con l'id del nuovo voto, oppure 404 se qualcosa non si trova.
    [HttpPost, Authorize(Roles = "InstitutionAdmin,Staff,Teacher")]
    [InstitutionPermission(InstitutionPermission.GradeCreate)]
    public async Task<ActionResult<Guid>> Add(CreateGradeDto dto) => await manager.AddGradeAsync(dto, HttpContext.RequestAborted)is { } id ? CreatedAtAction(nameof(GetById), new { id }, id) : NotFound();

    // PUT /api/institution/grades/{id} → MODIFICA un voto. Risponde 204 (ok, nulla da restituire) o 404.
    [HttpPut("{id:guid}"), Authorize(Roles = "InstitutionAdmin,Staff,Teacher")]
    [InstitutionPermission(InstitutionPermission.GradeUpdate)]
    public async Task<IActionResult> Update(Guid id, UpdateGradeDto dto) => await manager.UpdateGradeAsync(id, dto, HttpContext.RequestAborted) ? NoContent() : NotFound();

    // DELETE /api/institution/grades/{id} → ELIMINA un voto. Solo l'amministratore dell'istituto.
    [HttpDelete("{id:guid}"), Authorize(Roles = "InstitutionAdmin")]
    [InstitutionPermission(InstitutionPermission.GradeDelete)]
    public async Task<IActionResult> Delete(Guid id) => await manager.DeleteGradeAsync(id, HttpContext.RequestAborted) ? NoContent() : NotFound();
}
