# Modelli dati e tipi

Fonti: `src/Domain/ElectronicRegisterAPI.Domain/DTOs/**`, `.../Models/**`, `.../Enums/**`, `.../Results/**`, `src/Api/ElectronicRegisterAPI/Contracts/**`.

Convenzioni generali (dettagli in [CONVENTIONS.md §1](CONVENTIONS.md#1-formato-json)):

- Nomi in **camelCase**.
- `Guid` → `string` UUID.
- `DateOnly` → `"YYYY-MM-DD"`.
- `TimeOnly` → `"HH:mm:ss"`.
- `DateTime` → ISO 8601 (UTC).
- `decimal`, `int`, `long` → `number`.
- I campi `null` **non vengono inviati** nelle risposte: nelle interfacce sotto i campi nullable sono marcati `?` (opzionali).
- Le proprietà calcolate lato server (es. `isRetake`, `durationMinutes`) **sono incluse** nelle risposte.

---

## 1. Mappa delle entità

```mermaid
erDiagram
    USER ||--o{ MEMBERSHIP : "ha"
    INSTITUTION ||--o{ MEMBERSHIP : "ha"
    MEMBERSHIP }o--|| STUDENT : "profilo (opz.)"
    MEMBERSHIP }o--|| TEACHER : "profilo (opz.)"
    STUDY_AREA ||--o{ BIENNIUM_STUDY_AREA : ""
    BIENNIUM ||--o{ BIENNIUM_STUDY_AREA : ""
    BIENNIUM_STUDY_AREA ||--o{ BIENNIUM_STUDY_PATH : ""
    STUDY_PATH ||--o{ BIENNIUM_STUDY_PATH : ""
    BIENNIUM_STUDY_PATH ||--o{ CLASS : ""
    STUDY_PATH ||--o{ COURSE_EDITION : ""
    COURSE_EDITION ||--o{ CLASS : "assegnata a"
    COURSE_EDITION }o--o{ CAMPUS : "sedi"
    COURSE_EDITION ||--o{ ENROLLMENT : ""
    STUDENT ||--o{ ENROLLMENT : ""
    CLASS ||--o{ STUDENT : "classe corrente"
    CLASS ||--o{ CLASS_SUBJECT : ""
    SUBJECT ||--o{ CLASS_SUBJECT : ""
    TEACHER ||--o{ CLASS_SUBJECT : "insegna"
    CLASS_SUBJECT ||--o{ EXAM : ""
    EXAM ||--o{ QUESTION : ""
    QUESTION ||--o{ OPTION : ""
    EXAM ||--o{ EXAM_STUDENT : "assegnazioni"
    EXAM ||--o{ SUBMISSION : ""
    SUBMISSION ||--o{ ANSWER : ""
    SUBMISSION ||--o{ VIOLATION : ""
    CLASS_SUBJECT ||--o{ GRADE : ""
    STUDENT ||--o{ GRADE : ""
    STUDY_PATH ||--o{ FORMATIVE_PLAN : ""
    FORMATIVE_PLAN ||--o{ PLAN_VERSION : ""
    PLAN_VERSION ||--o{ UNIT : ""
    UNIT ||--o{ MODULE : ""
    COURSE_EDITION }o--|| PLAN_VERSION : "piano collegato"
    CLASS ||--o{ LESSON : ""
    MODULE ||--o{ LESSON : ""
```

| Entità | Descrizione | Vincoli principali |
|---|---|---|
| Identità (user) | account globale: email + password (o Microsoft) | email ≤ 255, unica |
| Istituto | scuola/ente | stati `Provisioning`, `Active`, `Suspended`, `Archived` |
| Membership | legame utente↔istituto con ruoli | stati `pending`, `active`, `suspended`, `left`; almeno 1 ruolo; almeno 1 admin attivo per istituto |
| Area di studio | macro area | nome 1–100, descrizione obbligatoria (≤ 65535 byte) |
| Percorso di studio | indirizzo | nome 1–100, descrizione opzionale |
| Biennio | intervallo date | `endYear ≥ startYear`, anno ≥ 1000 |
| Biennio↔Area, Biennio-Area↔Percorso | associazioni | nessun update, solo create/delete |
| Classe | gruppo di studenti | nome 1–20 caratteri; appartiene a un biennio-percorso; può avere un'edizione corso |
| Sede (campus) | sede fisica | nome 1–200, indirizzo ≤ 300, città/provincia ≤ 100 |
| Edizione corso | istanza di un percorso | codice ≤ 64, nome ≤ 200, stato `planned`, `active`, `completed`, `cancelled` |
| Iscrizione | studente ↔ edizione | stato `enrolled`, `completed`, `withdrawn`, `cancelled`; `completionDate ≥ enrollmentDate` |
| Studente / Docente | anagrafica | nome e cognome 1–100 |
| Materia | disciplina | nome 1–150 |
| Classe-materia | classe + materia + docente | |
| Esame | prova online | vedi [§3.8](#38-esami) |
| Consegna (submission) | svolgimento di un esame da parte di uno studente | |
| Voto | voto ufficiale | 1–10, sufficienza ≥ 6 |
| Piano formativo / versione / unità / modulo | programma del corso con monte ore | max 500 unità, 500 moduli/unità, 200 argomenti/modulo |
| Lezione | evento di calendario | intervallo nello stesso giorno, al minuto |
| Policy email / assegnazione email | regole di generazione indirizzi istituzionali | |
| Import | anteprima e risultato di un import anagrafico | |

---

## 2. Enum e costanti

### 2.1 Enum serializzati come **numero**

```ts
/** Domain/Enums/ExamStatus.cs */
export enum ExamStatus { Draft = 0, Published = 1, Closed = 2, Cancelled = 3 }

/** Domain/Enums/SubmissionStatus.cs */
export enum SubmissionStatus {
  InProgress = 0,     // in svolgimento
  Graded = 1,         // corretto automaticamente con voto ufficiale
  PendingReview = 2,  // ci sono risposte aperte da correggere
  FailedAuto = 3,     // previsto dall'enum ma non assegnato dal codice di consegna attuale (OPEN_QUESTIONS Q-14)
  Finalized = 4,      // voto finale registrato dal docente
}

/** Domain/Enums/QuestionType.cs */
export enum QuestionType { Single = 0, Multiple = 1, Open = 2 }

/** Domain/Enums/SubmitReason.cs */
export enum SubmitReason { Manual = 0, TimeExpired = 1, Violations = 2 }

/** Domain/Enums/CalendarConflictKind.cs */
export enum CalendarConflictKind { Duplicate = 0, ClassOverlap = 1, TeacherOverlap = 2 }
```

### 2.2 Enum serializzati come **stringa** (case-sensitive)

```ts
export type InstitutionRole = "InstitutionAdmin" | "Staff" | "Teacher" | "Student";
export type MembershipMutationAction = "AddRole" | "RemoveRole" | "Suspend" | "Reactivate";

export type EmailCategory = "Student" | "Teacher" | "Staff" | "InstitutionAdmin"; // categorie di generazione, non ruoli IAM
export type EmailNameCase = "Lower" | "Upper";
export type EmailAccentHandling = "RejectNonAscii" | "LatinFoldV1";
export type EmailPunctuationHandling = "Remove" | "WordBoundary";
export type EmailCollisionStrategy = "Reject" | "NumericSuffix";

export type ImportCategory = "Student" | "Teacher";
export type ImportEmailMode = "None" | "ReserveInstitutionalAddress";
export type ImportField = "Ignore" | "FirstName" | "LastName" | "Class" | "ExternalReference";
export type ImportRowStatus = "Valid" | "Warning" | "Invalid" | "AmbiguousReference";

export type CalendarImportField =
  | "Ignore" | "Date" | "StartsAt" | "EndsAt" | "Class" | "Module"
  | "Teacher" | "Room" | "Description" | "ExternalReference";
export type CalendarImportMode = "AppendDraft" | "ReplaceDraftScope";

export type FormativeVersionStatus = "Draft" | "Published";
```

### 2.3 Valori fissi di campi stringa

| Campo | Valori | Fonte |
|---|---|---|
| `InstitutionMemberDto.status`, filtro `status` dei membri | `"pending"`, `"active"`, `"suspended"`, `"left"` (minuscolo) | `MembershipStatus` + DB |
| `MembershipMutationResponse.status` | come sopra (minuscolo) | `Contracts/MembershipMutationResponse.cs` |
| `CourseEditionDto.status`, `CreateCourseEditionDto.status` | `"planned"`, `"active"`, `"completed"`, `"cancelled"` | `Policies/CourseStructureValidation.cs` |
| `StudentEnrollmentDto.status`, `targetStatus` | `"enrolled"`, `"completed"`, `"withdrawn"`, `"cancelled"` | idem |
| `CalendarLesson.publication`, filtro `publication` | `"Draft"`, `"Published"` | repository calendario |
| `CalendarLesson.status`, filtro `status` | `"Scheduled"`, `"Cancelled"`, `"Completed"` | idem |
| `EmailPolicyWrite.state`, `EmailPolicyVersion.state` | `"Draft"`, `"Published"`, `"Disabled"` | `InstitutionEmailRepository.cs` |
| `EmailAssignment.state` | `"reserved"`, `"retired"` (altri valori possibili non verificati) | idem |
| `ImportBatch.state` | `"Preview"`, `"Completed"` (altri non verificati) | `InstitutionImportManager.cs` |
| `InstitutionViolationDto.type` | `"tab_hidden"`, `"window_blur"`, `"fullscreen_exit"` | `InstitutionExamRepositoryFactory.cs` |
| `AcquireHistoricalProfile.kind`, `ProfileAcquisitionResult.kind` | `"Student"`, `"Teacher"` | |
| `ProfileAcquisitionResult.status`, filtro `status` acquisizioni | `"linked"`, `"requires-review"` | |
| `InvitationResult.status` | `"accepted"` (invito/revoca/riemissione registrati), `"consumed"` (invito accettato) | `InstitutionOnboardingRepository.cs` |

### 2.4 Costanti

| Costante | Valore | Fonte |
|---|---|---|
| Voto minimo / massimo / sufficienza | 1 / 10 / 6 | `Policies/GradeRules.cs` |
| Durata selection ticket / access token | 300 s / 900 s | `Security/InstitutionTokens.cs` |
| Durata minima esame | > 5 minuti | `Business/Services/ExamService.cs` |
| Max violazioni per consegna | 128 (e max 5 ogni 10 s) | `InstitutionExamRepositoryFactory.cs` |
| Max minuti erogati per lezione | 1440 | `Policies/InstitutionCalendarRules.cs` |
| Finestra trasferimento di classe | `AcademicTransfers:MaxMonthsFromCourseStart` mesi dall'inizio corso (default 3), fuso `Europe/Rome` | `appsettings.example.json` |

---

## 3. Interfacce TypeScript

```ts
export type Guid = string;      // UUID
export type DateOnly = string;  // "YYYY-MM-DD"
export type TimeOnly = string;  // "HH:mm:ss"
export type IsoDateTime = string; // ISO 8601, UTC
```

### 3.1 Errori

```ts
export interface ProblemDetails {
  type?: string;
  title: string;
  status: number;
  detail?: string;
  traceId?: string;
  code?: string;          // calendario, import
  details?: unknown;      // conflitti calendario (es. lista di CalendarConflict)
  hourSummary?: FormativeHourSummary; // pubblicazione piano formativo
  errors?: Record<string, string[]>;  // validazione automatica ASP.NET
}
```

### 3.2 Identità e autenticazione

```ts
// Request
export interface RegisterIdentityAccountDto { email: string; password: string }        // email ≤255; password 8–72, 1 speciale
export interface LoginDto { email: string; password: string }
export interface MicrosoftLoginDto { accessToken: string }
export interface SelectInstitutionDto { institutionId: Guid; membershipId: Guid; requestedRole?: InstitutionRole }
export interface UpdateIdentityAccountDto { displayName: string }                        // 1–160, no caratteri di controllo
export interface ChangeIdentityPasswordDto { currentPassword: string; newPassword: string } // current ≤1024; new 8–72

// Response
export interface IdentityTicketDto { ticket: string; expiresInSeconds: number }          // 300
export interface InstitutionAccessDto { token: string; expiresInSeconds: number }        // 900
export interface InstitutionChoiceDto { institutionId: Guid; membershipId: Guid; name: string; roles: InstitutionRole[] }
export interface TenantContextDto { userId: Guid; institutionId: Guid; membershipId: Guid; roles: InstitutionRole[] }
export interface IdentityAccountDto { userId: Guid; email: string; displayName?: string; hasLocalPassword: boolean }
```

### 3.3 Membri, audit, inviti

```ts
export interface InstitutionMemberDto {
  membershipId: Guid; userId: Guid; email: string;
  status: "pending" | "active" | "suspended" | "left";
  roles: InstitutionRole[];
  joinedAt: IsoDateTime; suspendedAt?: IsoDateTime;
}

export interface MembershipMutationRequest {
  eventId: Guid;
  expectedRevision: number;           // security generation corrente della membership (≥0)
  action: MembershipMutationAction;
  role?: InstitutionRole;             // obbligatorio per AddRole/RemoveRole, VIETATO per Suspend/Reactivate
  reason: string;                     // 1–500, no caratteri di controllo
}
export interface MembershipMutationResponse {
  eventId: Guid; revision: number;
  status: "pending" | "active" | "suspended" | "left";
  roles: InstitutionRole[];
}

export interface AcquireHistoricalProfile {
  eventId: Guid; expectedRevision: number; profileId: Guid;
  kind: "Student" | "Teacher"; reason: string;  // reason 1–1000
}
export interface ProfileAcquisitionResult {
  eventId: Guid; membershipId: Guid; profileId: Guid;
  kind: "Student" | "Teacher"; status: "linked" | "requires-review";
}

export interface MembershipAuditEntry {
  eventId: Guid; actorUserId: Guid; actorMembershipId: Guid; targetMembershipId: Guid;
  action: MembershipMutationAction | string; role?: string;
  previousStatus: string; newStatus: string;
  previousRoles: string[]; newRoles: string[];
  reason: string; revisionBefore: number; revisionAfter: number;
  occurredAtUtc: IsoDateTime;
}

export interface InviteMemberRequest { eventId: Guid; recipientUserId: Guid; roles: InstitutionRole[]; reason: string } // 1–4 ruoli distinti, reason ≤500
export interface InvitationChangeRequest { eventId: Guid; reason: string }
export interface InvitationAcceptanceRequest { institutionId: Guid; eventId: Guid; challenge: string }
export interface InvitationResult { eventId: Guid; status: "accepted" | "consumed" | string; membershipId?: Guid }
```

### 3.4 Catalogo accademico

```ts
export interface InstitutionAcademicPage<T> { items: T[]; totalCount: number; pageNumber: number; pageSize: number }

export interface InstitutionStudyAreaDto { id: Guid; name: string; description: string; revision: number }
export interface CreateInstitutionStudyAreaDto { name: string; description: string }   // name 1–100; description obbligatoria
export type UpdateInstitutionStudyAreaDto = CreateInstitutionStudyAreaDto;

export interface InstitutionStudyPathDto { id: Guid; name: string; description?: string; revision: number }
export interface CreateInstitutionStudyPathDto { name: string; description?: string | null }
export type UpdateInstitutionStudyPathDto = CreateInstitutionStudyPathDto;

export interface InstitutionBienniumDto { id: Guid; startYear: DateOnly; endYear: DateOnly; revision: number }
export interface CreateInstitutionBienniumDto { startYear: DateOnly; endYear: DateOnly } // nonostante il nome, sono DATE complete
export type UpdateInstitutionBienniumDto = CreateInstitutionBienniumDto;

export interface InstitutionBienniumStudyAreaDto { id: Guid; bienniumId: Guid; studyAreaId: Guid; revision: number }
export interface CreateInstitutionBienniumStudyAreaDto { bienniumId: Guid; studyAreaId: Guid }

export interface InstitutionBienniumStudyPathDto { id: Guid; bienniumStudyAreaId: Guid; studyPathId: Guid; revision: number }
export interface CreateInstitutionBienniumStudyPathDto { bienniumStudyAreaId: Guid; studyPathId: Guid }

export interface InstitutionCampusDto {
  id: Guid; campusId: Guid /* = id */; name: string;
  address?: string; city?: string; province?: string; isActive: boolean; revision: number;
}
export interface CreateInstitutionCampusDto { name: string; address?: string | null; city?: string | null; province?: string | null; isActive?: boolean /* default true */ }
export interface UpdateInstitutionCampusDto { name: string; address?: string | null; city?: string | null; province?: string | null; isActive: boolean /* obbligatorio */ }

export interface InstitutionClassDto { id: Guid; name: string; bienniumStudyPathId: Guid; courseEditionId?: Guid; revision: number }
export type InstitutionClassPageDto = InstitutionAcademicPage<InstitutionClassDto>;
export interface CreateInstitutionClassDto { name: string; bienniumStudyPathId: Guid } // name 1–20 (trim)
export interface UpdateInstitutionClassDto { name: string }

export interface InstitutionClassSubjectDto {
  id: Guid; classSubjectId: Guid /* = id */;
  classId: Guid; className: string; subjectId: Guid; subjectName: string;
  teacherId: Guid; teacherFirstName: string; teacherLastName: string; revision: number;
}
export interface CreateInstitutionClassSubjectDto { classId: Guid; subjectId: Guid; teacherId: Guid }
export interface UpdateInstitutionClassSubjectDto { teacherId: Guid }
```

### 3.5 Corsi e iscrizioni

```ts
export type CourseEditionStatus = "planned" | "active" | "completed" | "cancelled";
export type EnrollmentStatus = "enrolled" | "completed" | "withdrawn" | "cancelled";

export interface CourseEditionDto {
  id: Guid; institutionId: Guid; studyPathId: Guid; code: string; name: string;
  startDate?: DateOnly; endDate?: DateOnly; status: CourseEditionStatus;
  createdAt: IsoDateTime; updatedAt: IsoDateTime;
  version: string; // SHA-256 esadecimale (64 caratteri), da rimandare nel PUT
}
export interface CreateCourseEditionDto {
  studyPathId: Guid; code: string /* ≤64 */; name: string /* ≤200 */;
  startDate?: DateOnly | null; endDate?: DateOnly | null; status: CourseEditionStatus;
}
export interface UpdateCourseEditionDto {
  code: string; name: string; startDate?: DateOnly | null; endDate?: DateOnly | null;
  version: string; // esattamente 64 caratteri
}
export interface CampusDto { campusId: Guid; name: string; address?: string; city?: string; province?: string; isActive?: boolean }
export interface LinkEditionCampusDto { campusId: Guid }
export interface EditionClassDto { classId: Guid; name: string; bienniumStudyPathId: Guid; courseEditionId: Guid }

export interface StudentEnrollmentDto {
  enrollmentId: Guid; courseEditionId: Guid; courseEditionCode: string; courseEditionName: string;
  studentId: Guid; enrollmentDate: DateOnly; completionDate?: DateOnly; status: EnrollmentStatus; revision: number;
}
export interface OwnEnrollmentDto {
  enrollmentId: Guid; courseEditionId: Guid; courseEditionCode: string; courseEditionName: string;
  enrollmentDate: DateOnly; completionDate?: DateOnly; status: EnrollmentStatus;
}
export interface CreateStudentEnrollmentDto {
  studentId: Guid; courseEditionId: Guid; enrollmentDate: DateOnly; completionDate?: DateOnly | null; status: EnrollmentStatus;
}
export interface TransitionEnrollmentDto {
  eventId: Guid; targetStatus: EnrollmentStatus; completionDate: DateOnly; reason: string /* 1–1000 */; expectedRevision: number;
}
export interface CorrectEnrollmentCompletionDto { eventId: Guid; completionDate: DateOnly; reason: string; expectedRevision: number }
export interface EnrollmentMutationResult { eventId: Guid; revision: number; status: EnrollmentStatus; completionDate?: DateOnly }

export interface CourseAdministrationEventDto {
  eventId: Guid; institutionId: Guid; actorUserId: Guid; actorMembershipId: Guid; courseEditionId: Guid;
  enrollmentId?: Guid; campusId?: Guid; classId?: Guid; action: string;
  previousStatus?: string; newStatus?: string; previousCompletionDate?: DateOnly; newCompletionDate?: DateOnly;
  revisionBefore?: number; revisionAfter?: number; reason?: string;
  beforeJson?: string; afterJson: string; occurredAtUtc: IsoDateTime;
}
```

### 3.6 Anagrafiche e trasferimenti

```ts
export interface StudentDto { id: Guid; firstName: string; lastName: string }
export interface CreateStudentDto { firstName: string; lastName: string }   // 1–100 ciascuno
export type UpdateStudentDto = CreateStudentDto;

export interface TeacherDto { id: Guid; firstName: string; lastName: string }
export type CreateTeacherDto = CreateStudentDto;
export type UpdateTeacherDto = CreateStudentDto;

export interface SubjectDto { id: Guid; name: string }
export interface CreateSubjectDto { name: string }  // 1–150
export type UpdateSubjectDto = CreateSubjectDto;

export interface TransferStudentClassDto {
  eventId: Guid; toClassId: Guid; effectiveDate: DateOnly; reason: string /* 1–1000 */; expectedRevision: number;
}
export interface StudentClassTransferResultDto {
  eventId: Guid; studentId: Guid; fromClassId: Guid; toClassId: Guid;
  fromCourseEditionId: Guid; toCourseEditionId: Guid; effectiveDate: DateOnly;
  appliedLimitMonths: number; revision: number; occurredAtUtc: IsoDateTime;
}
export interface StudentClassTransferHistoryDto {
  receipt: StudentClassTransferResultDto;
  fromClassName: string; toClassName: string; fromCourseName: string; toCourseName: string;
  sourceEnrollmentId: Guid; destinationEnrollmentId: Guid; reason: string;
  actorUserId: Guid; actorMembershipId: Guid; revisionBefore: number;
  sourceCourseStartDate: DateOnly; destinationCourseStartDate: DateOnly;
}
export interface StudentClassTransferPageDto {
  items: StudentClassTransferHistoryDto[]; totalCount: number; pageNumber: number; pageSize: number;
  currentRevision: number; // da usare come expectedRevision del prossimo trasferimento
}
```

### 3.7 Voti

```ts
export interface GradeDto {
  id: Guid; studentId: Guid; subjectId: Guid; subjectName?: string; teacherId: Guid;
  value: number; date: DateOnly; student?: StudentDto;
}
export interface CreateGradeDto { studentId: Guid; subjectId: Guid; value: number /* 1–10 */; date: DateOnly }
export interface UpdateGradeDto { subjectId: Guid; value: number; date: DateOnly }
export interface GradePageDto { items: GradeDto[]; totalCount: number }
export interface GradeFiltersDto { subjects: SubjectDto[]; students: StudentDto[] }
export interface GradeStatisticsDto {
  yearlyAverage: number;
  monthlyAverage: (number | null)[]; // 12 elementi: indice 0 = gennaio … 11 = dicembre; null = nessun voto nel mese (anni diversi sommati, vedi OPEN_QUESTIONS Q-19)
}
```

### 3.8 Esami

```ts
// --- Gestione (docente/staff/admin)
export interface QuestionOptionInputDto { position: number /* ≥0, unico */; text: string /* 1–500 */; isCorrect: boolean }
export interface QuestionInputDto {
  position: number;   // ≥0, unico nell'esame
  text: string;       // obbligatorio
  type: QuestionType; // NUMERO: 0 Single, 1 Multiple, 2 Open
  points: number;     // > 0
  required: boolean;
  options: QuestionOptionInputDto[]; // Single: ≥2 opzioni, esattamente 1 corretta; Multiple: ≥2 opzioni, ≥2 corrette; Open: nessuna opzione
}
export interface ExamStudentInputDto { studentId: Guid; extraTimeMinutes: number /* ≥0 */ }
export interface CreateExamDto {
  classSubjectId: Guid;
  retakeOfExamId?: Guid | null;    // esame originale (stessa classe-materia)
  description?: string | null;
  availableFrom: IsoDateTime;      // UTC
  availableUntil: IsoDateTime;     // > availableFrom
  durationMinutes: number;         // > 5 e ≤ (availableUntil - availableFrom)
  passingPercent: number;          // 0 < x < 100, default 60
  assignedStudents: ExamStudentInputDto[]; // tutti della classe della classe-materia, senza duplicati
  questions: QuestionInputDto[];   // almeno 1
  // NB: il campo "name" è ignorato: il nome dell'esame = nome della materia
}
export interface UpdateExamDto extends Omit<CreateExamDto, "assignedStudents"> {} // classSubjectId deve restare uguale
export interface UpdateExamStudentsDto { students: ExamStudentInputDto[] }

export interface QuestionOptionDto { id: Guid; position: number; text: string; isCorrect: boolean }
export interface QuestionDto { id: Guid; position: number; text: string; type: QuestionType; points: number; required: boolean; options: QuestionOptionDto[] }
export interface ExamAssignedStudentDto { studentId: Guid; firstName: string; lastName: string; extraTimeMinutes: number; assignedAt: IsoDateTime }
export interface ExamSummaryDto {
  id: Guid; classSubjectId: Guid; retakeOfExamId?: Guid; name: string; description?: string;
  availableFrom: IsoDateTime; availableUntil: IsoDateTime; durationMinutes: number;
  passingPercent: number; totalPoints: number; status: ExamStatus; assignedStudentsCount: number;
  publishedAt?: IsoDateTime; closedAt?: IsoDateTime; cancelledAt?: IsoDateTime; isRetake: boolean;
}
export interface ExamDetailDto extends Omit<ExamSummaryDto, "assignedStudentsCount"> {
  questions: QuestionDto[];               // ordinate per position, con isCorrect
  assignedStudents: ExamAssignedStudentDto[];
}

// --- Studente
export interface AssignedExamDto {
  id: Guid; classSubjectId: Guid; name: string; description?: string;
  availableFrom: IsoDateTime; availableUntil: IsoDateTime; durationMinutes: number; extraTimeMinutes: number;
  status: ExamStatus; submissionStatus?: SubmissionStatus; submissionId?: Guid;
  startedAt?: IsoDateTime; deadlineAt?: IsoDateTime; submittedAt?: IsoDateTime;
  canStart: boolean;  // Published, nella finestra e senza consegna esistente
  isRetake: boolean;
}
export interface QuestionOptionForStudentDto { id: Guid; position: number; text: string } // niente isCorrect
export interface QuestionForStudentDto { id: Guid; position: number; text: string; type: QuestionType; points: number; required: boolean; options: QuestionOptionForStudentDto[] }
export interface ExamForStudentDto { id: Guid; name: string; description?: string; availableUntil: IsoDateTime; durationMinutes: number; totalPoints: number; questions: QuestionForStudentDto[] }
export interface StartExamDto { submissionId: Guid; startedAt: IsoDateTime; deadlineAt: IsoDateTime; exam: ExamForStudentDto }

export interface AnswerInputDto { questionId: Guid; openText?: string | null; selectedOptionIds: Guid[] }
export interface SaveExamAnswersDto { clientRevision: number; answers: AnswerInputDto[] } // sostituisce TUTTE le risposte
export interface SaveAnswersResponseDto { accepted: boolean; currentRevision: number; savedAt?: IsoDateTime }
export interface SubmitExamDto { clientRevision: number; answers: AnswerInputDto[] }
export interface SubmissionSessionDto {
  submissionId: Guid; examId: Guid; status: SubmissionStatus;
  startedAt: IsoDateTime; deadlineAt: IsoDateTime; submittedAt?: IsoDateTime;
  currentRevision: number; exam: ExamForStudentDto; answers: AnswerInputDto[];
}
export interface InstitutionViolationDto { eventId: Guid; type: "tab_hidden" | "window_blur" | "fullscreen_exit"; metadata?: string | null } // metadata: JSON oggetto serializzato, ≤1024 byte, profondità ≤4
export interface ViolationReceiptDto { eventId: Guid; violationCount: number; duplicate: boolean }
export interface ViolationEventDto { eventId: Guid; type: string; occurredAt: IsoDateTime; metadata?: string }

// --- Risultati e correzione
export interface SubmissionResultDto {
  submissionId: Guid; examId: Guid; status: SubmissionStatus; submittedAt?: IsoDateTime; submitReason?: SubmitReason;
  totalScore?: number; maxScore?: number; grade?: number; passed?: boolean;
  // Vista studente: grade/totalScore/maxScore/passed sono omessi finché non c'è un voto ufficiale (Graded o Finalized)
}
export interface SubmissionSummaryDto {
  id: Guid; examId: Guid; studentId: Guid; studentFirstName: string; studentLastName: string;
  status: SubmissionStatus; startedAt: IsoDateTime; deadlineAt: IsoDateTime; submittedAt?: IsoDateTime;
  submitReason?: SubmitReason; violationCount: number; totalScore?: number; maxScore?: number; passed?: boolean; gradeId?: Guid;
}
export interface SubmissionAnswerOptionReviewDto { id: Guid; position: number; text: string; isCorrect: boolean; isSelected: boolean }
export interface SubmissionAnswerReviewDto {
  answerId: Guid; questionId: Guid; position: number; questionText: string; questionType: QuestionType;
  maxPoints: number; required: boolean; openText?: string; isCorrect?: boolean; awardedPoints?: number;
  teacherComment?: string; options: SubmissionAnswerOptionReviewDto[];
}
export interface SubmissionReviewDto {
  id: Guid; examId: Guid; examName: string; studentId: Guid; studentFirstName: string; studentLastName: string;
  status: SubmissionStatus; startedAt: IsoDateTime; deadlineAt: IsoDateTime; submittedAt?: IsoDateTime;
  submitReason?: SubmitReason; violationCount: number;
  maxScore?: number; passingScore?: number; closedScore?: number; openMaxPoints?: number; openScore?: number;
  totalScore?: number; passed?: boolean; suggestedGrade?: number; gradeId?: Guid; finalGrade?: number;
  reviewedByUserId?: Guid; finalizedAt?: IsoDateTime; answers: SubmissionAnswerReviewDto[];
}
export interface OpenAnswerReviewInputDto { answerId: Guid; awardedPoints: number; comment?: string | null }
export interface ReviewOpenAnswersDto { answers: OpenAnswerReviewInputDto[] }
export interface FinalizeSubmissionDto {
  finalGrade?: number | null;     // null = usa il voto suggerito; 1–10
  overrideReason?: string | null; // obbligatorio se finalGrade ≠ voto suggerito
}
```

**Conversione punteggio → voto suggerito** (`Business/Services/SubmissionGradingService.cs`). Siano `p = totalScore / maxScore` e `s = passingPercent / 100`:

- se `p ≤ s`: voto = `1 + 5·(p/s)`;
- altrimenti: voto = `6 + 4·((p−s)/(1−s))`.

Il risultato è limitato all'intervallo 1–10 e arrotondato a 2 decimali. Sotto soglia il voto non è mai ≥ 6: in quel caso vale `5.99`.

### 3.9 Calendario

```ts
export interface CalendarSettings { timeZoneId: string; approvalLeadDays: number; revision: number }
export interface CalendarSettingsCommand { eventId: Guid; expectedRevision: number /* -1 alla prima configurazione */; timeZoneId: string; approvalLeadDays: number /* 0–366 */; reason: string }
export interface CalendarCommand { eventId: Guid; expectedRevision: number; reason: string } // reason 1–500
export interface CalendarReceipt { eventId: Guid; resourceId: Guid; revision: number; lessonIds?: Guid[] }

export interface CalendarLessonInput {
  classId: Guid; moduleId: Guid; teacherId: Guid;
  date: DateOnly; startsAt: TimeOnly; endsAt: TimeOnly;   // stesso giorno, al minuto, endsAt > startsAt
  substitutionReason?: string | null; room?: string | null; description?: string | null;
  externalReference?: string | null; recoveryOfLessonId?: Guid | null;
}
export interface CalendarSaveLesson { eventId: Guid; expectedRevision: number /* -1 per creare, altrimenti la revision corrente */; lesson: CalendarLessonInput; reason: string }
export interface CalendarRescheduleCommand { eventId: Guid; expectedRevision: number; date: DateOnly; startsAt: TimeOnly; endsAt: TimeOnly; reason: string }
export interface CalendarSubstituteCommand { eventId: Guid; expectedRevision: number; teacherId: Guid; reason: string }
export interface CalendarDeliveredCommand { eventId: Guid; expectedRevision: number; deliveredMinutes: number /* 1–1440 */; reason: string }

export interface CalendarLesson {
  id: Guid; periodId: Guid; classId: Guid; courseEditionId: Guid; versionId: Guid; moduleId: Guid;
  subjectId: Guid; classSubjectId: Guid; originalTeacherId: Guid; teacherId: Guid;
  date: DateOnly; startsAt: TimeOnly; endsAt: TimeOnly; timeZoneId: string;
  startUtc: IsoDateTime; endUtc: IsoDateTime;
  publication: "Draft" | "Published"; status: "Scheduled" | "Cancelled" | "Completed";
  revision: number; deliveredMinutes?: number; recoveryOfLessonId?: Guid;
  room?: string; description?: string; externalReference?: string;
  className?: string; moduleName?: string; teacherName?: string;
  durationMinutes: number;
}
/** Query string di GET /calendar, /calendar/classes/{id}, /calendar/teachers/me, /calendar/students/me */
export interface CalendarQuery {
  from: DateOnly; to: DateOnly;   // obbligatori, to ≥ from, max 366 giorni
  classId?: Guid; teacherId?: Guid; moduleId?: Guid; courseEditionId?: Guid;
  publication?: "Draft" | "Published"; status?: "Scheduled" | "Cancelled" | "Completed";
  includeCancelled?: boolean;     // default false
  page?: number;                  // default 1
  size?: number;                  // default 100, max 100
}
export interface CalendarApproval { deadlineLocalDate: DateOnly; isPublished: boolean; isLate: boolean; isOverdue: boolean }
export interface CalendarPeriod {
  id: Guid; classId: Guid; month: DateOnly; status: string; revision: number;
  publishedByUserId?: Guid; publishedByMembershipId?: Guid; publishedAtUtc?: IsoDateTime; approval: CalendarApproval;
}
export interface CalendarHistory {
  eventId: Guid; operation: string; actorUserId: Guid; actorMembershipId: Guid;
  revisionBefore?: number; revisionAfter: number; beforeJson?: string; stateJson: string; reason: string; occurredAtUtc: IsoDateTime;
}
export interface CalendarModuleHours {
  classId: Guid; moduleId: Guid; plannedMinutes: number; scheduledMinutes: number; deliveredMinutes: number;
  remainingToScheduleMinutes: number; remainingToDeliverMinutes: number;
  scheduledExcessMinutes: number; deliveredExcessMinutes: number;
  warnings: ("calendar.under-scheduled" | "calendar.over-scheduled" | "calendar.delivery-incomplete" | "calendar.over-delivered")[];
}
export interface CalendarUnitHours { unitId: Guid; plannedMinutes: number; scheduledMinutes: number; deliveredMinutes: number }
export interface CalendarMonthlyHours { year: number; month: number; scheduledMinutes: number; deliveredMinutes: number }
export interface CalendarHoursReport {
  classId: Guid; courseEditionId: Guid; versionId: Guid;
  modules: CalendarModuleHours[]; units: CalendarUnitHours[]; months: CalendarMonthlyHours[];
  plannedMinutes: number; scheduledMinutes: number; deliveredMinutes: number;
}

// Import calendario
export interface CalendarColumnMapping { columnIndex: number; field: CalendarImportField }
export interface CalendarImportSettings {
  eventId: Guid; year: number; month: number; classIds: Guid[]; mode: CalendarImportMode;
  mapping?: CalendarColumnMapping[] | null; reason: string;
}
export interface ImportIssue { field: string; code: string; message: string; warning: boolean }
export interface CalendarConflict { firstLessonId: Guid; secondLessonId: Guid; kind: CalendarConflictKind }
export interface CalendarImportRow {
  rowNumber: number; lesson?: CalendarLessonInput; className?: string; moduleName?: string; teacherName?: string;
  durationMinutes?: number; issues: ImportIssue[]; conflicts: CalendarConflict[]; previewLessonId: Guid;
  classReference?: string; moduleReference?: string; teacherReference?: string;
}
export interface CalendarImportImpact {
  classId: Guid; moduleId: Guid; plannedMinutes: number; publishedMinutes: number;
  draftMinutesAfterImport: number; incomingMinutes: number;
  projectedScheduledMinutes: number; warning?: "calendar.under-scheduled" | "calendar.over-scheduled";
}
export interface CalendarImportPreview {
  id: Guid; revision: number; digest: string; fileSha256: string; expiresAtUtc: IsoDateTime; status: string;
  scope: CalendarImportSettings; added: number; replaced: number; preserved: number;
  rows: CalendarImportRow[]; hours: CalendarImportImpact[]; referenceDigest: string;
  columns?: string[]; resolvedMapping?: CalendarColumnMapping[];
  totalRows: number; validRows: number; invalidRows: number; warningRows: number; duplicateRows: number; canConfirm: boolean;
}
export interface CalendarConfirmImport { eventId: Guid; expectedRevision: number; digest: string; acknowledgeWarnings: boolean; reason: string }
```

### 3.10 Piani formativi

```ts
export interface FormativeTopic { id: Guid; description: string; displayOrder: number }
export interface FormativeModuleSnapshot {
  id: Guid; code?: string; name: string; description?: string; plannedMinutes: number; displayOrder: number;
  subjectId?: Guid; competencies: FormativeTopic[]; contents: FormativeTopic[];
}
export interface FormativeUnitSnapshot { id: Guid; code?: string; name: string; description?: string; displayOrder: number; modules: FormativeModuleSnapshot[]; plannedMinutes: number }
export interface FormativePlanSnapshot {
  id: Guid; institutionId: Guid; studyPathId: Guid; planId: Guid; versionNumber: number; name: string;
  status: FormativeVersionStatus; revision: number; theoryPlannedMinutes: number; internshipPlannedMinutes: number;
  units: FormativeUnitSnapshot[]; createdAtUtc?: IsoDateTime; publishedAtUtc?: IsoDateTime;
}
export interface FormativePlanInfo { id: Guid; studyPathId: Guid; code: string; name: string; description?: string; revision: number; createdAtUtc: IsoDateTime }
export interface FormativeReceipt { eventId: Guid; planId: Guid; versionId?: Guid; revision: number; courseEditionId?: Guid }
export interface FormativeValidationIssue { code: string; resourceId: Guid }
export interface FormativeHourSummary {
  theoryPlannedMinutes: number; internshipPlannedMinutes: number; totalPlannedMinutes: number;
  assignedMinutes: number; missingMinutes: number; excessMinutes: number;
  issues: FormativeValidationIssue[]; canPublish: boolean;
}
export interface FormativeHistory {
  eventId: Guid; versionId?: Guid; courseEditionId?: Guid; operation: string; actorUserId: Guid; actorMembershipId: Guid;
  revisionBefore?: number; revisionAfter: number; beforeJson?: string; afterJson: string; reason: string; occurredAtUtc: IsoDateTime;
}

// Request
export interface CreateFormativePlan { eventId: Guid; studyPathId: Guid; code: string /* ≤64 */; name: string /* ≤200 */; description?: string | null; reason: string }
export interface RenameFormativePlan { eventId: Guid; expectedRevision: number; name: string; description?: string | null; reason: string }
export interface CreateFormativeVersion { eventId: Guid; name: string; theoryPlannedMinutes: number; internshipPlannedMinutes: number; reason: string }
export interface FormativeTopicInput { id?: Guid | null; description: string /* ≤4000 */; displayOrder: number }
export interface FormativeModuleInput {
  id?: Guid | null; code?: string | null; name: string; description?: string | null;
  plannedMinutes: number; displayOrder: number; subjectId?: Guid | null;
  competencies: FormativeTopicInput[]; contents: FormativeTopicInput[];
}
export interface FormativeUnitInput { id?: Guid | null; code?: string | null; name: string; description?: string | null; displayOrder: number; modules: FormativeModuleInput[] }
export interface ReplaceFormativeDraft {
  eventId: Guid; expectedRevision: number; name: string;
  theoryPlannedMinutes: number; internshipPlannedMinutes: number; units: FormativeUnitInput[]; reason: string;
}
export interface FormativeCommand { eventId: Guid; expectedRevision: number; reason: string }
export interface BindFormativeVersion { eventId: Guid; versionId: Guid; reason: string }
```

### 3.11 Email istituzionali

```ts
export interface EmailNamingRules {
  case?: EmailNameCase;                 // default "Lower"
  accents?: EmailAccentHandling;        // default "LatinFoldV1"
  wordSeparator?: string;               // default ""
  punctuation?: EmailPunctuationHandling; // default "Remove"
}
export interface EmailTemplate {
  localPartTemplate: string;   // 1–512; placeholder {nome} / {cognome} con trasformazioni :iniziale :minuscolo :maiuscolo :primi(N) (N 1–64), max 32 placeholder
  domain: string;              // dominio DNS ASCII
  names: EmailNamingRules;
  collision?: EmailCollisionStrategy; // default "Reject"
  numericSuffix?: string;      // default "{n}", deve contenere un solo {n}, 3–24 caratteri
  suffixStart?: number;        // default 2
  maximumAttempts?: number;    // default 32
}
export interface EmailPolicyVersion {
  policyId: Guid; category: EmailCategory; revision: number; state: "Draft" | "Published" | "Disabled";
  template: EmailTemplate; actorUserId: Guid; actorMembershipId: Guid; createdAtUtc: IsoDateTime;
  domainVerificationState: string; // default "ConfiguredUnverified"
}
export interface EmailPolicyWrite { eventId: Guid; expectedRevision: number; state: "Draft" | "Published" | "Disabled"; template: EmailTemplate; reason: string }
export interface EmailPreviewRequest { category: EmailCategory; policyRevision?: number | null; firstName?: string | null; lastName?: string | null; draft?: EmailTemplate | null }
export interface GeneratedInstitutionEmail { email: string; normalizedEmail: string; localPart: string; domain: string; warnings: string[] }
export interface EmailRecipient { studentId?: Guid | null; teacherId?: Guid | null; membershipId?: Guid | null; firstName?: string | null; lastName?: string | null }
export interface EmailReserve { eventId: Guid; category: EmailCategory; policyRevision: number; recipient: EmailRecipient }
export interface EmailCreatePerson { eventId: Guid; category: EmailCategory; policyRevision: number; firstName: string; lastName: string; classId?: Guid | null }
export interface EmailOverride { eventId: Guid; expectedRevision: number; policyRevision: number; email: string; reason: string }
export interface EmailRetire { eventId: Guid; expectedRevision: number; reason: string }
export interface EmailAssignment {
  id: Guid; studentId?: Guid; teacherId?: Guid; membershipId?: Guid; category: EmailCategory; policyRevision: number;
  email: string; state: string; revision: number; firstNameSnapshot: string; lastNameSnapshot: string;
  createdAtUtc: IsoDateTime; updatedAtUtc: IsoDateTime;
  mailboxVerified: boolean; domainVerified: boolean; loginEnabled: boolean; // sempre false nel codice attuale
}
export interface EmailPage<T> { pageNumber: number; pageSize: number; totalCount: number; items: T[] }
export interface EmailHistory { eventId: Guid; operation: string; actorUserId: Guid; actorMembershipId: Guid; beforeJson?: string; afterJson: string; reason: string; occurredAtUtc: IsoDateTime }
```

### 3.12 Import anagrafiche

```ts
export interface ImportColumnMapping { columnIndex: number; field: ImportField }
export interface ImportClassChoice { rowNumber: number; classId: Guid }
export interface ImportEmailCandidateChoice { rowNumber: number; candidateOffset: number }
export interface ImportPreviewSettings {
  category: ImportCategory;                // obbligatorio
  emailMode?: ImportEmailMode;             // default "None"
  mapping?: ImportColumnMapping[] | null;
  classes?: ImportClassChoice[] | null;    // disambiguazione classi
  emailCandidates?: ImportEmailCandidateChoice[] | null;
}
export interface ImportParsingOptions { delimiter?: ";" | "," | null; encoding?: "utf-8"; worksheetName?: string | null }
export interface ImportBatch {
  id: Guid; category: ImportCategory; state: string; revision: number; fileSha256: string; previewDigest: string;
  totalRows: number; validRows: number; invalidRows: number; createdAtUtc: IsoDateTime; expiresAtUtc: IsoDateTime;
  policyRevision?: number; warnings: ImportIssue[]; columns?: string[]; mapping?: ImportColumnMapping[];
}
export interface ImportBatchPage { pageNumber: number; pageSize: number; totalCount: number; items: ImportBatch[] }
export interface ImportValidatedRow {
  rowNumber: number; status: ImportRowStatus; firstName?: string; lastName?: string;
  classReference?: string; externalReference?: string; emailPreview?: string; issues: ImportIssue[];
}
export interface ImportPreparedRow { data: ImportValidatedRow; classId?: Guid; classFingerprint?: string; classCandidates?: Guid[] }
export interface ImportRowsPage { pageNumber: number; pageSize: number; totalCount: number; items: ImportPreparedRow[] }
export interface ImportRemap { eventId: Guid; expectedRevision: number; settings: ImportPreviewSettings }
export interface ImportConfirmation {
  eventId: Guid; expectedRevision: number; previewDigest: string;
  acknowledgeWarnings?: boolean;   // default false
  allowRepeatedFile?: boolean;     // default false; se true serve reason
  reason?: string | null;
}
export interface ImportCreatedRow { rowNumber: number; studentId?: Guid; teacherId?: Guid; emailAssignmentId?: Guid }
export interface ImportResult { importId: Guid; revision: number; createdCount: number; rows: ImportCreatedRow[] }
```

---

## 4. Convenzioni su null/undefined

| Caso | Regola |
|---|---|
| Risposta | i campi `null` sono **omessi** → in TS trattarli come `undefined` |
| Richiesta: campo facoltativo | si può omettere o mandare `null` |
| Richiesta: campo `[JsonRequired]` | deve essere presente, anche se il valore è 0/false |
| Richiesta: stringhe | molti campi vengono **trimmati** lato server (nomi, motivazioni). Le stringhe composte solo da spazi sono rifiutate. |
| Booleani con default | `isActive` (creazione sede) = `true`, `acknowledgeWarnings` = `false`, `includeCancelled` = `false` |
