# Strategia di implementazione: creazione, svolgimento e valutazione degli esami

Documento di analisi e proposta. **Non modifica nulla**: descrive cosa fare, livello per livello, seguendo le convenzioni già presenti nel backend.

- Stato del backend analizzato: branch `branch-dema`, commit `0555c33`.
- Destinatari: chi sviluppa il backend (.NET) e chi adatta il frontend React.

---

## 1. Obiettivo

Permettere di **creare, modificare, eliminare, eseguire e valutare** gli esami.

Regole di business da rispettare:

1. **Solo domande chiuse** → la valutazione è **immediata**: lo studente vede subito punteggio e voto.
2. **Almeno una domanda aperta** → lo studente vede l'esame **"in correzione"**; l'insegnante valuta le risposte aperte.
3. **Esame misto non superabile** → se, alla consegna, `punti delle chiuse corrette + punti massimi delle aperte < soglia di sufficienza`, l'esame è **automaticamente "non superato"**. Lo studente **non vede il voto** ma sa che non l'ha superato. Il docente definisce comunque la valutazione finale.

Esempio (soglia 60% su 30 punti = 18): Luca consegna, le chiuse corrette valgono 13 punti, le aperte valgono al massimo 4 → 13 + 4 = 17 < 18 → **non superato automaticamente**, senza aspettare il docente.

---

## 2. Stato attuale del backend

### 2.1 Architettura a livelli

Soluzione `ElectronicRegisterAPI.slnx`, .NET 10, EF Core 9 con **Pomelo MySQL**. Cinque progetti:

| Livello | Progetto | Contenuto oggi |
|---|---|---|
| Domain | `ElectronicRegisterAPI.Domain` | `Models/`, `DTOs/`, `Enums/`, `Exceptions/`, `Constants/`, `Interfaces/{Repositories,Services,Managers,Security}` |
| Infrastructure | `…Infrastructure` | `Persistence/` (DbContext + `Entities/` scaffoldate), `Repositories/`, `Security/`, `Options/` |
| Business | `…Business` | `Services/` (regole di dominio, validazioni) |
| Application | `…Application` | `Managers/` (orchestrazione, autorizzazione per ruolo, mapping a DTO) |
| Api | `…Api` | `Controllers/`, `ExceptionHandling/`, `Program.cs` |

Flusso di una richiesta: `Controller → Manager → (Service + Repository) → DbContext`.

- I **Manager** ricevono un `ClaimsContext(UserId, Role, StudentId, TeacherId)` e applicano le restrizioni per ruolo (esempio in `GradeManager`).
- I **Service** contengono validazioni (`EnsureXxx…`) e sollevano eccezioni di dominio.
- I **Repository** lavorano con i **modelli di dominio** (POCO), non con le entità EF: il mapping entità ↔ modello è manuale (`MapToModel`).
- Registrazione: 3 punti di DI (`Infrastructure`, `Business`, `Application`), tutti `AddScoped`.

### 2.2 Convenzioni da rispettare

- **Rotte**: `[Route("api/[controller]")]`, `[Authorize]` a livello di classe, `[Authorize(Roles = "teacher,admin")]` sui singoli metodi. Stile dei metodi: `GET`, `GET {id}`, `POST`, `PUT update/{id}`, `DELETE {id}`, filtri `GET byname/{name}`. Il frontend React chiama già `/Exam` e `/Test` con questo stile.
- **Errori** (`GlobalExceptionHandler`): `KeyNotFoundException`→404, `ArgumentException`→400, `UnauthorizedAccessException`→403, `BusinessRuleException`→409, resto→500. I messaggi sono in italiano.
- **Id**: `Guid` generato nel Manager (`Guid.NewGuid()`).
- **N+1**: gli ultimi commit vanno nella direzione "i service/repository restituiscono oggetti già completi per evitare N+1". `GradeManager.MapToDtosAsync` carica in blocco con `GetByIdsAsync`. Le nuove query devono seguire lo stesso principio (una query con `Include`, non un ciclo di query).
- **Nessuna transazione esplicita** né unit of work: ogni repository fa `SaveChangesAsync()` per operazione. Vedi §5.5, per la consegna serve una scrittura atomica.

### 2.3 Database desunto

Il database è **MySQL**, `utf8mb4_unicode_ci`, colonne `snake_case`, chiavi `Guid` (con Pomelo `char(36)`: verificare con `SHOW CREATE TABLE`). Il `DbContext` è **scaffoldato** (approccio database-first): non esiste la cartella `Migrations`, e c'è il metodo `partial OnModelCreatingPartial` per le personalizzazioni.

```
biennium ─< biennium_study_area >─ study_area
                  │
                  └─< biennium_study_path >─ study_path
                              │
                              └─< class ─┬─< student ──── user (1:1, opzionale)
                                         │
                                         └─< class_subject >─ subject
                                                  │  └────── teacher ── user (1:1, opzionale)
                                                  │
                                                  └─< grade >── student
```

| Tabella | Colonne principali | Note |
|---|---|---|
| `biennium` | `id`, `start_year`, `end_year` | unique (start, end) |
| `study_area`, `study_path` | `id`, `name`, `description` | `name` unique |
| `biennium_study_area` | `id`, `biennium_id`, `study_area_id` | |
| `biennium_study_path` | `id`, `biennium_study_area_id`, `study_path_id` | |
| `class` | `id`, `biennium_study_path_id`, `name(20)` | unique (path, name) |
| `student` | `id`, `first_name`, `last_name`, `class_id` (null) | |
| `teacher` | `id`, `first_name`, `last_name` | |
| `subject` | `id`, `name(150)` | `name` unique |
| `class_subject` | `id`, `class_id`, `subject_id`, `teacher_id` | unique (class, subject): **una materia in una classe ha un solo docente** |
| `grade` | `id`, `student_id`, `class_subject_id`, `value decimal(4,2)`, `date` | il `GradeService` valida `1 ≤ value ≤ 10` |
| `user` | `id`, `email`, `password_hash`, `role enum(admin,teacher,student)`, `student_id`, `teacher_id` | |

**Conseguenza chiave per gli esami**: `class_subject` identifica già *classe + materia + docente*. Un esame agganciato a `class_subject` sa da solo quali studenti lo devono svolgere (gli studenti della classe) e chi lo può valutare (il docente della materia), esattamente come fa `grade`.

### 2.4 Cosa manca oggi

Nel codice del backend non esiste nulla per esami, test, domande o risposte (nessuna entità, servizio o controller). Sul server online `/Exam` e `/Test` rispondono 404. Anche i controller per `Biennium`, `StudyArea`, `StudyPath` e le classi non sono ancora esposti; non sono necessari a questo lavoro, ma vanno tenuti presenti.

---

## 3. Modello concettuale proposto

Il frontend ragiona già su due livelli, e conviene mantenerli:

- **Exam** (esame/appello): l'evento in una data per una classe e una materia. Campi: nome, data, se è di recupero.
- **Test** (prova): il quiz con le domande, associato a un esame. Un esame può avere più prove.
- **Submission** (consegna): il tentativo di uno studente su una prova, con risposte, punteggi ed esito.
- **Grade** (voto): la tabella esistente. Il voto ufficiale nel registro resta un `grade`; la consegna vi fa riferimento.

> **Decisione aperta 1.** Se "esame" e "prova" devono essere un'unica cosa, si elimina la tabella `exam` e i suoi campi passano su `test`. Il resto della strategia non cambia.

### 3.1 Tipi di domanda

| Tipo | Chiusa/aperta | Correzione |
|---|---|---|
| `single` (una sola risposta giusta) | chiusa | automatica |
| `multiple` (una o più risposte giuste) | chiusa | automatica |
| `open` (risposta scritta) | aperta | manuale dal docente |

Punteggio delle `multiple`: proposta **tutto o niente** (punti pieni solo se l'insieme selezionato coincide con quello corretto). È una regola isolata nel service e cambiabile (variante: punti parziali proporzionali).

### 3.2 Stati della consegna

```
in_progress ──consegna──▶ graded          (solo chiuse: esito immediato, voto visibile)
                     ├──▶ pending_review  (ci sono aperte e la soglia è ancora raggiungibile: "in correzione")
                     └──▶ failed_auto     (soglia non più raggiungibile: "non superato", voto nascosto)

pending_review ──il docente corregge──▶ finalized
failed_auto    ──il docente definisce il voto──▶ finalized
```

Visibilità lato studente (deve essere applicata **dal server**, non solo dall'interfaccia):

| Stato | Lo studente vede |
|---|---|
| `in_progress` | il test da svolgere |
| `graded` | punteggio, voto, esito (superato o no) |
| `pending_review` | "In correzione" (nessun punteggio) |
| `failed_auto` | "Non superato" (nessun punteggio né voto) |
| `finalized` | punteggio, voto, esito |

### 3.3 Algoritmo di valutazione alla consegna

```
correctClosed = Σ punti delle domande chiuse risposte correttamente
openMax       = Σ punti di tutte le domande aperte
maxScore      = Σ punti di tutte le domande             (= test.total_points)
passingScore  = maxScore × test.passing_percent / 100   (default 60%)

se non ci sono domande aperte:
    stato = graded                       → esito immediato: total = correctClosed, passed = (total ≥ passingScore)
altrimenti se correctClosed + openMax < passingScore:
    stato = failed_auto                  → non superato, voto nascosto
altrimenti:
    stato = pending_review               → in correzione
```

Casi di prova (test unitari del service, soglia 18 su 30):

| Chiuse corrette | Max aperte | Risultato | Motivo |
|---|---|---|---|
| 13 | 4 | `failed_auto` | 13 + 4 = 17 < 18 (l'esempio di Luca) |
| 14 | 4 | `pending_review` | 14 + 4 = 18 ≥ 18 (la soglia è inclusa) |
| 15 | 4 | `pending_review` | ancora raggiungibile |
| 20 (nessuna aperta) | 0 | `graded`, superato | valutazione immediata |
| 12 (nessuna aperta) | 0 | `graded`, non superato | con solo chiuse il voto si mostra sempre |

Ottimizzazione facoltativa: se tutte le risposte aperte sono vuote, assegnare 0 a quelle domande e ricalcolare come se `openMax = 0`, evitando di mandare in correzione un esame già deciso.

---

## 4. Tabelle del database

Il progetto usa il database-first: si scrive uno **script SQL versionato** e poi si rigenera lo scaffold (§5.1). Convenzioni: `snake_case`, chiavi `CHAR(36)`.

```sql
-- Esame (appello) di una classe per una materia
CREATE TABLE exam (
  id               CHAR(36)     NOT NULL,
  class_subject_id CHAR(36)     NOT NULL,
  name             VARCHAR(150) NOT NULL,
  exam_date        DATE         NOT NULL,
  is_retake        TINYINT(1)   NOT NULL DEFAULT 0,
  created_at       DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (id),
  KEY idx_exam_class_subject (class_subject_id),
  KEY idx_exam_date (exam_date),
  CONSTRAINT fk_exam_class_subject FOREIGN KEY (class_subject_id) REFERENCES class_subject (id)
);

-- Prova (quiz) di un esame
CREATE TABLE test (
  id               CHAR(36)     NOT NULL,
  exam_id          CHAR(36)     NOT NULL,
  title            VARCHAR(150) NOT NULL,
  description      TEXT         NULL,
  duration_minutes INT          NOT NULL,
  passing_percent  DECIMAL(5,2) NOT NULL DEFAULT 60.00,
  total_points     DECIMAL(6,2) NOT NULL DEFAULT 0,          -- somma dei punti delle domande, ricalcolata a ogni salvataggio
  status           ENUM('draft','published','closed') NOT NULL DEFAULT 'draft',
  published_at     DATETIME     NULL,
  created_at       DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at       DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (id),
  KEY idx_test_exam (exam_id),
  CONSTRAINT fk_test_exam FOREIGN KEY (exam_id) REFERENCES exam (id)
);

CREATE TABLE question (
  id       CHAR(36)     NOT NULL,
  test_id  CHAR(36)     NOT NULL,
  position INT          NOT NULL,
  text     TEXT         NOT NULL,
  type     ENUM('single','multiple','open') NOT NULL,
  points   DECIMAL(5,2) NOT NULL,
  required TINYINT(1)   NOT NULL DEFAULT 0,
  PRIMARY KEY (id),
  UNIQUE KEY uq_question_position (test_id, position),
  CONSTRAINT fk_question_test FOREIGN KEY (test_id) REFERENCES test (id) ON DELETE CASCADE
);

CREATE TABLE question_option (
  id          CHAR(36)     NOT NULL,
  question_id CHAR(36)     NOT NULL,
  position    INT          NOT NULL,
  text        VARCHAR(500) NOT NULL,
  is_correct  TINYINT(1)   NOT NULL DEFAULT 0,      -- MAI esposto agli studenti
  PRIMARY KEY (id),
  UNIQUE KEY uq_option_position (question_id, position),
  CONSTRAINT fk_option_question FOREIGN KEY (question_id) REFERENCES question (id) ON DELETE CASCADE
);

-- Consegna: un tentativo di uno studente su una prova
CREATE TABLE test_submission (
  id                     CHAR(36)     NOT NULL,
  test_id                CHAR(36)     NOT NULL,
  student_id             CHAR(36)     NOT NULL,
  status                 ENUM('in_progress','graded','pending_review','failed_auto','finalized') NOT NULL DEFAULT 'in_progress',
  started_at             DATETIME     NOT NULL,
  deadline_at            DATETIME     NOT NULL,        -- started_at + durata, fissata all'avvio
  submitted_at           DATETIME     NULL,
  submit_reason          ENUM('manual','time_expired','violations') NULL,
  violation_count        INT          NOT NULL DEFAULT 0,
  max_score              DECIMAL(6,2) NULL,            -- snapshot alla consegna
  passing_score          DECIMAL(6,2) NULL,
  closed_score           DECIMAL(6,2) NULL,
  open_max_points        DECIMAL(6,2) NULL,
  open_score             DECIMAL(6,2) NULL,            -- assegnato dal docente
  total_score            DECIMAL(6,2) NULL,
  passed                 TINYINT(1)   NULL,
  grade_id               CHAR(36)     NULL,            -- voto ufficiale nel registro
  reviewed_by_teacher_id CHAR(36)     NULL,
  finalized_at           DATETIME     NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_submission_test_student (test_id, student_id),   -- un solo tentativo
  KEY idx_submission_student (student_id),
  KEY idx_submission_status (test_id, status),
  CONSTRAINT fk_submission_test    FOREIGN KEY (test_id)    REFERENCES test (id),
  CONSTRAINT fk_submission_student FOREIGN KEY (student_id) REFERENCES student (id),
  CONSTRAINT fk_submission_grade   FOREIGN KEY (grade_id)   REFERENCES grade (id) ON DELETE SET NULL,
  CONSTRAINT fk_submission_teacher FOREIGN KEY (reviewed_by_teacher_id) REFERENCES teacher (id) ON DELETE SET NULL
);

CREATE TABLE submission_answer (
  id             CHAR(36)     NOT NULL,
  submission_id  CHAR(36)     NOT NULL,
  question_id    CHAR(36)     NOT NULL,
  open_text      TEXT         NULL,
  is_correct     TINYINT(1)   NULL,       -- solo chiuse, calcolato alla consegna
  awarded_points DECIMAL(5,2) NULL,       -- chiuse: automatico, aperte: docente
  teacher_comment TEXT        NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_answer (submission_id, question_id),
  CONSTRAINT fk_answer_submission FOREIGN KEY (submission_id) REFERENCES test_submission (id) ON DELETE CASCADE,
  CONSTRAINT fk_answer_question   FOREIGN KEY (question_id)   REFERENCES question (id)
);

CREATE TABLE submission_answer_option (
  submission_answer_id CHAR(36) NOT NULL,
  option_id            CHAR(36) NOT NULL,
  PRIMARY KEY (submission_answer_id, option_id),
  CONSTRAINT fk_sao_answer FOREIGN KEY (submission_answer_id) REFERENCES submission_answer (id) ON DELETE CASCADE,
  CONSTRAINT fk_sao_option FOREIGN KEY (option_id)            REFERENCES question_option (id)
);

-- Facoltativa: pallino "nuovo esame" per lo studente
CREATE TABLE student_test_seen (
  test_id    CHAR(36) NOT NULL,
  student_id CHAR(36) NOT NULL,
  seen_at    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (test_id, student_id),
  CONSTRAINT fk_seen_test    FOREIGN KEY (test_id)    REFERENCES test (id) ON DELETE CASCADE,
  CONSTRAINT fk_seen_student FOREIGN KEY (student_id) REFERENCES student (id) ON DELETE CASCADE
);
```

**Note di progetto**
- `grade` **non cambia**: il voto in registro resta la fonte unica; la consegna lo referenzia con `grade_id`.
- `test_submission` conserva uno snapshot di `max_score` e `passing_score`, così un eventuale intervento sul test non altera gli esiti già calcolati. In ogni caso un test pubblicato non si può più modificare nella struttura (§5.6).
- `numberOfStudents`, oggi inviato dal frontend, si **elimina**: è il numero di studenti della classe, derivabile.
- Le date/ore delle consegne sono in **UTC** (`DATETIME`), a differenza di `grade.date` che è solo data.

---

## 5. Implementazione per livello

Ordine consigliato: database → scaffolding → Domain → Infrastructure → Business → Application → Api → frontend. Ogni livello dipende solo da quelli precedenti.

### 5.1 Scaffolding (Infrastructure)

1. Salvare lo script SQL del §4 in una cartella versionata (oggi non ne esiste una, per esempio `Backend/ElectronicRegisterAPI/database/`) e applicarlo al database.
2. Rigenerare le entità con `dotnet ef dbcontext scaffold` (`Pomelo.EntityFrameworkCore.MySql`), con gli stessi parametri usati per lo scaffold esistente (`--context ElectronicRegisterContext`, `--output-dir Persistence/Entities`, `--namespace …Persistence.Entities`). Nuove entità: `Exam`, `Test`, `Question`, `QuestionOption`, `TestSubmission`, `SubmissionAnswer`, `SubmissionAnswerOption`, `StudentTestSeen`.
3. Attenzione a `--force`: sovrascrive `ElectronicRegisterContext.cs` e le entità. Ogni personalizzazione va tenuta nella parte `partial` (`OnModelCreatingPartial`), mai nel file generato. Se lo scaffold si rifà in blocco, verificare con `git diff` che `Biennium`, `Grade` ecc. non cambino.
4. Verificare che gli `ENUM` MySQL vengano mappati come stringhe (come `user.role`, che ha già `HasColumnType("enum(…)")`) e convertirli in enum C# con `HasConversion<string>()` nella parte partial.

### 5.2 Domain

**Enums** (`Domain/Enums`): `QuestionType { Single, Multiple, Open }`, `TestStatus { Draft, Published, Closed }`, `SubmissionStatus { InProgress, Graded, PendingReview, FailedAuto, Finalized }`, `SubmitReason { Manual, TimeExpired, Violations }`.

**Models** (`Domain/Models`, POCO con proprietà, come `Grade`, non con campi come `Biennium`): `Exam`, `Test` (con `List<Question>`), `Question` (con `List<QuestionOption>`), `QuestionOption`, `TestSubmission` (con `List<SubmissionAnswer>`), `SubmissionAnswer` (con `List<Guid> SelectedOptionIds`).

**DTO** (`Domain/DTOs`): si distinguono due famiglie per non far mai uscire le risposte giuste.

| Uso | DTO |
|---|---|
| Docente/admin: lettura completa | `ExamDto`, `TestDto`, `QuestionDto` (con `IsCorrect` sulle opzioni) |
| Studente: svolgimento | `TestForStudentDto`, `QuestionForStudentDto` (**senza** `IsCorrect`) |
| Scrittura | `CreateExamDto`, `UpdateExamDto`, `CreateTestDto` (con domande annidate), `UpdateTestDto` |
| Consegna | `SubmitTestDto` (risposte), `SubmissionResultDto` (vista studente **mascherata per stato**), `SubmissionReviewDto` (vista docente) |
| Correzione | `ReviewOpenAnswersDto` (punti e commento per risposta), `FinalizeSubmissionDto` (voto 1–10) |

**Interfacce**
- Repository: `IExamRepository`, `ITestRepository`, `ITestSubmissionRepository`.
- Service: `IExamService`, `ITestService`, `ISubmissionGradingService`.
- Manager: `IExamManager`, `ITestManager`, `ISubmissionManager`.

### 5.3 Infrastructure: repository

Stesso schema dei repository esistenti (modelli di dominio, `AsNoTracking` in lettura, mapping manuale). Le liste caricano l'aggregato in **una query** con `Include`.

```csharp
public interface IExamRepository {
    Task<Exam?> GetByIdAsync(Guid id);
    Task<List<Exam>> GetAllAsync(Guid? teacherId, Guid? studentClassId);   // restrizioni per ruolo, come GradeRepository
    Task<List<Exam>> GetByNameAsync(string name, Guid? teacherId, Guid? studentClassId);
    Task AddAsync(Exam exam);
    Task UpdateAsync(Exam exam);
    Task DeleteAsync(Exam exam);
    Task<bool> HasSubmissionsAsync(Guid examId);
}

public interface ITestRepository {
    Task<Test?> GetWithQuestionsAsync(Guid id);                     // Include(Questions).ThenInclude(Options): una query
    Task<List<Test>> GetByExamIdsAsync(IEnumerable<Guid> examIds);  // senza domande, per gli elenchi
    Task<List<Test>> GetPublishedForClassAsync(Guid classId);       // test assegnati a uno studente
    Task AddAsync(Test test);                                       // salva test + domande + opzioni in un solo SaveChanges
    Task ReplaceStructureAsync(Test test);                          // aggiorna test e sostituisce domande/opzioni (solo bozza)
    Task DeleteAsync(Guid id);
    Task<bool> HasSubmissionsAsync(Guid testId);
}

public interface ITestSubmissionRepository {
    Task<TestSubmission?> GetByIdAsync(Guid id, bool withAnswers = false);
    Task<TestSubmission?> GetByTestAndStudentAsync(Guid testId, Guid studentId);
    Task<List<TestSubmission>> GetByStudentAsync(Guid studentId);
    Task<List<TestSubmission>> GetByTestAsync(Guid testId, SubmissionStatus? status = null);
    Task<List<TestSubmission>> GetExpiredInProgressAsync(DateTime utcNow);  // chiusura automatica allo scadere
    Task AddAsync(TestSubmission submission);                                // all'avvio
    Task SaveSubmittedAsync(TestSubmission submission);                      // consegna: stato + risposte + punteggi, atomico
    Task UpdateAsync(TestSubmission submission);                             // correzione e voto finale
}
```

Estensioni a repository esistenti: `IStudentRepository.GetByClassIdAsync(Guid classId)` (studenti da valutare e destinatari), `IClassSubjectRepository.GetByClassIdsAsync(...)` se serve per gli elenchi.

### 5.4 Business: service

Contengono le **regole pure** (facilmente testabili in unità) e le validazioni con eccezioni, come `GradeService`.

`ITestService` (validazione della struttura):
- almeno una domanda; punti `> 0`; `duration_minutes > 0`; `0 < passing_percent ≤ 100`;
- `single`: 2 o più opzioni con testo, **esattamente una** corretta;
- `multiple`: 2 o più opzioni con testo, **almeno una** corretta;
- `open`: nessuna opzione;
- `EnsureEditable(test)`: struttura modificabile solo in bozza e senza consegne; `EnsureDeletable(test)`;
- `EnsureTeacherOwnsExam(teacherId, exam)`: come `EnsureTeacherOwnsGradeAsync`.

`ISubmissionGradingService` (cuore delle regole di business, **senza accesso al database**):

```csharp
public interface ISubmissionGradingService {
    // Corregge le chiuse: per ogni risposta calcola is_correct e punti (tutto o niente per le multiple)
    GradingResult AutoGradeClosed(Test test, IReadOnlyList<SubmissionAnswer> answers);

    // Applica l'algoritmo del §3.3: Graded, PendingReview o FailedAuto
    SubmissionStatus Evaluate(Test test, decimal closedScore);

    // Voto 1–10 da punteggio, con la soglia mappata su 6
    decimal ConvertToGrade(decimal totalScore, decimal maxScore, decimal passingPercent);

    void EnsureCanStart(Test test, Student student, TestSubmission? existing);   // test pubblicato, studente della classe, nessun tentativo
    void EnsureWithinDeadline(TestSubmission submission, DateTime utcNow, TimeSpan grace);
}
```

Conversione punteggio → voto: proposta **lineare a tratti** con la soglia sul 6 (`< soglia`: da 1 a 5,99; `≥ soglia`: da 6 a 10). È una politica didattica da concordare: isolata in `ConvertToGrade`, si sostituisce senza toccare il resto. Il risultato deve rientrare nel range 1–10 già imposto da `GradeService.EnsureValidGradeValue`.

### 5.5 Application: manager

I manager orchestrano, applicano i permessi per ruolo con `ClaimsContext` e mappano ai DTO (con la **maschera per ruolo/stato**).

**`IExamManager`**: `GetAllAsync`, `GetByNameAsync`, `GetByIdAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync`. Il docente lavora solo sui propri `class_subject`; l'admin su tutti; lo studente legge solo gli esami della propria classe.

**`ITestManager`**: `GetByExamAsync`, `GetByIdAsync` (docente: completo; studente: `TestForStudentDto`), `AddAsync` (test + domande in un solo salvataggio), `UpdateAsync`, `PublishAsync`, `CloseAsync`, `DeleteAsync`, `GetAssignedToStudentAsync` (con il flag "nuovo").

**`ISubmissionManager`**, il flusso di svolgimento e valutazione:

| Metodo | Chi | Cosa fa |
|---|---|---|
| `StartAsync(testId, caller)` | studente | verifica classe/pubblicazione/tentativo unico; crea la consegna `in_progress` con `started_at` e `deadline_at`; restituisce `TestForStudentDto` |
| `SaveAnswersAsync(...)` (facoltativo) | studente | salva le risposte in corso, per poter consegnare in automatico se il client sparisce |
| `SubmitAsync(testId, dto, caller)` | studente | vedi sotto |
| `GetMineAsync(...)`, `GetResultAsync(testId, caller)` | studente | esito **mascherato per stato** (§3.2) |
| `GetSubmissionsAsync(testId, status?, caller)` | docente/admin | elenco consegne, per esempio "da correggere" |
| `ReviewOpenAnswersAsync(id, dto, caller)` | docente | assegna punti/commenti alle aperte; `punti ≤ punti della domanda` |
| `FinalizeAsync(id, dto, caller)` | docente | definisce il voto e crea/aggiorna il `grade`; stato `finalized` |

`SubmitAsync`, in ordine:
1. Carica consegna e test completo (`GetWithQuestionsAsync`), controlla che sia `in_progress` e che le risposte riguardino domande del test.
2. Controlla la scadenza: entro `deadline_at` + tolleranza (per esempio 30 s) la consegna è valida; oltre, si consegna con `submit_reason = time_expired` e le risposte già salvate.
3. `AutoGradeClosed` → `closed_score`, `open_max_points`, `max_score`, `passing_score` (snapshot).
4. `Evaluate` → `graded`, `pending_review` o `failed_auto`.
5. Se `graded`: `total_score`, `passed`, voto immediato (`ConvertToGrade`). **Decisione aperta 2**: creare subito la riga in `grade` per le sole chiuse, oppure lasciare che lo pubblichi il docente. Proposta: crearla subito, è quello che significa "valutazione immediata".
6. `SaveSubmittedAsync`: **un solo `SaveChanges` per tutta la consegna** (stato, punteggi, risposte, opzioni scelte), quindi atomico. Il vincolo `UNIQUE (test_id, student_id)` e un aggiornamento condizionato a `status = 'in_progress'` evitano consegne doppie da richieste concorrenti.

`FinalizeAsync`: consentito da `pending_review` e `failed_auto`. Per `failed_auto` il docente sceglie il voto a mano (di norma insufficiente, ma la scelta resta sua, come dice la regola di business). Dopo la finalizzazione lo studente vede il voto.

**Chiusura automatica allo scadere**: un `BackgroundService` (o la lettura "pigra" di una consegna scaduta) porta a consegnata le `in_progress` con `deadline_at` passata, usando le risposte salvate (`GetExpiredInProgressAsync`). Senza questo, uno studente che chiude il browser lascerebbe la consegna aperta.

**Transazioni**: oggi non ce ne sono. Le scritture qui sopra sono atomiche perché passano da un solo `SaveChanges`. Se in `FinalizeAsync` si scrivono `grade` e `test_submission` in due repository distinti, serve una transazione (`IDbContextTransaction`) o un metodo di repository che li scriva insieme.

### 5.6 Regole di modifica ed eliminazione

| Azione | Consentita se |
|---|---|
| Modificare nome/data/recupero dell'esame | sempre (docente proprietario o admin) |
| Modificare **struttura** del test (domande, opzioni, punti, soglia) | test in `draft` |
| Modificare titolo/descrizione/durata | test non chiuso e senza consegne |
| Pubblicare | almeno una domanda, struttura valida |
| Eliminare il test | `draft`, oppure pubblicato **senza consegne** |
| Eliminare l'esame | nessun test con consegne (`BusinessRuleException` → 409) |

Le consegne non si eliminano dall'API; l'eliminazione a cascata passa solo dall'eliminazione della bozza.

### 5.7 Api: controller

`[Route("api/[controller]")]`, `[Authorize]`, ruoli sui metodi, come `GradeController`. Da registrare nei tre `ServiceCollectionExtensions` (repository, service, manager).

**`ExamController`** (`api/Exam`)

| Metodo e rotta | Ruoli | Note |
|---|---|---|
| `GET /` , `GET /{id}` , `GET /byname/{name}` | teacher, admin, student | filtrati per ruolo nel manager |
| `POST /` | teacher, admin | 201 |
| `PUT /update/{id}` | teacher, admin | 204 |
| `DELETE /{id}` | teacher, admin | 204, 409 se ci sono consegne |

**`TestController`** (`api/Test`)

| Metodo e rotta | Ruoli | Note |
|---|---|---|
| `GET /byexam/{examId}` , `GET /{id}` | teacher, admin, student | lo studente riceve `TestForStudentDto` senza `IsCorrect` |
| `GET /assigned` | student | test da svolgere e stato di ciascuno |
| `POST /` | teacher, admin | crea test con domande annidate |
| `PUT /update/{id}` | teacher, admin | struttura solo in bozza |
| `PUT /{id}/publish`, `PUT /{id}/close` | teacher, admin | |
| `DELETE /{id}` | teacher, admin | |
| `POST /{id}/start` | student | avvia il tentativo |
| `PUT /{id}/answers` (facoltativo) | student | salvataggio progressivo |
| `POST /{id}/submit` | student | consegna e valutazione |
| `GET /{id}/result` | student | esito mascherato |
| `GET /{id}/submissions?status=` | teacher, admin | elenco consegne |

**`TestSubmissionController`** (`api/TestSubmission`)

| Metodo e rotta | Ruoli | Note |
|---|---|---|
| `GET /{id}` | teacher, admin | consegna con risposte e punteggi |
| `PUT /{id}/review` | teacher, admin | punti e commenti sulle aperte |
| `PUT /{id}/finalize` | teacher, admin | voto finale e creazione del `grade` |

Ogni metodo deve applicare l'autorizzazione **anche nel manager** (il docente solo sulle proprie classi, lo studente solo sulle proprie consegne), come già fa `GradeManager`. Il solo `[Authorize(Roles = …)]` non basta.

---

## 6. Sicurezza e integrità

- **Risposte giuste mai verso lo studente**: `TestForStudentDto` non ha `IsCorrect`. Verificare con un test che il JSON di uno studente non contenga risposte corrette, punti raggiunti né voto mentre lo stato è `pending_review` o `failed_auto`.
- **Il tempo lo decide il server** (`deadline_at`), non il client. Il timer del frontend è solo informativo.
- **Un tentativo per studente** garantito dal vincolo unico e da un aggiornamento condizionale.
- **Anti-copiatura**: il frontend registra già le uscite dal test. Lato server conservare `violation_count` e `submit_reason` (i dati inviati dal client sono indicativi, non a prova di manomissione: un controllo lato client non è una garanzia).
- **Errori**: usare `BusinessRuleException` (409) per le violazioni di regole, `UnauthorizedAccessException` (403) per i permessi, `KeyNotFoundException` (404) per risorse mancanti, come oggi.

---

## 7. Piano di lavoro

| Fase | Contenuto | Verifica |
|---|---|---|
| 1 | Script SQL (§4), applicazione al DB, rigenerazione scaffold (§5.1) | il progetto compila; nessuna entità esistente cambia |
| 2 | Enums, Models, DTO, interfacce (Domain) | compila |
| 3 | Repository e registrazione DI (Infrastructure) | test di integrazione su DB di prova |
| 4 | `ISubmissionGradingService` e `ITestService` (Business) | **test unitari** dei casi del §3.3, compreso l'esempio di Luca |
| 5 | Manager (Application) | test dei permessi per ruolo e della maschera degli esiti |
| 6 | Controller (Api) | prova con Scalar/OpenAPI (già attivo in sviluppo) |
| 7 | Adeguamento del frontend (§8) | flusso completo docente → studente → correzione |

Suddivisione consigliata: prima le fasi 1–6 per `Exam` e `Test` (CRUD), poi lo svolgimento (`start`/`submit`), poi la correzione.

---

## 8. Impatto sul frontend React

Il frontend è già impostato su questo flusso (editor dei test, svolgimento a schermo bloccato, esito), ma con dati di prova. Per collegarlo al backend:

1. **Domande**: oggi le risposte corrette sono un array `correct` di id generati dal client. Conviene inviare `isCorrect` su ogni opzione, con gli id assegnati dal server.
2. **Esame**: il modulo "+ Esame" oggi invia `subjectId`. Serve `classSubjectId` (classe + materia), quindi il modulo deve permettere di scegliere la classe. `numberOfStudents` si elimina.
3. **Test da svolgere** dello studente: sostituire `src/data/studentExams.js` con `GET /Test/assigned`, `POST /Test/{id}/start`, `POST /Test/{id}/submit`, `GET /Test/{id}/result`.
4. **Esiti**: mostrare "In correzione" per `pending_review` e "Non superato" per `failed_auto`, senza punteggio né voto, coerente con la maschera del server.
5. **Correzione del docente**: manca la schermata (elenco consegne da correggere, punti sulle aperte, voto finale). Va costruita sui tre endpoint di `TestSubmission`.
6. **Dati di prova**: eliminare `examsMock.js`, `classesMock.js`, `studentExams.js` e `DEMO_*` quando gli endpoint rispondono.

---

## 9. Decisioni aperte

| # | Domanda | Proposta |
|---|---|---|
| 1 | Esame e prova sono due livelli (`exam` + `test`) o uno solo? | Due livelli, come nel frontend |
| 2 | Per le sole domande chiuse si crea subito il `grade` in registro? | Sì, è la valutazione immediata |
| 3 | Soglia di sufficienza: 60% per tutti o configurabile per test? | Configurabile per test (`passing_percent`), default 60% |
| 4 | Domande a scelta multipla: tutto o niente, oppure punti parziali? | Tutto o niente |
| 5 | Conversione punteggio → voto 1–10 | Lineare a tratti con la soglia sul 6 |
| 6 | Un solo tentativo o più tentativi per prova? | Un solo tentativo; il recupero è un altro esame (`is_retake`) |
| 7 | Dopo la finalizzazione lo studente vede le risposte corrette? | Da decidere (di default no) |
| 8 | Se il docente rifiuta di correggere, c'è una scadenza? | Fuori ambito |

---

## 10. Riferimenti nel codice esistente

Modelli da imitare per ogni livello:

- Controller: `Api/Controllers/GradeController.cs`
- Manager con permessi per ruolo e mapping in blocco: `Application/Managers/GradeManager.cs`
- Service con validazioni: `Business/Services/GradeService.cs`
- Repository con mapping manuale: `Infrastructure/Repositories/GradeRepository.cs`, `ClassSubjectRepository.cs`
- Configurazione EF e nomi colonna: `Infrastructure/Persistence/ElectronicRegisterContext.cs`
- Mappatura errori: `Api/ExceptionHandling/GlobalExceptionHandler.cs`
