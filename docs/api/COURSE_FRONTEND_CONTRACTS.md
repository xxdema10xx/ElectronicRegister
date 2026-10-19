# Estensione MT-4C: lifecycle e audit amministrativo

Questa sezione prevale sulle limitazioni MT4B riportate nello snapshot sottostante. MT-4C completata:
due suite 675/675 Passed, zero Failed/Skipped, 644 identita precedenti preservate; rollout chiuso. Stessa InstitutionCourseAccess,
JWT InstitutionAccess e flag server-side CourseApi && SchoolApi. Ogni nuova action richiede
InstitutionAdmin della membership corrente. Teacher/Student non leggono ragioni interne o attori.

| Metodo | Route sotto /api/institution | Input | Output |
| --- | --- | --- | --- |
| POST | /enrollments/{id}/transition | TransitionEnrollmentDto | 200 EnrollmentMutationResult |
| PUT | /enrollments/{id}/completion-date | CorrectEnrollmentCompletionDto | 200 EnrollmentMutationResult |
| GET | /enrollments/{id}/history | pageNumber=1, pageSize=20 | 200 CourseAdministrationEventDto[] |
| GET | /course-editions/{id}/audit | pageNumber=1, pageSize=20 | 200 CourseAdministrationEventDto[] |

TransitionEnrollmentDto: eventId Guid non vuoto, targetStatus string esplicita, completionDate
`yyyy-MM-dd` obbligatoria, reason non bianca max1000, expectedRevision intero non negativo obbligatorio.
CorrectEnrollmentCompletionDto ha gli stessi campi eccetto targetStatus, che non e accettato.
Campi estranei (owner, StudentId, CourseEditionId, EnrollmentDate, Status di rettifica) producono 400.
Body delle mutazioni massimo16KiB (413). ExpectedRevision va preservata come intero Int64 esatto;
le revision oggi piccole non autorizzano arrotondamenti dei client JavaScript su valori futuri grandi.

Il dettaglio e le liste amministrative StudentEnrollmentDto aggiungono `revision` (iniziale0).
CourseEdition conserva la precedente `version` opaca, senza sostituzione con Revision.
Le creazioni MT4B continuano ad accettare tutti i quattro stati tecnici con date esplicite come prima;
nessun cambiamento retroattivo e nessuna transizione dedotta dalle date.

Transizioni: soltanto enrolled -> completed, withdrawn oppure cancelled. Non sono regole normative.
Data esplicita >= EnrollmentDate, motivo obbligatorio. Non si inferisce la data da oggi/UTC o dal
calendario dell'edizione. Uno stato terminale non viene riattivato ne cambiato in un altro terminale.
La rettifica modifica soltanto CompletionDate di un'iscrizione terminale; puo valorizzare una data
precedentemente sconosciuta, preservando null nello storico. Data invariata o stato enrolled: 409.
Non cambia Status, EnrollmentDate, owner, studente, edizione o classe attuale.

Esempio POST /enrollments/{id}/transition (Id e revisione letti dal dettaglio corrente):

```json
{
  "eventId": "44b4f5a1-c33a-4676-8fb9-73c528d39f33",
  "targetStatus": "completed",
  "completionDate": "2026-10-19",
  "reason": "Conclusione amministrativa documentata",
  "expectedRevision": 0
}
```

La risposta 200 contiene lo stesso eventId, revision 1, status completed e la data esplicita.
Per rettificare la data usare PUT /enrollments/{id}/completion-date con nuovo eventId,
expectedRevision 1, reason e completionDate nuova; omettere targetStatus. Per consultare gli eventi:
GET /enrollments/{id}/history?pageNumber=1&pageSize=20, solo con ruolo amministrativo corrente.

EnrollmentMutationResult: `eventId, revision, status, completionDate`. Generare EventId una sola
volta per l'intenzione. In caso di risposta persa, ripetere lo stesso contenuto con lo stesso EventId,
ExpectedRevision e stessa identita/membership. Risultato identico, nessun evento/incremento ulteriore.
L'operazione gia committata puo essere riprodotta anche se il record corrente ha una revision piu
alta: dopo il receipt rileggere GET per lo stato attuale. Stesso EventId con motivo/data/stato/revision,
risorsa o attore diversi produce 409. Non cambiare EventId per nascondere un conflitto di rete.
Dopo 409 riconciliare con l'amministratore e rileggere; un nuovo intento deliberato usa nuova chiave.

History usa ordine RevisionAfter/OccurredAtUtc/EventId; audit edizione UTC/EventId. Pagine come
MT4B, max100; nessun count, 200 [] per risorsa autorizzata senza eventi. Il registro espone EventId,
InstitutionId, ActorUserId, ActorMembershipId, CourseEditionId e riferimenti opzionali EnrollmentId,
CampusId/ClassId; Action, PreviousStatus/NewStatus, PreviousCompletionDate/NewCompletionDate,
RevisionBefore/RevisionAfter, Reason, BeforeJson/AfterJson (stringhe JSON minime) e OccurredAtUtc
ISO UTC con Z. Action distingue creazione/descrizione edizione, link sede, attribuzione gruppo,
creazione iscrizione, transizione e rettifica. Nessuna password/token nel registro.

GET /enrollments/me resta la vista personale corrente, senza reason/actor/membership interni.
Una history personale ridotta non e esposta in MT4C. Non usare la directory amministrativa per
costruire una timeline studente. Nessuna DELETE fisica di Enrollment/eventi, unlink Campus,
trasferimento Class, rientro o secondo episodio nella medesima edizione. Nessuna rettifica stato o
data di iscrizione, nessuna transizione CourseEdition.Status o effetto su Exam/Grade/Submission.

400 DTO/data/motivo, 401 JWT, 403 ruolo/revoca/flag, 404 fuori tenant/inesistente indistinguibili,
409 stato/revision/intenzione incoerente o conflitto SQL, 413 body troppo grande. Nessun dettaglio SQL.
Eventi registrati dopo MT4C e stato attuale sono distinti dalla storia legacy non disponibile. Eventi
precedenti non vengono inventati: una vecchia iscrizione puo non avere history. Assenza di eventi
non dimostra assenza di cambi storici. [Modello lifecycle](../architecture/ENROLLMENT_LIFECYCLE_AND_AUDIT.md),
[report MT4C](../../tests/Integration/MULTITENANCY_MT4C_REPORT.md).

---

# Contratti frontend del modello ITS Academy - MT-4B

API istituzionali sotto `/api/institution`. Nessun cutover o onboarding pubblico. Il client usa
`Authorization: Bearer <InstitutionAccess>` ottenuto dal flusso MT2 login/selezione membership.
Entrambi i flag server-side `Multitenancy:SchoolApi:Enabled` e `Multitenancy:CourseApi:Enabled`
devono essere true. Assenti, falsi o non validi negano. Default false nell'esempio versionato.
ExamApi e InstitutionWorker conservano configurazione e indipendenza precedenti.

Ogni operazione rivalida membership Active, Institution Active e insieme dei ruoli emessi nel JWT.
Ruoli modificati o membership revocata richiedono una nuova autorizzazione. Selection ticket e JWT
legacy non autorizzano queste route. InstitutionId non viene selezionato da header, query o body.
I DTO di scrittura rifiutano campi sconosciuti, compresi InstitutionId e campi immutabili.

## Ruoli e route

A = InstitutionAdmin della membership corrente. S = Student con profilo della stessa membership.
Teacher non ha accesso amministrativo o directory iscrizioni. A non implica S.
Tutte le route seguenti hanno policy `InstitutionCourseAccess`, manager `IInstitutionCourseManager`
e repository `ICourseStructureRepository`, con InstitutionId nel predicato SQL.

| Metodo | Route relativa | Ruolo | Risposta positiva |
| --- | --- | --- | --- |
| GET | /course-editions | A | 200 CourseEditionDto[] |
| GET | /course-editions/{id} | A | 200 CourseEditionDto |
| GET | /course-editions/by-code/{code} | A | 200 CourseEditionDto |
| POST | /course-editions | A | 201 Guid + Location del dettaglio |
| PUT | /course-editions/{id} | A | 204 |
| GET | /course-editions/{id}/campuses | A | 200 CampusDto[] |
| POST | /course-editions/{id}/campuses | A | 201 + Location della collezione |
| GET | /course-editions/{id}/classes | A | 200 EditionClassDto[] |
| POST | /course-editions/{editionId}/classes/{classId} | A | 204 |
| GET | /course-editions/{id}/enrollments | A | 200 StudentEnrollmentDto[] |
| GET | /campuses | A | 200 CampusDto[] |
| GET | /enrollments/{id} | A | 200 StudentEnrollmentDto |
| GET | /enrollments?studentId={id} | A | 200 StudentEnrollmentDto[] |
| POST | /enrollments | A | 201 Guid + Location del dettaglio |
| GET | /enrollments/me | S | 200 OwnEnrollmentDto[] |

Ogni GET di collezione accetta `pageNumber` (default 1, intervallo 1..1000000) e `pageSize`
(default 20, intervallo 1..100). Valori fuori intervallo: 400. Risposta sempre array, 200 `[]`
per pagina valida senza risultati; nessun count totale incluso. Il client puo richiedere pagine
successive. Ordinamento deterministico: edizioni Code/Id, sedi Name/Id, gruppi Name/Id,
iscrizioni EnrollmentDate/EnrollmentId. La paginazione offset non e uno snapshot transazionale
tra richieste: modifiche concorrenti del catalogo possono cambiare i confini delle pagine.

GET /course-editions accetta `studyPathId` facoltativo, applicato in SQL nello stesso tenant.
GET /enrollments richiede `studentId`: non espone un elenco globale senza filtro.
Un parent inesistente/fuori tenant delle collezioni annidate restituisce 404; un parent valido
senza figli restituisce []. /me ignora eventuali identificatori StudentId inviati in query:
la selezione effettiva deriva esclusivamente dal profilo della membership verificata.

## DTO e input

JSON camelCase; Guid stringhe; date didattiche `yyyy-MM-dd`, senza fuso orario.
Campi nullable assenti nella risposta equivalgono a null, secondo configurazione JSON esistente.

CourseEditionDto: `id, institutionId, studyPathId, code, name, startDate?, endDate?, status,
createdAt, updatedAt, version`. InstitutionId in output descrive l'owner autorizzato, non autorizza
richieste successive. CreatedAt/UpdatedAt sono timestamp tecnici DB DATETIME(6), distinti dalle
date didattiche; non costituiscono uno storico delle decisioni. DATETIME non memorizza un offset:
questa fase conserva l'orologio della sessione DB preesistente, senza certificarlo come UTC. La zona
operativa va verificata nel deployment; non usare questi timestamp come date di iscrizione.
Il client usa esclusivamente Version per il controllo di concorrenza.

POST CourseEdition: `studyPathId, code, name, startDate?, endDate?, status`.
Code obbligatorio max64, Name obbligatorio max200, entrambi non bianchi. Code locale, non
ministeriale; unicita per fondazione secondo collation utf8mb4_unicode_ci, quindi senza distinzione
maiuscole/minuscole. Stesso Code consentito in fondazioni diverse. Date opzionali, anno >=1000;
EndDate >= StartDate quando entrambe note. Nessun limite automatico di durata.
Status esplicito fra `planned, active, completed, cancelled`; nessuna inferenza dalle date.

PUT CourseEdition: `code, name, startDate?, endDate?, version`. Tutti i dati descrittivi sono
sostituiti: date omesse/null vengono azzerate. `version` e la stringa opaca di 64 caratteri ricevuta
nell'ultimo GET. Una versione superata restituisce 409: rileggere e far riconciliare le modifiche,
senza retry cieco. Dopo 204 eseguire GET per ottenere la nuova versione. Owner, StudyPath,
CreatedAt e Status non sono modificabili; inviarli produce 400. Il token non e un'autorizzazione.
Lock scoped e confronto sotto transazione impediscono sovrascritture perse tra writer API.

CampusDto: `campusId, name, address?, city?, province?, isActive?`. IsActive conserva la nullabilita
del modello legacy: null non va inventato come true. POST collegamento: `{ "campusId": "guid" }`.
Sede ed edizione devono appartenere alla fondazione corrente. Nessuna sede principale obbligatoria.
Le sedi inattive restano leggibili e collegate; questa fase non introduce una policy di esclusione
per nuovi collegamenti in base allo stato della sede o dell'edizione. Nessun unlink disponibile.

EditionClassDto: `classId, name, bienniumStudyPathId, courseEditionId`. POST assegnazione senza
body: gruppo ed edizione devono avere stesso tenant e StudyPath. Ripetere la stessa assegnazione
e idempotente; assegnare a un'altra edizione produce 409. Nessun trasferimento o azzeramento,
nessuna modifica di ClassSubject o Student.ClassId. I gruppi legacy senza edizione rimangono validi.

StudentEnrollmentDto amministrativo: `enrollmentId, courseEditionId, courseEditionCode,
courseEditionName, studentId, enrollmentDate, completionDate?, status`.
OwnEnrollmentDto contiene gli stessi campi eccetto StudentId. Codice/nome dell'edizione sono i
valori descrittivi correnti, non uno snapshot storico del catalogo.

POST Enrollment: `studentId, courseEditionId, enrollmentDate, completionDate?, status`.
EnrollmentDate e Status sono espliciti, senza default derivati da ClassId. Data iniziale anno >=1000;
CompletionDate >= EnrollmentDate quando presente. Status fra `enrolled, completed, withdrawn,
cancelled`. Tutti leggibili; creazione esplicita anche di un record gia concluso ammessa dal contratto
tecnico, senza attribuirgli una cronologia di transizioni. Date dell'edizione non impongono ulteriori
limiti. Studente e edizione devono appartenere al tenant. Un'iscrizione per Student/edizione,
anche dopo withdrawn/cancelled; piu edizioni contemporanee consentite. Non si creano User,
membership, profili o classi; uno Student senza account puo essere iscritto dall'amministratore.

## Errori e operazioni chiuse

400 input/date/stato/pagina non validi o campi di scrittura sconosciuti; 401 JWT assente/invalido;
403 ruolo/profilo/revoca/istituto/rollout; 404 risorsa inesistente o fuori tenant indistinguibili;
409 unicita, concorrenza o dipendenza/stato strutturale. Errori SQL interni non esposti.
Un collegamento Class con percorso incompatibile e rifiutato come risorsa non disponibile (404).
Nessuna API DELETE fisica, unlink sede, cambio stato, riattivazione, secondo episodio nella stessa
edizione o rettifica delle date di iscrizione. Non presentare pulsanti per queste operazioni.

Le iscrizioni rappresentano data nota, eventuale conclusione nota e stato corrente: **non una
cronologia completa delle decisioni amministrative**. L'audit attuale ammette soltanto exam,
exam_submission e platform con shape specifica: i nuovi tipi non vengono mascherati sotto tali
EntityType. Creazioni e modifiche descrittive non producono ancora un evento audit didattico
append-only; non certificano attore/storia amministrativa. Per rettifiche e transizioni servono
prima policy funzionale, attribuzione audit supportata e atomicita dei relativi eventi.

Nessun backfill di iscrizioni da Student.ClassId o conversione Biennium -> CourseEdition. Per
attribuzioni legacy servono dati amministrativi verificati. Trasferimenti di gruppo/sede e reingresso
richiedono procedure distinte ancora da concordare. Nessuna nuova route apre User, membership,
Audit o API legacy. [Modello](../architecture/ITS_ACADEMY_COURSE_MODEL.md),
[release gate](../architecture/MULTITENANCY_RELEASE_GATE.md).
