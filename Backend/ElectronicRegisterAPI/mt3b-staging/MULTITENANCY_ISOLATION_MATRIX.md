# MULTITENANCY ISOLATION MATRIX - stato corrente MT-3B

**Student/Teacher/Subject tenant-aware nel livello Application/Infrastructure. MT-3 complessiva PARZIALE; HTTP scolastico ancora legacy, gate chiuso.** Dettagli e 426/426 test in due run: [report MT-3B](../../tests/Integration/MULTITENANCY_MT3B_REPORT.md).

| Livello / risorsa | Garanzia corrente | Limite |
| --- | --- | --- |
| Autenticazione MT2 | JWT operativo scoped, membership/Institution Active, ruoli della membership | Nessuna apertura automatica delle route scolastiche |
| Manager istituzionali | ICurrentTenant esistente, rivalidazione corrente e permesso per ogni operazione | Revoca non retroattiva su operazioni gia autorizzate |
| Profili | Resolver e query legano Institution, membership, identita e owner diretto | Nessun fallback User.StudentId/TeacherId; assenza nega |
| Student letture | Admin tenant; Teacher solo classi assegnate in SQL; self-profile separato | Student non accede alla directory |
| Teacher letture | Directory locale per ruoli ammessi; lookup ClassSubject scoped; Count Admin/Teacher | Un User globale puo avere profili A/B distinti |
| Subject letture | Admin catalogo locale; Teacher assegnazioni; Student propria classe | byTeacher mantiene intersezione sulla stessa assegnazione, non solo SubjectId |
| CRUD tre famiglie | Admin, owner shadow esplicito in INSERT, predicato owner+ID in UPDATE/DELETE | Nessun trasferimento owner o creazione automatica membership/ruoli |
| Dipendenze | Class/Teacher/Subject/Student/CS owner coerente; FK MT3A; Delete protetto | Nuovo Student Delete richiede transazione e lock parent per conservare Grade |
| SQL MT3A | 19 tabelle owned, 27 FK composite, cataloghi scoped e 40 trigger invariati | SQL non concede permessi di lettura o ruoli |
| Concorrenza | Scope A/B distinti; due Admin; update cross-tenant negato; UNIQUE scoped concorrente | Nessun benchmark di carico dichiarato |
| HTTP MT2 | Cinque API istituzionali e gate legacy verificati | Nessuna API scolastica scoped esposta |
| Legacy / Exam / Grade / Audit / worker | Percorsi precedenti preservati e protetti dal gate | Migrazione selettiva C/D/E ancora necessaria |
| Holding tecnica | MT3A_LEGACY_TEST provisioning, dati legacy invariati | Manifest SQL distinto da LEGACY_MT1 auth; nessuna riconciliazione implicita |

I repository richiedono TenantContext verificato dal manager e non sono un'autenticazione JWT autonoma. I nuovi test usano servizi e repository reali con MySQL electronic_register_test: 39 casi aggiunti ai 387 precedenti, senza mock di integrita SQL. Baseline e insiemi dei test confrontati tramite TRX; bootstrap reale Development/Production incluso. Non si dichiara isolamento HTTP positivo per controller non migrati.

La tabella e la ricognizione storiche seguenti descrivono lo stato precedente: le sole famiglie Student, Teacher e Subject hanno ora i percorsi dedicati sopra. I metodi globali restano utilizzati dai flussi legacy e non vengono esposti dai nuovi manager.

---

## Snapshot storico MT-3A e fasi precedenti


# MULTITENANCY ISOLATION MATRIX — MT-3A

**Schema owned e vincolato; isolamento HTTP scolastico ancora non migrato. Gate MT2 chiuso per tutti i nuovi token A/B. MT-3 complessiva PARZIALE.**

L'inventario corrente completo, generato dai metadati effettivi delle 28 tabelle e arricchito con decisioni/limiti, e in [MULTITENANCY_MT3A_OWNERSHIP_INVENTORY.md](MULTITENANCY_MT3A_OWNERSHIP_INVENTORY.md): PK, tutte le UNIQUE e FK/delete rule, percorsi, rischi, modifiche, SQL/app e analisi di ogni indice. Il report e in [MULTITENANCY_MT3A_REPORT.md](../../tests/Integration/MULTITENANCY_MT3A_REPORT.md).

Le categorie qui sotto descrivono lo stato SQL corrente; la ricognizione repository/controller/raw SQL conservata piu sotto rimane la lista dei percorsi da migrare. Nessuna di quelle letture e diventata tenant-aware per effetto di FK o shadow property.

| Tabella | Categoria | Percorso di attribuzione |
| --- | --- | --- |
| `audit_event` | E REQUIRES_DECISION (polimorfica: B oppure D per riga) | Exam/ExamSubmission -> Institution; platform esplicito -> NULL |
| `biennium` | B TENANT_OWNED_DIRECT | InstitutionId diretto, manifest legacy esplicito |
| `biennium_study_area` | B TENANT_OWNED_DIRECT | InstitutionId materializzato da Biennium, confrontato con StudyArea |
| `biennium_study_path` | B TENANT_OWNED_DIRECT | InstitutionId materializzato da BienniumStudyArea, confrontato con StudyPath |
| `campus` | B TENANT_OWNED_DIRECT | institution_id -> Institution |
| `class` | B TENANT_OWNED_DIRECT | InstitutionId materializzato da BienniumStudyPath |
| `class_subject` | B TENANT_OWNED_DIRECT | InstitutionId materializzato da Class; Subject e Teacher concordi |
| `exam` | B TENANT_OWNED_DIRECT | InstitutionId materializzato da ClassSubject; retake stesso tenant |
| `exam_student` | B TENANT_OWNED_DIRECT | InstitutionId materializzato da Exam; Student concorde |
| `exam_submission` | B TENANT_OWNED_DIRECT | InstitutionId materializzato da Exam; Student e Grade concordi |
| `exam_violation` | B TENANT_OWNED_DIRECT | InstitutionId materializzato dalla Submission |
| `grade` | B TENANT_OWNED_DIRECT | InstitutionId materializzato da Student; ClassSubject concorde |
| `institution` | D PLATFORM_INTERNAL | Registro tenant; id identifica la Fondazione |
| `institution_membership` | B TENANT_OWNED_DIRECT | institution_id -> Institution; user_id -> User globale |
| `institution_membership_role` | C TENANT_OWNED_INDIRECT | membership_id -> Membership -> Institution |
| `institution_student_profile` | B TENANT_OWNED_DIRECT | Institution + Membership + Student dello stesso istituto |
| `institution_teacher_profile` | B TENANT_OWNED_DIRECT | Institution + Membership + Teacher dello stesso istituto |
| `mt3a_legacy_scope` | D PLATFORM_INTERNAL | Manifest SQL esplicito -> Institution provisioning di test |
| `question` | B TENANT_OWNED_DIRECT | InstitutionId materializzato da Exam |
| `question_option` | B TENANT_OWNED_DIRECT | InstitutionId materializzato da Question |
| `student` | B TENANT_OWNED_DIRECT | InstitutionId diretto; Class coerente se presente; membership opzionale |
| `study_area` | B TENANT_OWNED_DIRECT | InstitutionId diretto, manifest legacy esplicito |
| `study_path` | B TENANT_OWNED_DIRECT | InstitutionId diretto, manifest legacy esplicito |
| `subject` | B TENANT_OWNED_DIRECT | InstitutionId diretto, manifest legacy esplicito |
| `submission_answer` | B TENANT_OWNED_DIRECT | InstitutionId + exam_id materializzati dalla Submission |
| `submission_answer_option` | B TENANT_OWNED_DIRECT | InstitutionId + question_id materializzati da Answer |
| `teacher` | B TENANT_OWNED_DIRECT | InstitutionId diretto; membership opzionale tramite link |
| `user` | A GLOBAL_IDENTITY | Credenziali globali; User.Id |

## Garanzie SQL e confini HTTP

| Confine | DB effettivo | Stato applicativo/HTTP |
| --- | --- | --- |
| Class -> percorso; ponti Biennium/Area/Path | FK composite stesso owner | Query didattiche legacy ancora da filtrare/autorizzare |
| ClassSubject -> Class/Teacher/Subject | Tre FK composite | Ruolo/docente/gruppo e directory scoped da migrare |
| Student/Teacher -> Institution/link profilo/Membership | Owner diretto, UNIQUE profilo e FK composite link | Resolver MT3 preparatorio; manager scolastici non migrati |
| Exam/Retake -> ClassSubject/Exam | FK composite, scope SET NULL separata | ExamManager/lifecycle/retake semantici invariati e dietro gate |
| ExamStudent/Submission/Grade -> Exam/Student/ClassSubject/Grade | FK composite; Grade SET NULL conserva owner | Enrollment, Grade ufficiale entro tenant, reviewer e permessi ancora applicativi |
| Answer -> Submission/Question | FK triple stesso istituto e stesso esame | Filtri lettura/lock/autorizzazione da migrare; formule invariate |
| Ponte opzioni -> Answer/Option | FK triple stessa domanda, anche nello stesso tenant | Single/Multiple/Open e cardinalita applicative |
| Violation -> Submission | Owner/FK composita e CASCADE storico | Accesso e worker per tenant/item da migrare |
| Audit polimorfico | Attribuzione DB per tipi noti, owner immutabile; platform esplicito privo di payload | Nessuna lettura globale o API nuova; nuovi tipi richiedono classificazione |
| User globale / membership | Credenziali uniche; scope/ruoli MT1/MT2 | Nuovo auth e revoca preservati; account management istituzionale ancora da implementare |
| Route legacy | Nessuna FK autorizza il caller | Ticket e JWT scoped A/B continuano a essere rifiutati dal gate/audience |

Le prove nuove sono SQL reali (39 casi); le prove MT2 mantengono il diniego HTTP su tutte le route legacy protette. Non sono test HTTP positivi di didattica A/B, che restano checkpoint successivi. Nessun secondo tenant operativo.

---

## Ricognizione MT-3 parziale conservata (snapshot precedente allo schema MT-3A)


# MULTITENANCY ISOLATION MATRIX - MT-3 PARZIALE

Ricognizione prima del DDL. Tutte le API didattiche restano bloccate ai token scoped: nessuna query legacy diventa sicura dal solo Guid.

| Risorsa / tabella | Ownership attuale / destinazione | Endpoint | Repository | Operazioni / stato |
| --- | --- | --- | --- | --- |
| user | Globale intenzionale; account management scolastico da confinare a membership | Auth, Users | UserRepository | Login globale legittimo; CRUD globale bloccato ai tenant nuovi |
| student | Legacy non attribuito; profilo univoco via institution_student_profile preparatorio | Student, Grade, Exam | StudentRepository | CRUD/count/search: da migrare |
| teacher | Legacy non attribuito; profilo univoco via institution_teacher_profile preparatorio | Teacher, Subject, Exam | TeacherRepository | CRUD/directory: da migrare |
| subject | Da aggiungere institution_id e UNIQUE(institution_id,name) | Subject, Grade, Exam | SubjectRepository | CRUD/lookup: da migrare |
| study_area | Da aggiungere institution_id e UNIQUE(institution_id,name) | non HTTP | StudyAreaRepository | CRUD interno non isolato |
| study_path | Da aggiungere institution_id e UNIQUE(institution_id,name) | non HTTP | StudyPathRepository | CRUD interno non isolato |
| biennium | Da aggiungere institution_id e UNIQUE(institution_id,start_year,end_year) | Biennium | BienniumRepository | CRUD/alias Subject da migrare |
| biennium_study_area | Da derivare/confermare da Biennium; composite FK ad Area stesso tenant | indiretto | StudyArea/StudyPath/Biennium | join da migrare |
| biennium_study_path | Da derivare/confermare da ponte e Path; composite FK | indiretto | StudyPath/Biennium | join da migrare; non CourseEdition |
| class | Da Institution via percorso; composite FK a catena | indiretto Student/Exam | Student/ClassSubject | join da migrare |
| class_subject | Da institution_id diretto; FK composite Class/Teacher/Subject | Subject/Grade/Exam | ClassSubjectRepository | tutte le ownership da migrare |
| grade | Da institution_id diretto; FK composite Student/ClassSubject | Grade/Exam | GradeRepository | CRUD/paged/statistics/finalizzazione da migrare |
| exam | Da institution_id diretto; FK composite ClassSubject e Retake | Exam | ExamRepository | CRUD/lifecycle/assigned da migrare |
| exam_student | Da Exam; composite FK a Student nello stesso tenant | Exam assegnazioni | ExamRepository | da migrare |
| exam_submission | Da institution_id diretto; composite FK Exam/Student/Grade | Exam | ExamSubmissionRepository | Start/save/submit/review/finalize/result/worker da migrare |
| question | Derivata da Exam; chiave (institution_id,exam_id,id) se necessaria per FK risposte | Exam | ExamRepository | nessuna API globale; indiretta da migrare |
| question_option | Derivata da Question; verificare domanda esatta oltre tenant | Exam | ExamRepository/ExamSubmissionRepository | FK opzioni-risposte da progettare |
| submission_answer | Derivata da Submission; FK composita con domanda dello stesso Exam | Exam | ExamSubmissionRepository | tenant non basta a verificare stesso Exam |
| submission_answer_option | Derivata da Answer; opzione deve appartenere alla domanda della risposta | Exam | ExamSubmissionRepository | ponte many-to-many, nessun DbSet esplicito; da migrare |
| exam_violation | Derivata da Submission | Exam | ExamSubmissionRepository | da migrare |
| audit_event | Da institution_id diretto; EntityId polimorfico richiede preflight per eventi non riconducibili | non HTTP diretto | AuditRepository | append atomico con Exam/submission; da migrare |
| institution | Tenant root; non regione | Auth istituzioni selezionabili | InstitutionRepository/MembershipAuthorizationRepository | MT1/MT2; nessun onboarding pubblico |
| campus | FK institution_id diretta | non HTTP | CampusRepository | lookup scoped, non sostituisce autorizzazione |
| institution_membership | User globale + Institution, coppia unica | Auth | InstitutionMembershipRepository/MembershipAuthorizationRepository | MT2 verificato; gestione pubblica non implementata |
| institution_membership_role | Derivata da membership | Auth | InstitutionMembershipRepository | MT2 verificato, nessun ruolo globale |
| institution_student_profile | NUOVA: ownership Student unica e FK (institution_id,membership_id) | nessuno | InstitutionProfileRepository | preparatorio; risolutore senza fallback |
| institution_teacher_profile | NUOVA: ownership Teacher unica e FK (institution_id,membership_id) | nessuno | InstitutionProfileRepository | preparatorio; risolutore senza fallback |

## Tutti i DbSet e join many-to-many

```text
Infrastructure\ElectronicRegisterAPI.Infrastructure\Persistence\ElectronicRegisterContext.cs:15: public virtual DbSet<AuditEvent> AuditEvents { get; set; }
Infrastructure\ElectronicRegisterAPI.Infrastructure\Persistence\ElectronicRegisterContext.cs:17: public virtual DbSet<Biennium> Biennia { get; set; }
Infrastructure\ElectronicRegisterAPI.Infrastructure\Persistence\ElectronicRegisterContext.cs:19: public virtual DbSet<BienniumStudyArea> BienniumStudyAreas { get; set; }
Infrastructure\ElectronicRegisterAPI.Infrastructure\Persistence\ElectronicRegisterContext.cs:21: public virtual DbSet<BienniumStudyPath> BienniumStudyPaths { get; set; }
Infrastructure\ElectronicRegisterAPI.Infrastructure\Persistence\ElectronicRegisterContext.cs:23: public virtual DbSet<Class> Classes { get; set; }
Infrastructure\ElectronicRegisterAPI.Infrastructure\Persistence\ElectronicRegisterContext.cs:25: public virtual DbSet<ClassSubject> ClassSubjects { get; set; }
Infrastructure\ElectronicRegisterAPI.Infrastructure\Persistence\ElectronicRegisterContext.cs:27: public virtual DbSet<Exam> Exams { get; set; }
Infrastructure\ElectronicRegisterAPI.Infrastructure\Persistence\ElectronicRegisterContext.cs:29: public virtual DbSet<ExamStudent> ExamStudents { get; set; }
Infrastructure\ElectronicRegisterAPI.Infrastructure\Persistence\ElectronicRegisterContext.cs:31: public virtual DbSet<ExamSubmission> ExamSubmissions { get; set; }
Infrastructure\ElectronicRegisterAPI.Infrastructure\Persistence\ElectronicRegisterContext.cs:33: public virtual DbSet<ExamViolation> ExamViolations { get; set; }
Infrastructure\ElectronicRegisterAPI.Infrastructure\Persistence\ElectronicRegisterContext.cs:35: public virtual DbSet<Grade> Grades { get; set; }
Infrastructure\ElectronicRegisterAPI.Infrastructure\Persistence\ElectronicRegisterContext.cs:37: public virtual DbSet<Question> Questions { get; set; }
Infrastructure\ElectronicRegisterAPI.Infrastructure\Persistence\ElectronicRegisterContext.cs:39: public virtual DbSet<QuestionOption> QuestionOptions { get; set; }
Infrastructure\ElectronicRegisterAPI.Infrastructure\Persistence\ElectronicRegisterContext.cs:41: public virtual DbSet<Student> Students { get; set; }
Infrastructure\ElectronicRegisterAPI.Infrastructure\Persistence\ElectronicRegisterContext.cs:43: public virtual DbSet<StudyArea> StudyAreas { get; set; }
Infrastructure\ElectronicRegisterAPI.Infrastructure\Persistence\ElectronicRegisterContext.cs:45: public virtual DbSet<StudyPath> StudyPaths { get; set; }
Infrastructure\ElectronicRegisterAPI.Infrastructure\Persistence\ElectronicRegisterContext.cs:47: public virtual DbSet<Subject> Subjects { get; set; }
Infrastructure\ElectronicRegisterAPI.Infrastructure\Persistence\ElectronicRegisterContext.cs:49: public virtual DbSet<SubmissionAnswer> SubmissionAnswers { get; set; }
Infrastructure\ElectronicRegisterAPI.Infrastructure\Persistence\ElectronicRegisterContext.cs:51: public virtual DbSet<Teacher> Teachers { get; set; }
Infrastructure\ElectronicRegisterAPI.Infrastructure\Persistence\ElectronicRegisterContext.cs:53: public virtual DbSet<User> Users { get; set; }
Infrastructure\ElectronicRegisterAPI.Infrastructure\Persistence\ElectronicRegisterContext.cs:635: .UsingEntity<Dictionary<string, object>>(
Infrastructure\ElectronicRegisterAPI.Infrastructure\Persistence\ElectronicRegisterContext.Mt3Profiles.cs:7: public virtual DbSet<InstitutionStudentProfile> InstitutionStudentProfiles { get; set; }
Infrastructure\ElectronicRegisterAPI.Infrastructure\Persistence\ElectronicRegisterContext.Mt3Profiles.cs:8: public virtual DbSet<InstitutionTeacherProfile> InstitutionTeacherProfiles { get; set; }
Infrastructure\ElectronicRegisterAPI.Infrastructure\Persistence\ElectronicRegisterContext.Multitenancy.cs:10: public virtual DbSet<Institution> Institutions { get; set; }
Infrastructure\ElectronicRegisterAPI.Infrastructure\Persistence\ElectronicRegisterContext.Multitenancy.cs:11: public virtual DbSet<Campus> Campuses { get; set; }
Infrastructure\ElectronicRegisterAPI.Infrastructure\Persistence\ElectronicRegisterContext.Multitenancy.cs:12: public virtual DbSet<InstitutionMembership> InstitutionMemberships { get; set; }
Infrastructure\ElectronicRegisterAPI.Infrastructure\Persistence\ElectronicRegisterContext.Multitenancy.cs:13: public virtual DbSet<InstitutionMembershipRole> InstitutionMembershipRoles { get; set; }
```

## Repository e metodi: scolastici da migrare; auth globale intenzionale; nuovi profili scoped

```text
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\AuditRepository.cs:11: public AuditRepository(ElectronicRegisterContext context)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\AuditRepository.cs:16: public async Task AddAsync(AuditEvent auditEvent)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\BienniumRepository.cs:12: public BienniumRepository(ElectronicRegisterContext context)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\BienniumRepository.cs:17: public async Task<int> CountAsync()
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\BienniumRepository.cs:22: public async Task<IEnumerable<BienniumModel>> GetAllAsync()
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\BienniumRepository.cs:28: public async Task<IEnumerable<BienniumModel>> GetActiveBienniaAsync()
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\BienniumRepository.cs:40: public async Task<IEnumerable<BienniumModel>> GetInactiveBienniaAsync()
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\BienniumRepository.cs:50: public async Task<bool> HasStudyAreasAsync(Guid bienniumId)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\BienniumRepository.cs:55: public async Task<BienniumModel?> GetByIdAsync(Guid id)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\BienniumRepository.cs:61: public async Task<BienniumModel?> GetBienniumByStartYearAsync(int startYear)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\BienniumRepository.cs:67: public async Task<BienniumModel?> GetBienniumByEndYearAsync(int endYear)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\BienniumRepository.cs:73: public async Task AddAsync(BienniumModel biennium)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\BienniumRepository.cs:85: public async Task UpdateAsync(BienniumModel biennium)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\BienniumRepository.cs:94: public async Task DeleteAsync(BienniumModel biennium)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\CampusRepository.cs:11: public async Task<Campus?> GetByIdAsync(Guid institutionId, Guid id)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\CampusRepository.cs:17: public async Task<List<Campus>> GetByInstitutionAsync(Guid institutionId) =>
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\CampusRepository.cs:20: public async Task AddAsync(Campus campus)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\ClassSubjectRepository.cs:14: public ClassSubjectRepository(ElectronicRegisterContext context)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\ClassSubjectRepository.cs:19: public async Task<DomainClassSubject?> GetByIdAsync(Guid id)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\ClassSubjectRepository.cs:28: public async Task<List<DomainClassSubject>> GetByIdsAsync(IEnumerable<Guid> ids)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\ClassSubjectRepository.cs:43: public async Task<DomainClassSubject?> GetAsync(Guid classId, Guid subjectId, Guid? teacherId = null)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\ClassSubjectRepository.cs:58: public async Task<List<DomainClassSubject>> GetByTeacherIdAsync(Guid teacherId)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\ClassSubjectRepository.cs:73: public async Task<List<DomainClassSubject>> GetBySubjectIdAsync(Guid subjectId)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\ClassSubjectRepository.cs:88: public async Task<List<DomainClassSubject>> GetByClassIdAsync(Guid classId)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\ClassSubjectRepository.cs:103: public Task<bool> ExistsForTeacherAsync(Guid teacherId)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\ClassSubjectRepository.cs:108: public Task<bool> ExistsForSubjectAsync(Guid subjectId)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\ExamRepository.cs:18: public ExamRepository(ElectronicRegisterContext context)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\ExamRepository.cs:23: public async Task<Exam?> GetByIdAsync( Guid id, bool includeStructure = false, bool includeStudents = false)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\ExamRepository.cs:45: public async Task<Exam?> GetByIdForUpdateAsync(Guid id)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\ExamRepository.cs:55: public Task<bool> HasRetakesAsync(Guid examId) =>
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\ExamRepository.cs:57: public async Task<List<Exam>> GetAllAsync(Guid? teacherId = null)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\ExamRepository.cs:90: public async Task<List<ExamSummaryDto>> GetSummariesAsync(Guid? teacherId = null)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\ExamRepository.cs:139: public async Task<List<Exam>> GetAssignedToStudentAsync(Guid studentId)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\ExamRepository.cs:154: public async Task<List<Exam>> GetByClassSubjectAsync(Guid classSubjectId)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\ExamRepository.cs:164: public Task<bool> HasSubmissionsAsync(Guid examId)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\ExamRepository.cs:168: public Task<bool> HasInProgressSubmissionsAsync(Guid examId)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\ExamRepository.cs:178: public async Task AddAsync(Exam exam)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\ExamRepository.cs:245: public async Task UpdateAsync(Exam exam)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\ExamRepository.cs:267: public async Task ReplaceStructureAsync(Exam exam)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\ExamRepository.cs:319: public async Task ReplaceAssignedStudentsAsync(Guid examId, IReadOnlyCollection<ExamStudent> students)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\ExamRepository.cs:342: public async Task DeleteDraftAsync(Guid examId)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\ExamSubmissionRepository.cs:19: public ExamSubmissionRepository(ElectronicRegisterContext context, ElectronicRegisterAPI.Domain.Interfaces.Services.ITimeProvider timeProvider)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\ExamSubmissionRepository.cs:25: public async Task<ExamSubmission?> GetByIdAsync(Guid id, bool withAnswers = false)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\ExamSubmissionRepository.cs:44: public async Task<ExamSubmission?> GetByIdForUpdateAsync(Guid id, bool withAnswers = false)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\ExamSubmissionRepository.cs:74: public async Task<ExamSubmission?> GetByExamAndStudentAsync(Guid examId, Guid studentId, bool withAnswers = false)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\ExamSubmissionRepository.cs:90: public async Task<List<ExamSubmission>> GetByExamAsync(Guid examId, SubmissionStatus? status = null)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\ExamSubmissionRepository.cs:109: public async Task<List<ExamSubmission>> GetByStudentAsync(Guid studentId)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\ExamSubmissionRepository.cs:120: public async Task<List<ExamSubmission>> GetExpiredInProgressAsync(DateTime utcNow, int take)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\ExamSubmissionRepository.cs:135: public async Task AddAsync(ExamSubmission submission)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\ExamSubmissionRepository.cs:196: public async Task<SaveAnswersResult> SaveAnswersAsync(Guid submissionId, long clientRevision, IReadOnlyList<SubmissionAnswer> answers)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\ExamSubmissionRepository.cs:355: public async Task<bool> TryBeginSubmitAsync(Guid submissionId, DateTime submittedAt, SubmitReason reason)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\ExamSubmissionRepository.cs:374: public async Task UpdateAfterGradingAsync(ExamSubmission submission)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\ExamSubmissionRepository.cs:416: public async Task UpdateAfterReviewAsync(ExamSubmission submission)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\GradeRepository.cs:14: public GradeRepository(ElectronicRegisterContext context)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\GradeRepository.cs:19: public async Task<Grade?> GetByIdAsync(Guid id)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\GradeRepository.cs:25: public async Task<List<Grade>> GetAllAsync(Guid? teacherId, Guid? studentId)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\GradeRepository.cs:39: public async Task<int> CountAsync(Guid? teacherId = null, Guid? studentId = null)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\GradeRepository.cs:53: public async Task<List<Grade>> GetByDateAsync(DateOnly date, Guid? studentId = null, Guid? teacherId = null)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\GradeRepository.cs:63: public async Task<List<Grade>> GetBySubjectNameAsync(string subjectName, Guid? studentId = null, Guid? teacherId = null)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\GradeRepository.cs:72: public async Task<(List<Grade> Items, int TotalCount)>GetPagedAsync(
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\GradeRepository.cs:108: public async Task<GradeStatistics> GetStatisticsAsync(Guid? teacherId, Guid? studentId)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\GradeRepository.cs:141: public async Task<List<Grade>> GetByStudentIdAsync(Guid studentId)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\GradeRepository.cs:148: public async Task AddAsync(Grade grade)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\GradeRepository.cs:162: public async Task UpdateAsync(Grade grade)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\GradeRepository.cs:170: public async Task DeleteAsync(Grade grade)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\GradeRepository.cs:178: public async Task<bool> ExistsForStudentAsync(Guid studentId)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\GradeRepository.cs:183: public Task<bool> IsLinkedToExamSubmissionAsync(Guid gradeId) =>
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\InstitutionMembershipRepository.cs:13: public async Task<InstitutionMembership?> GetAsync(Guid institutionId, Guid userId)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\InstitutionMembershipRepository.cs:25: public async Task AddAsync(InstitutionMembership membership)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\InstitutionMembershipRepository.cs:44: public async Task AddRoleAsync(Guid institutionId, Guid membershipId, InstitutionRole role)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\InstitutionMembershipRepository.cs:53: public async Task SuspendAsync(Guid institutionId, Guid membershipId, DateTime suspendedAtUtc)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\InstitutionProfileRepository.cs:8: public Task<Guid?> GetStudentAsync(TenantContext tenant, CancellationToken cancellationToken = default) =>
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\InstitutionProfileRepository.cs:12: public Task<Guid?> GetTeacherAsync(TenantContext tenant, CancellationToken cancellationToken = default) =>
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\InstitutionRepository.cs:12: public async Task<Institution?> GetByIdAsync(Guid id)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\InstitutionRepository.cs:18: public async Task<Institution?> GetByCodeAsync(string code)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\InstitutionRepository.cs:24: public async Task AddAsync(Institution institution)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\MembershipAuthorizationRepository.cs:11: public Task<IReadOnlyList<MembershipAuthorization>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\MembershipAuthorizationRepository.cs:14: public async Task<MembershipAuthorization?> GetAsync(Guid userId, Guid institutionId, Guid membershipId, CancellationToken cancellationToken = default) =>
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\StudentRepository.cs:13: public StudentRepository(ElectronicRegisterContext context)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\StudentRepository.cs:18: public async Task<int> CountAsync(Guid? teacherId = null)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\StudentRepository.cs:23: public async Task<List<Student>> GetAllAsync(Guid? teacherId = null)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\StudentRepository.cs:29: public async Task<Student?> GetByIdAsync(Guid id, Guid? teacherId = null)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\StudentRepository.cs:35: public async Task<List<Student>> GetByIdsAsync(IEnumerable<Guid> ids)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\StudentRepository.cs:41: public async Task<List<Student>> GetByLastNameAsync(string lastName, Guid? teacherId = null)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\StudentRepository.cs:47: public async Task AddAsync(Student student)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\StudentRepository.cs:60: public async Task UpdateAsync(Student student)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\StudentRepository.cs:68: public async Task DeleteAsync(Student student)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\StudyAreaRepository.cs:13: public StudyAreaRepository(ElectronicRegisterContext context)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\StudyAreaRepository.cs:18: public async Task<int> CountAsync()
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\StudyAreaRepository.cs:23: public async Task<List<StudyAreaModel>> GetAllAsync()
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\StudyAreaRepository.cs:29: public async Task<StudyAreaModel?> GetByIdAsync(Guid id)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\StudyAreaRepository.cs:35: public async Task<StudyAreaModel?> GetStudyAreaByNameAsync(string name)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\StudyAreaRepository.cs:41: public async Task<bool> HasStudyPathsAsync(Guid studyAreaId)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\StudyAreaRepository.cs:46: public async Task AddAsync(StudyAreaModel studyArea)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\StudyAreaRepository.cs:58: public async Task UpdateAsync(StudyAreaModel studyArea)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\StudyAreaRepository.cs:67: public async Task DeleteAsync(StudyAreaModel studyArea)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\StudyPathRepository.cs:13: public StudyPathRepository(ElectronicRegisterContext context)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\StudyPathRepository.cs:18: public async Task<int> CountAsync()
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\StudyPathRepository.cs:23: public async Task<List<StudyPathModel>> GetAllAsync()
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\StudyPathRepository.cs:29: public async Task<StudyPathModel?> GetByIdAsync(Guid id)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\StudyPathRepository.cs:35: public async Task<StudyPathModel?> GetByNameAsync(string name)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\StudyPathRepository.cs:41: public async Task<bool> HasClassesAsync(Guid studyPathId)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\StudyPathRepository.cs:48: public async Task AddAsync(StudyPathModel studyPath)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\StudyPathRepository.cs:60: public async Task UpdateAsync(StudyPathModel studyPath)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\StudyPathRepository.cs:69: public async Task DeleteAsync(StudyPathModel studyPath)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\SubjectRepository.cs:11: public SubjectRepository(ElectronicRegisterContext context)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\SubjectRepository.cs:16: public async Task<int> CountAsync()
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\SubjectRepository.cs:21: public async Task<List<DomainSubject>> GetAllAsync()
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\SubjectRepository.cs:33: public async Task<DomainSubject?> GetByIdAsync(Guid id)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\SubjectRepository.cs:46: public async Task<List<DomainSubject>> GetByIdsAsync(IEnumerable<Guid> ids)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\SubjectRepository.cs:59: public async Task<DomainSubject?> GetByNameAsync(string name)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\SubjectRepository.cs:72: public async Task AddAsync(DomainSubject subject)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\SubjectRepository.cs:84: public async Task UpdateAsync(DomainSubject subject)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\SubjectRepository.cs:97: public async Task DeleteAsync(DomainSubject subject)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\TeacherRepository.cs:13: public TeacherRepository(ElectronicRegisterContext context)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\TeacherRepository.cs:18: public async Task<int> CountAsync()
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\TeacherRepository.cs:23: public async Task<List<Teacher>> GetAllAsync()
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\TeacherRepository.cs:29: public async Task<Teacher?> GetByIdAsync(Guid id)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\TeacherRepository.cs:35: public async Task<List<Teacher>> GetByIdsAsync(IEnumerable<Guid> ids)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\TeacherRepository.cs:41: public async Task<List<Teacher>> GetByLastNameAsync(string lastName)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\TeacherRepository.cs:47: public async Task AddAsync(Teacher teacher)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\TeacherRepository.cs:59: public async Task UpdateAsync(Teacher teacher)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\TeacherRepository.cs:68: public async Task DeleteAsync(Teacher teacher)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\TransactionRepository.cs:10: public TransactionManager(ElectronicRegisterContext context)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\TransactionRepository.cs:15: public async Task ExecuteAsync(Func<Task> action, CancellationToken cancellationToken = default)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\UserRepository.cs:15: public UserRepository(ElectronicRegisterContext context)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\UserRepository.cs:20: public async Task<User?> GetByEmailAsync(string email)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\UserRepository.cs:26: public async Task<User?> GetByIdAsync(Guid id)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\UserRepository.cs:32: public async Task<List<User>> GetAllAsync()
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\UserRepository.cs:38: public async Task<int> CountAsync()
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\UserRepository.cs:43: public async Task AddAsync(User userDto)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\UserRepository.cs:50: public async Task UpdateAsync(User user)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\UserRepository.cs:64: public async Task DeleteAsync(User user)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\UserRepository.cs:74: public async Task<bool> ExistsByEmailAsync(string email)
```

## Manager e metodi: ClaimsContext legacy ancora da migrare

```text
Application\ElectronicRegisterAPI.Application\Managers\AuthManager.cs:24: public AuthManager(
Application\ElectronicRegisterAPI.Application\Managers\AuthManager.cs:45: public async Task<string?> LoginAsync(LoginDto dto)
Application\ElectronicRegisterAPI.Application\Managers\AuthManager.cs:54: public async Task<string?> MicrosoftLoginAsync(MicrosoftLoginDto dto)
Application\ElectronicRegisterAPI.Application\Managers\AuthManager.cs:64: public Task RegisterAsync(RegisterDto dto) =>
Application\ElectronicRegisterAPI.Application\Managers\AuthManager.cs:94: public Task RegisterForAdminAsync(RegisterForAdminDto dto) =>
Application\ElectronicRegisterAPI.Application\Managers\AuthManager.cs:136: public async Task<UserDto?> GetCurrentUserAsync(ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\BienniumManager.cs:15: public BienniumManager(IBienniumService bienniumService, IBienniumRepository bienniumRepository)
Application\ElectronicRegisterAPI.Application\Managers\BienniumManager.cs:21: public async Task<int> CountAsync()
Application\ElectronicRegisterAPI.Application\Managers\BienniumManager.cs:26: public async Task<List<BienniumDto>> GetAllAsync()
Application\ElectronicRegisterAPI.Application\Managers\BienniumManager.cs:32: public async Task<List<BienniumDto>> GetActiveBienniaAsync()
Application\ElectronicRegisterAPI.Application\Managers\BienniumManager.cs:38: public async Task<List<BienniumDto>> GetInactiveBienniaAsync()
Application\ElectronicRegisterAPI.Application\Managers\BienniumManager.cs:44: public async Task<BienniumDto?> GetByIdAsync(Guid id)
Application\ElectronicRegisterAPI.Application\Managers\BienniumManager.cs:50: public async Task<BienniumDto?> GetBienniumByStartYearAsync(int startYear)
Application\ElectronicRegisterAPI.Application\Managers\BienniumManager.cs:56: public async Task<BienniumDto?> GetBienniumByEndYearAsync(int endYear)
Application\ElectronicRegisterAPI.Application\Managers\BienniumManager.cs:62: public async Task<Guid> AddAsync(CreateBienniumDto dto)
Application\ElectronicRegisterAPI.Application\Managers\BienniumManager.cs:75: public async Task<bool> UpdateAsync(Guid id, UpdateBienniumDto dto)
Application\ElectronicRegisterAPI.Application\Managers\BienniumManager.cs:86: public async Task<bool> DeleteAsync(Guid id)
Application\ElectronicRegisterAPI.Application\Managers\ExamManager.cs:28: public ExamManager(
Application\ElectronicRegisterAPI.Application\Managers\ExamManager.cs:57: public async Task<List<ExamSummaryDto>> GetAllAsync(ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\ExamManager.cs:76: public async Task<ExamDetailDto?> GetByIdAsync(Guid id, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\ExamManager.cs:117: public async Task<List<AssignedExamDto>> GetAssignedAsync(ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\ExamManager.cs:140: public async Task<Guid> AddAsync(CreateExamDto dto, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\ExamManager.cs:209: public async Task UpdateAsync(Guid id, UpdateExamDto dto, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\ExamManager.cs:277: public async Task UpdateStudentsAsync(Guid id, UpdateExamStudentsDto dto, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\ExamManager.cs:328: public async Task PublishAsync(Guid id, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\ExamManager.cs:366: public async Task CloseAsync(Guid id, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\ExamManager.cs:399: public async Task CancelAsync(Guid id, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\ExamManager.cs:432: public async Task DeleteAsync(Guid id, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\ExamManager.cs:462: public async Task<StartExamDto> StartAsync(Guid id, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\ExamManager.cs:511: public async Task<SaveAnswersResponseDto> SaveAnswersAsync(Guid id, SaveExamAnswersDto dto, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\ExamManager.cs:544: public async Task<SubmissionResultDto> SubmitAsync(Guid id, SubmitExamDto dto, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\ExamManager.cs:570: public async Task<SubmissionResultDto> GetResultAsync(Guid id, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\ExamSubmissionManager.cs:25: public ExamSubmissionManager(
Application\ElectronicRegisterAPI.Application\Managers\ExamSubmissionManager.cs:47: public async Task<List<SubmissionSummaryDto>> GetByExamAsync(Guid examId, SubmissionStatus? status, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\ExamSubmissionManager.cs:99: public async Task<SubmissionReviewDto> GetByIdAsync(Guid id, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\ExamSubmissionManager.cs:229: public async Task ReviewAsync(Guid id, ReviewOpenAnswersDto dto, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\ExamSubmissionManager.cs:309: public async Task FinalizeAsync(Guid id, FinalizeSubmissionDto dto, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\GradeManager.cs:20: public GradeManager(
Application\ElectronicRegisterAPI.Application\Managers\GradeManager.cs:34: public async Task<int> CountAsync(ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\GradeManager.cs:44: public async Task<List<GradeDto>> GetAllAsync(ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\GradeManager.cs:61: public async Task<GradeDto?> GetByIdAsync(Guid id, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\GradeManager.cs:93: public async Task<GradePageDto> GetPagedAsync(
Application\ElectronicRegisterAPI.Application\Managers\GradeManager.cs:116: public async Task<GradeStatisticsDto?> GetStatisticsAsync(ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\GradeManager.cs:131: public async Task<GradeFiltersDto> GetFiltersAsync(ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\GradeManager.cs:194: public async Task<List<GradeDto>>GetGradesByStudentIdAsync(Guid studentId)
Application\ElectronicRegisterAPI.Application\Managers\GradeManager.cs:208: public async Task<List<GradeDto>?>GetGradesBySubjectNameAsync(string subjectName, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\GradeManager.cs:229: public async Task<List<GradeDto>>GetGradesByDateAsync(DateOnly date, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\GradeManager.cs:250: public async Task<Guid?> AddAsync(CreateGradeDto dto, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\GradeManager.cs:303: public async Task<bool> UpdateAsync(Guid id, UpdateGradeDto dto, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\GradeManager.cs:367: public async Task<bool> DeleteAsync(Guid id)
Application\ElectronicRegisterAPI.Application\Managers\InstitutionAuthManager.cs:15: public async Task<IdentityTicketDto?> LoginAsync(LoginDto input)
Application\ElectronicRegisterAPI.Application\Managers\InstitutionAuthManager.cs:22: public async Task<IdentityTicketDto?> MicrosoftLoginAsync(MicrosoftLoginDto input)
Application\ElectronicRegisterAPI.Application\Managers\InstitutionAuthManager.cs:29: public async Task<bool> IdentityExistsAsync(Guid userId) => await users.GetByIdAsync(userId) is not null;
Application\ElectronicRegisterAPI.Application\Managers\InstitutionAuthManager.cs:31: public async Task<IReadOnlyList<InstitutionChoiceDto>> InstitutionsAsync(Guid userId, CancellationToken cancellationToken) =>
Application\ElectronicRegisterAPI.Application\Managers\InstitutionAuthManager.cs:36: public async Task<InstitutionAccessDto> SelectAsync(Guid userId, SelectInstitutionDto input, CancellationToken cancellationToken)
Application\ElectronicRegisterAPI.Application\Managers\InstitutionAuthManager.cs:47: public async Task<TenantContext?> AuthorizeAsync(TenantContext token, CancellationToken cancellationToken)
Application\ElectronicRegisterAPI.Application\Managers\StudentManager.cs:13: public StudentManager(IStudentRepository studentRepository, IStudentService studentService)
Application\ElectronicRegisterAPI.Application\Managers\StudentManager.cs:19: public async Task<int> CountAsync(ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\StudentManager.cs:24: public async Task<List<StudentDto>> GetAllAsync(ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\StudentManager.cs:30: public async Task<StudentDto?> GetByIdAsync(Guid id, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\StudentManager.cs:37: public async Task<List<StudentDto>> GetStudentsByLastNameAsync(string lastName, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\StudentManager.cs:42: public async Task<bool> UpdateAsync(Guid id, UpdateStudentDto dto)
Application\ElectronicRegisterAPI.Application\Managers\StudentManager.cs:53: public async Task<Guid> AddAsync(CreateStudentDto dto)
Application\ElectronicRegisterAPI.Application\Managers\StudentManager.cs:65: public async Task<bool> DeleteAsync(Guid id)
Application\ElectronicRegisterAPI.Application\Managers\StudyAreaManager.cs:14: public StudyAreaManager(IStudyAreaService studyAreaService, IStudyAreaRepository studyAreaRepository)
Application\ElectronicRegisterAPI.Application\Managers\StudyAreaManager.cs:20: public async Task<int> CountAsync()
Application\ElectronicRegisterAPI.Application\Managers\StudyAreaManager.cs:25: public async Task<List<StudyAreaDto>> GetAllAsync()
Application\ElectronicRegisterAPI.Application\Managers\StudyAreaManager.cs:31: public async Task<StudyAreaDto?> GetByIdAsync(Guid id)
Application\ElectronicRegisterAPI.Application\Managers\StudyAreaManager.cs:37: public async Task<StudyAreaDto?> GetStudyAreaByNameAsync(string name)
Application\ElectronicRegisterAPI.Application\Managers\StudyAreaManager.cs:43: public async Task<bool> AddAsync(CreateStudyAreaDto dto)
Application\ElectronicRegisterAPI.Application\Managers\StudyAreaManager.cs:58: public async Task<bool> UpdateAsync(Guid id, UpdateStudyAreaDto dto)
Application\ElectronicRegisterAPI.Application\Managers\StudyAreaManager.cs:73: public async Task<bool> DeleteAsync(Guid id)
Application\ElectronicRegisterAPI.Application\Managers\StudyPathManager.cs:14: public StudyPathManager(IStudyPathRepository repository, IStudyPathService service)
Application\ElectronicRegisterAPI.Application\Managers\StudyPathManager.cs:20: public async Task<int> CountAsync()
Application\ElectronicRegisterAPI.Application\Managers\StudyPathManager.cs:24: public async Task<List<StudyPathDto>> GetAllAsync()
Application\ElectronicRegisterAPI.Application\Managers\StudyPathManager.cs:29: public async Task<StudyPathDto?> GetByIdAsync(Guid id)
Application\ElectronicRegisterAPI.Application\Managers\StudyPathManager.cs:34: public async Task<StudyPathDto?> GetStudyPathByNameAsync(string name)
Application\ElectronicRegisterAPI.Application\Managers\StudyPathManager.cs:39: public async Task<bool> AddAsync(CreateStudyPathDto dto)
Application\ElectronicRegisterAPI.Application\Managers\StudyPathManager.cs:53: public async Task<bool> UpdateAsync(Guid id, UpdateStudyPathDto dto)
Application\ElectronicRegisterAPI.Application\Managers\StudyPathManager.cs:66: public async Task<bool> DeleteAsync(Guid id)
Application\ElectronicRegisterAPI.Application\Managers\SubjectManager.cs:17: public SubjectManager(
Application\ElectronicRegisterAPI.Application\Managers\SubjectManager.cs:29: public async Task<int> CountAsync(ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\SubjectManager.cs:38: public async Task<List<SubjectDto>> GetAllAsync(ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\SubjectManager.cs:44: public async Task<SubjectDto?> GetByIdAsync(Guid id, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\SubjectManager.cs:56: public async Task<SubjectDto?> GetSubjectByNameAsync(string name, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\SubjectManager.cs:75: public async Task<List<SubjectDto>>GetSubjectsByTeacherIdAsync(Guid teacherId, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\SubjectManager.cs:108: public async Task<bool> UpdateAsync(Guid id, UpdateSubjectDto dto)
Application\ElectronicRegisterAPI.Application\Managers\SubjectManager.cs:121: public async Task<Guid?> AddAsync(CreateSubjectDto dto)
Application\ElectronicRegisterAPI.Application\Managers\SubjectManager.cs:136: public async Task<bool> DeleteAsync(Guid id)
Application\ElectronicRegisterAPI.Application\Managers\TeacherManager.cs:15: public TeacherManager(ITeacherRepository teacherRepository, ITeacherService teacherService)
Application\ElectronicRegisterAPI.Application\Managers\TeacherManager.cs:21: public async Task<int> CountAsync()
Application\ElectronicRegisterAPI.Application\Managers\TeacherManager.cs:26: public async Task<List<TeacherDto>> GetAllAsync()
Application\ElectronicRegisterAPI.Application\Managers\TeacherManager.cs:32: public async Task<TeacherDto?> GetByIdAsync(Guid id)
Application\ElectronicRegisterAPI.Application\Managers\TeacherManager.cs:38: public async Task<bool> UpdateAsync(Guid id, UpdateTeacherDto dto)
Application\ElectronicRegisterAPI.Application\Managers\TeacherManager.cs:50: public async Task<Guid?> AddAsync(CreateTeacherDto dto)
Application\ElectronicRegisterAPI.Application\Managers\TeacherManager.cs:62: public async Task<bool> DeleteAsync(Guid id)
Application\ElectronicRegisterAPI.Application\Managers\UserManager.cs:20: public UserManager(
Application\ElectronicRegisterAPI.Application\Managers\UserManager.cs:36: public Task<int> CountAsync() => _userRepository.CountAsync();
Application\ElectronicRegisterAPI.Application\Managers\UserManager.cs:38: public async Task<List<UserDto>> GetAllAsync()
Application\ElectronicRegisterAPI.Application\Managers\UserManager.cs:44: public async Task<UserDto?> GetByIdAsync(Guid id)
Application\ElectronicRegisterAPI.Application\Managers\UserManager.cs:52: public async Task<bool> UpdatePasswordAsync(Guid id, UpdatePasswordDto dto, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\UserManager.cs:67: public async Task<bool> UpdateAsync(Guid id, UpdateUserDto dto)
Application\ElectronicRegisterAPI.Application\Managers\UserManager.cs:120: public async Task<Guid> AddAsync(CreateUserDto dto)
Application\ElectronicRegisterAPI.Application\Managers\UserManager.cs:141: public async Task<bool> DeleteAsync(Guid id)
```

## Controller: route, metodo HTTP, ruoli, input e output

```text
Api\ElectronicRegisterAPI\Controllers\AuthController.cs:11: [Route("api/[controller]")]
Api\ElectronicRegisterAPI\Controllers\AuthController.cs:16: public AuthController(IAuthManager authManager)
Api\ElectronicRegisterAPI\Controllers\AuthController.cs:21: [HttpPost("login")]
Api\ElectronicRegisterAPI\Controllers\AuthController.cs:22: public async Task<ActionResult> Login(LoginDto dto)
Api\ElectronicRegisterAPI\Controllers\AuthController.cs:28: [HttpPost("microsoft-login")]
Api\ElectronicRegisterAPI\Controllers\AuthController.cs:29: public async Task<ActionResult> MicrosoftLogin([FromBody] MicrosoftLoginDto dto)
Api\ElectronicRegisterAPI\Controllers\AuthController.cs:40: [HttpPost("register")]
Api\ElectronicRegisterAPI\Controllers\AuthController.cs:41: [Authorize(Roles = "admin")]
Api\ElectronicRegisterAPI\Controllers\AuthController.cs:42: public async Task<ActionResult> Register(RegisterDto dto)
Api\ElectronicRegisterAPI\Controllers\AuthController.cs:48: [HttpPost("RegisterForAdmin")]
Api\ElectronicRegisterAPI\Controllers\AuthController.cs:49: [Authorize(Roles = "admin")]
Api\ElectronicRegisterAPI\Controllers\AuthController.cs:50: public async Task<ActionResult> RegisterForAdmin(RegisterForAdminDto dto)
Api\ElectronicRegisterAPI\Controllers\AuthController.cs:56: [HttpGet("me")]
Api\ElectronicRegisterAPI\Controllers\AuthController.cs:57: [Authorize(Roles = "teacher,admin,student")]
Api\ElectronicRegisterAPI\Controllers\AuthController.cs:58: public async Task<ActionResult<UserDto>> GetCurrentUser()
Api\ElectronicRegisterAPI\Controllers\BienniumController.cs:10: [Route("api/[controller]")]
Api\ElectronicRegisterAPI\Controllers\BienniumController.cs:11: [Authorize]
Api\ElectronicRegisterAPI\Controllers\BienniumController.cs:17: public BienniumController(IBienniumManager bienniumManager, ISubjectManager subjectManager)
Api\ElectronicRegisterAPI\Controllers\BienniumController.cs:23: [HttpGet("count")]
Api\ElectronicRegisterAPI\Controllers\BienniumController.cs:24: [Authorize(Roles = "admin")]
Api\ElectronicRegisterAPI\Controllers\BienniumController.cs:25: public async Task<ActionResult<int>> Count()
Api\ElectronicRegisterAPI\Controllers\BienniumController.cs:30: [HttpGet]
Api\ElectronicRegisterAPI\Controllers\BienniumController.cs:31: [Authorize(Roles = "admin")]
Api\ElectronicRegisterAPI\Controllers\BienniumController.cs:32: public async Task<ActionResult<List<BienniumDto>>> GetAll()
Api\ElectronicRegisterAPI\Controllers\BienniumController.cs:38: [HttpGet("active")]
Api\ElectronicRegisterAPI\Controllers\BienniumController.cs:39: [Authorize(Roles = "admin")]
Api\ElectronicRegisterAPI\Controllers\BienniumController.cs:40: public async Task<ActionResult<List<BienniumDto>>> GetActiveBiennia()
Api\ElectronicRegisterAPI\Controllers\BienniumController.cs:46: [HttpGet("inactive")]
Api\ElectronicRegisterAPI\Controllers\BienniumController.cs:47: [Authorize(Roles = "admin")]
Api\ElectronicRegisterAPI\Controllers\BienniumController.cs:48: public async Task<ActionResult<List<BienniumDto>>> GetInactiveBiennia()
Api\ElectronicRegisterAPI\Controllers\BienniumController.cs:54: [HttpGet("{id}")]
Api\ElectronicRegisterAPI\Controllers\BienniumController.cs:55: [Authorize(Roles = "admin")]
Api\ElectronicRegisterAPI\Controllers\BienniumController.cs:56: public async Task<ActionResult<BienniumDto>> GetById(Guid id)
Api\ElectronicRegisterAPI\Controllers\BienniumController.cs:62: [HttpGet("byname/{name}")]
Api\ElectronicRegisterAPI\Controllers\BienniumController.cs:63: [Authorize(Roles = "admin")]
Api\ElectronicRegisterAPI\Controllers\BienniumController.cs:64: public async Task<ActionResult<SubjectDto>> GetSubjectByName(string name)
Api\ElectronicRegisterAPI\Controllers\BienniumController.cs:70: [HttpGet("byteacher/{id}")]
Api\ElectronicRegisterAPI\Controllers\BienniumController.cs:71: [Authorize(Roles = "admin")]
Api\ElectronicRegisterAPI\Controllers\BienniumController.cs:72: public async Task<ActionResult<List<SubjectDto>>> GetSubjectByTeacherId(Guid id)
Api\ElectronicRegisterAPI\Controllers\BienniumController.cs:78: [HttpPut("update/{id}")]
Api\ElectronicRegisterAPI\Controllers\BienniumController.cs:79: [Authorize(Roles = "admin")]
Api\ElectronicRegisterAPI\Controllers\BienniumController.cs:80: public async Task<ActionResult> Update(Guid id, UpdateBienniumDto dto)
Api\ElectronicRegisterAPI\Controllers\BienniumController.cs:86: [HttpPost]
Api\ElectronicRegisterAPI\Controllers\BienniumController.cs:87: [Authorize(Roles = "admin")]
Api\ElectronicRegisterAPI\Controllers\BienniumController.cs:88: public async Task<ActionResult> Add(CreateBienniumDto dto)
Api\ElectronicRegisterAPI\Controllers\BienniumController.cs:94: [HttpDelete("{id}")]
Api\ElectronicRegisterAPI\Controllers\BienniumController.cs:95: [Authorize(Roles = "admin")]
Api\ElectronicRegisterAPI\Controllers\BienniumController.cs:96: public async Task<ActionResult> Delete(Guid id)
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:10: [Route("api/[controller]")]
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:11: [Authorize]
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:17: public ExamController(IExamManager examManager, IExamSubmissionManager submissionManager)
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:23: [HttpGet]
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:24: [Authorize(Roles = "teacher,admin")]
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:25: public async Task<ActionResult<List<ExamSummaryDto>>> GetAll()
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:31: [HttpGet("{id}")]
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:32: [Authorize(Roles = "teacher,admin")]
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:33: public async Task<ActionResult<ExamDetailDto>> GetById(Guid id)
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:39: [HttpGet("assigned")]
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:40: [Authorize(Roles = "student")]
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:41: public async Task<ActionResult<List<AssignedExamDto>>> GetAssigned()
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:47: [HttpPost]
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:48: [Authorize(Roles = "teacher,admin")]
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:49: public async Task<ActionResult> Add(CreateExamDto dto)
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:55: [HttpPut("{id}")]
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:56: [Authorize(Roles = "teacher,admin")]
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:57: public async Task<ActionResult> Update(Guid id, UpdateExamDto dto)
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:63: [HttpPut("{id}/students")]
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:64: [Authorize(Roles = "teacher,admin")]
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:65: public async Task<ActionResult> UpdateStudents(Guid id, UpdateExamStudentsDto dto)
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:71: [HttpPost("{id}/publish")]
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:72: [Authorize(Roles = "teacher,admin")]
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:73: public async Task<ActionResult> Publish(Guid id)
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:79: [HttpPost("{id}/close")]
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:80: [Authorize(Roles = "teacher,admin")]
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:81: public async Task<ActionResult> Close(Guid id)
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:87: [HttpPost("{id}/cancel")]
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:88: [Authorize(Roles = "teacher,admin")]
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:89: public async Task<ActionResult> Cancel(Guid id)
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:95: [HttpDelete("{id}")]
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:96: [Authorize(Roles = "teacher,admin")]
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:97: public async Task<ActionResult> Delete(Guid id)
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:103: [HttpPost("{id}/start")]
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:104: [Authorize(Roles = "student")]
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:105: public async Task<ActionResult<StartExamDto>> Start(Guid id)
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:111: [HttpPost("{id}/answers/save")]
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:112: [Authorize(Roles = "student")]
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:113: public async Task<ActionResult<SaveAnswersResponseDto>> SaveAnswers(Guid id, SaveExamAnswersDto dto)
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:119: [HttpPost("{id}/submit")]
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:120: [Authorize(Roles = "student")]
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:121: public async Task<ActionResult<SubmissionResultDto>> Submit(Guid id, SubmitExamDto dto)
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:127: [HttpGet("{examId}/submissions")]
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:128: [Authorize(Roles = "teacher,admin")]
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:129: public async Task<ActionResult<List<SubmissionSummaryDto>>> GetSubmissions(Guid examId, [FromQuery] SubmissionStatus? status)
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:134: [HttpGet("submission/{id}/review")]
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:135: [Authorize(Roles = "teacher,admin")]
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:136: public async Task<ActionResult<SubmissionReviewDto>> GetReview(Guid id)
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:141: [HttpPut("submission/{id}/review")]
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:142: [Authorize(Roles = "teacher,admin")]
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:143: public async Task<ActionResult> Review(Guid id, ReviewOpenAnswersDto dto)
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:149: [HttpPost("submission/{id}/finalize")]
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:150: [Authorize(Roles = "teacher,admin")]
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:151: public async Task<ActionResult> FinalizeSubmission(Guid id, FinalizeSubmissionDto dto)
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:157: [HttpGet("submission/{id}/result")]
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:158: [Authorize(Roles = "teacher,admin,student")]
Api\ElectronicRegisterAPI\Controllers\ExamController.cs:159: public async Task<ActionResult<SubmissionResultDto>> GetResult(Guid id)
Api\ElectronicRegisterAPI\Controllers\GradeController.cs:10: [Route("api/[controller]")]
Api\ElectronicRegisterAPI\Controllers\GradeController.cs:11: [Authorize]
Api\ElectronicRegisterAPI\Controllers\GradeController.cs:16: public GradeController(IGradeManager gradeManager)
Api\ElectronicRegisterAPI\Controllers\GradeController.cs:21: [HttpGet("count")]
Api\ElectronicRegisterAPI\Controllers\GradeController.cs:22: [Authorize(Roles = "teacher,admin,student")]
Api\ElectronicRegisterAPI\Controllers\GradeController.cs:23: public async Task<ActionResult<int>> Count()
Api\ElectronicRegisterAPI\Controllers\GradeController.cs:29: [HttpGet("statistics")]
Api\ElectronicRegisterAPI\Controllers\GradeController.cs:30: [Authorize(Roles = "teacher,admin,student")]
Api\ElectronicRegisterAPI\Controllers\GradeController.cs:31: public async Task<ActionResult<GradeStatisticsDto>> GetStatistics()
Api\ElectronicRegisterAPI\Controllers\GradeController.cs:37: [HttpGet("filters")]
Api\ElectronicRegisterAPI\Controllers\GradeController.cs:38: [Authorize(Roles = "teacher,admin,student")]
Api\ElectronicRegisterAPI\Controllers\GradeController.cs:39: public async Task<ActionResult<GradeFiltersDto>> GetFiltered()
Api\ElectronicRegisterAPI\Controllers\GradeController.cs:45: [HttpGet("paged")]
Api\ElectronicRegisterAPI\Controllers\GradeController.cs:46: [Authorize(Roles = "teacher,admin,student")]
Api\ElectronicRegisterAPI\Controllers\GradeController.cs:47: public async Task<ActionResult<GradePageDto>> GetPaged(
Api\ElectronicRegisterAPI\Controllers\GradeController.cs:66: [HttpGet]
Api\ElectronicRegisterAPI\Controllers\GradeController.cs:67: [Authorize(Roles = "teacher,admin,student")]
Api\ElectronicRegisterAPI\Controllers\GradeController.cs:68: public async Task<ActionResult<List<GradeDto>>> GetAll()
Api\ElectronicRegisterAPI\Controllers\GradeController.cs:74: [HttpGet("{id}")]
Api\ElectronicRegisterAPI\Controllers\GradeController.cs:75: [Authorize(Roles = "teacher,admin,student")]
Api\ElectronicRegisterAPI\Controllers\GradeController.cs:76: public async Task<ActionResult<GradeDto>> GetById(Guid id)
Api\ElectronicRegisterAPI\Controllers\GradeController.cs:82: [HttpGet("bystudentid/{id}")]
Api\ElectronicRegisterAPI\Controllers\GradeController.cs:83: [Authorize(Roles = "admin")]
Api\ElectronicRegisterAPI\Controllers\GradeController.cs:84: public async Task<ActionResult<List<GradeDto>>> GetGradesByStudentId(Guid id)
Api\ElectronicRegisterAPI\Controllers\GradeController.cs:90: [HttpGet("bysubject/{subject}")]
Api\ElectronicRegisterAPI\Controllers\GradeController.cs:91: [Authorize(Roles = "teacher,admin,student")]
Api\ElectronicRegisterAPI\Controllers\GradeController.cs:92: public async Task<ActionResult<List<GradeDto>>> GetGradesBySubjectName(string subject)
Api\ElectronicRegisterAPI\Controllers\GradeController.cs:98: [HttpGet("bydate/{date}")]
Api\ElectronicRegisterAPI\Controllers\GradeController.cs:99: [Authorize(Roles = "teacher,admin,student")]
Api\ElectronicRegisterAPI\Controllers\GradeController.cs:100: public async Task<ActionResult<List<GradeDto>>> GetGradesByDate(DateOnly date)
Api\ElectronicRegisterAPI\Controllers\GradeController.cs:106: [HttpPost]
Api\ElectronicRegisterAPI\Controllers\GradeController.cs:107: [Authorize(Roles = "teacher,admin")]
Api\ElectronicRegisterAPI\Controllers\GradeController.cs:108: public async Task<ActionResult> Add(CreateGradeDto dto)
Api\ElectronicRegisterAPI\Controllers\GradeController.cs:114: [HttpPut("update/{id}")]
Api\ElectronicRegisterAPI\Controllers\GradeController.cs:115: [Authorize(Roles = "teacher,admin")]
Api\ElectronicRegisterAPI\Controllers\GradeController.cs:116: public async Task<ActionResult> Update(Guid id, UpdateGradeDto dto)
Api\ElectronicRegisterAPI\Controllers\GradeController.cs:122: [HttpDelete("{id}")]
Api\ElectronicRegisterAPI\Controllers\GradeController.cs:123: [Authorize(Roles = "admin")]
Api\ElectronicRegisterAPI\Controllers\GradeController.cs:124: public async Task<ActionResult> Delete(Guid id)
Api\ElectronicRegisterAPI\Controllers\InstitutionIdentityController.cs:12: public sealed class InstitutionIdentityController(IInstitutionAuthManager manager, ICurrentTenant tenant) : ControllerBase
Api\ElectronicRegisterAPI\Controllers\InstitutionIdentityController.cs:14: [HttpPost("identity/login")]
Api\ElectronicRegisterAPI\Controllers\InstitutionIdentityController.cs:15: public async Task<ActionResult<IdentityTicketDto>> Login(LoginDto input)
Api\ElectronicRegisterAPI\Controllers\InstitutionIdentityController.cs:21: [HttpPost("identity/microsoft-login")]
Api\ElectronicRegisterAPI\Controllers\InstitutionIdentityController.cs:22: public async Task<ActionResult<IdentityTicketDto>> MicrosoftLogin(MicrosoftLoginDto input)
Api\ElectronicRegisterAPI\Controllers\InstitutionIdentityController.cs:28: [HttpGet("institutions"), Authorize(Policy = InstitutionTokenContract.SelectionScheme)]
Api\ElectronicRegisterAPI\Controllers\InstitutionIdentityController.cs:29: public async Task<ActionResult<IReadOnlyList<InstitutionChoiceDto>>> Institutions(CancellationToken cancellationToken) =>
Api\ElectronicRegisterAPI\Controllers\InstitutionIdentityController.cs:32: [HttpPost("select-institution"), Authorize(Policy = InstitutionTokenContract.SelectionScheme)]
Api\ElectronicRegisterAPI\Controllers\InstitutionIdentityController.cs:33: public async Task<ActionResult<InstitutionAccessDto>> Select(SelectInstitutionDto input, CancellationToken cancellationToken) =>
Api\ElectronicRegisterAPI\Controllers\InstitutionIdentityController.cs:36: [HttpGet("context"), Authorize(Policy = InstitutionTokenContract.AccessScheme)]
Api\ElectronicRegisterAPI\Controllers\InstitutionIdentityController.cs:37: public ActionResult<TenantContextDto> Context() => Ok(new TenantContextDto(tenant.Value.UserId, tenant.Value.InstitutionId,
Api\ElectronicRegisterAPI\Controllers\StudentController.cs:9: [Route("api/[controller]")]
Api\ElectronicRegisterAPI\Controllers\StudentController.cs:10: [Authorize]
Api\ElectronicRegisterAPI\Controllers\StudentController.cs:15: public StudentController(IStudentManager studentManager)
Api\ElectronicRegisterAPI\Controllers\StudentController.cs:20: [HttpGet("count")]
Api\ElectronicRegisterAPI\Controllers\StudentController.cs:21: [Authorize(Roles = "teacher,admin")]
Api\ElectronicRegisterAPI\Controllers\StudentController.cs:22: public async Task<ActionResult<int>> Count()
Api\ElectronicRegisterAPI\Controllers\StudentController.cs:27: [HttpGet]
Api\ElectronicRegisterAPI\Controllers\StudentController.cs:28: [Authorize(Roles = "teacher,admin")]
Api\ElectronicRegisterAPI\Controllers\StudentController.cs:29: public async Task<ActionResult<List<StudentDto>>> GetAll()
Api\ElectronicRegisterAPI\Controllers\StudentController.cs:35: [HttpGet("{id}")]
Api\ElectronicRegisterAPI\Controllers\StudentController.cs:36: [Authorize(Roles = "teacher,admin")]
Api\ElectronicRegisterAPI\Controllers\StudentController.cs:37: public async Task<ActionResult<StudentDto>> GetById(Guid id)
Api\ElectronicRegisterAPI\Controllers\StudentController.cs:43: [HttpGet("bylastname/{lastName}")]
Api\ElectronicRegisterAPI\Controllers\StudentController.cs:44: [Authorize(Roles = "teacher,admin")]
Api\ElectronicRegisterAPI\Controllers\StudentController.cs:45: public async Task<ActionResult<List<StudentDto>>> GetStudentsByName(string lastName)
Api\ElectronicRegisterAPI\Controllers\StudentController.cs:51: [HttpPut("update/{id}")]
Api\ElectronicRegisterAPI\Controllers\StudentController.cs:52: [Authorize(Roles = "admin")]
Api\ElectronicRegisterAPI\Controllers\StudentController.cs:53: public async Task<ActionResult> Update(Guid id, UpdateStudentDto dto)
Api\ElectronicRegisterAPI\Controllers\StudentController.cs:59: [HttpPost]
Api\ElectronicRegisterAPI\Controllers\StudentController.cs:60: [Authorize(Roles = "admin")]
Api\ElectronicRegisterAPI\Controllers\StudentController.cs:61: public async Task<ActionResult> Add(CreateStudentDto dto)
Api\ElectronicRegisterAPI\Controllers\StudentController.cs:67: [HttpDelete("{id}")]
Api\ElectronicRegisterAPI\Controllers\StudentController.cs:68: [Authorize(Roles = "admin")]
Api\ElectronicRegisterAPI\Controllers\StudentController.cs:69: public async Task<ActionResult> Delete(Guid id)
Api\ElectronicRegisterAPI\Controllers\SubjectController.cs:9: [Route("api/[controller]")]
Api\ElectronicRegisterAPI\Controllers\SubjectController.cs:10: [Authorize]
Api\ElectronicRegisterAPI\Controllers\SubjectController.cs:15: public SubjectController(ISubjectManager subjectManager)
Api\ElectronicRegisterAPI\Controllers\SubjectController.cs:20: [HttpGet("count")]
Api\ElectronicRegisterAPI\Controllers\SubjectController.cs:21: [Authorize(Roles = "teacher,admin,student")]
Api\ElectronicRegisterAPI\Controllers\SubjectController.cs:22: public async Task<ActionResult<int>> Count()
Api\ElectronicRegisterAPI\Controllers\SubjectController.cs:27: [HttpGet]
Api\ElectronicRegisterAPI\Controllers\SubjectController.cs:28: [Authorize(Roles = "teacher,admin,student")]
Api\ElectronicRegisterAPI\Controllers\SubjectController.cs:29: public async Task<ActionResult<List<SubjectDto>>> GetAll()
Api\ElectronicRegisterAPI\Controllers\SubjectController.cs:35: [HttpGet("{id}")]
Api\ElectronicRegisterAPI\Controllers\SubjectController.cs:36: [Authorize(Roles = "teacher,admin,student")]
Api\ElectronicRegisterAPI\Controllers\SubjectController.cs:37: public async Task<ActionResult<SubjectDto>> GetById(Guid id)
Api\ElectronicRegisterAPI\Controllers\SubjectController.cs:43: [HttpGet("byname/{name}")]
Api\ElectronicRegisterAPI\Controllers\SubjectController.cs:44: [Authorize(Roles = "teacher,admin,student")]
Api\ElectronicRegisterAPI\Controllers\SubjectController.cs:45: public async Task<ActionResult<SubjectDto>> GetSubjectByName(string name)
Api\ElectronicRegisterAPI\Controllers\SubjectController.cs:51: [HttpGet("byteacher/{id}")]
Api\ElectronicRegisterAPI\Controllers\SubjectController.cs:52: [Authorize(Roles = "teacher,admin,student")]
Api\ElectronicRegisterAPI\Controllers\SubjectController.cs:53: public async Task<ActionResult<List<SubjectDto>>> GetSubjectByTeacherId(Guid id)
Api\ElectronicRegisterAPI\Controllers\SubjectController.cs:59: [HttpPut("update/{id}")]
Api\ElectronicRegisterAPI\Controllers\SubjectController.cs:60: [Authorize(Roles = "admin")]
Api\ElectronicRegisterAPI\Controllers\SubjectController.cs:61: public async Task<ActionResult> Update(Guid id, UpdateSubjectDto dto)
Api\ElectronicRegisterAPI\Controllers\SubjectController.cs:67: [HttpPost]
Api\ElectronicRegisterAPI\Controllers\SubjectController.cs:68: [Authorize(Roles = "admin")]
Api\ElectronicRegisterAPI\Controllers\SubjectController.cs:69: public async Task<ActionResult> Add(CreateSubjectDto dto)
Api\ElectronicRegisterAPI\Controllers\SubjectController.cs:75: [HttpDelete("{id}")]
Api\ElectronicRegisterAPI\Controllers\SubjectController.cs:76: [Authorize(Roles = "admin")]
Api\ElectronicRegisterAPI\Controllers\SubjectController.cs:77: public async Task<ActionResult> Delete(Guid id)
Api\ElectronicRegisterAPI\Controllers\TeacherController.cs:9: [Route("api/[controller]")]
Api\ElectronicRegisterAPI\Controllers\TeacherController.cs:10: [Authorize]
Api\ElectronicRegisterAPI\Controllers\TeacherController.cs:15: public TeacherController(ITeacherManager teacherManager)
Api\ElectronicRegisterAPI\Controllers\TeacherController.cs:20: [HttpGet("count")]
Api\ElectronicRegisterAPI\Controllers\TeacherController.cs:21: [Authorize(Roles = "teacher,admin")]
Api\ElectronicRegisterAPI\Controllers\TeacherController.cs:22: public async Task<ActionResult<int>> Count()
Api\ElectronicRegisterAPI\Controllers\TeacherController.cs:27: [HttpGet]
Api\ElectronicRegisterAPI\Controllers\TeacherController.cs:28: [Authorize(Roles = "teacher,admin,student")]
Api\ElectronicRegisterAPI\Controllers\TeacherController.cs:29: public async Task<ActionResult<List<TeacherDto>>> GetAll()
Api\ElectronicRegisterAPI\Controllers\TeacherController.cs:35: [HttpGet("{id}")]
Api\ElectronicRegisterAPI\Controllers\TeacherController.cs:36: [Authorize(Roles = "teacher,admin,student")]
Api\ElectronicRegisterAPI\Controllers\TeacherController.cs:37: public async Task<ActionResult<TeacherDto>> GetById(Guid id)
Api\ElectronicRegisterAPI\Controllers\TeacherController.cs:43: [HttpPut("update/{id}")]
Api\ElectronicRegisterAPI\Controllers\TeacherController.cs:44: [Authorize(Roles = "admin")]
Api\ElectronicRegisterAPI\Controllers\TeacherController.cs:45: public async Task<ActionResult> Update(Guid id, UpdateTeacherDto dto)
Api\ElectronicRegisterAPI\Controllers\TeacherController.cs:51: [HttpPost]
Api\ElectronicRegisterAPI\Controllers\TeacherController.cs:52: [Authorize(Roles = "admin")]
Api\ElectronicRegisterAPI\Controllers\TeacherController.cs:53: public async Task<ActionResult> Add(CreateTeacherDto dto)
Api\ElectronicRegisterAPI\Controllers\TeacherController.cs:59: [HttpDelete("{id}")]
Api\ElectronicRegisterAPI\Controllers\TeacherController.cs:60: [Authorize(Roles = "admin")]
Api\ElectronicRegisterAPI\Controllers\TeacherController.cs:61: public async Task<ActionResult> Delete(Guid id)
Api\ElectronicRegisterAPI\Controllers\UserController.cs:9: [Route("api/[controller]")]
Api\ElectronicRegisterAPI\Controllers\UserController.cs:10: [Authorize]
Api\ElectronicRegisterAPI\Controllers\UserController.cs:15: public UsersController(IUserManager userManager)
Api\ElectronicRegisterAPI\Controllers\UserController.cs:20: [HttpGet("count")]
Api\ElectronicRegisterAPI\Controllers\UserController.cs:21: [Authorize(Roles = "admin")]
Api\ElectronicRegisterAPI\Controllers\UserController.cs:22: public async Task<ActionResult<int>> Count()
Api\ElectronicRegisterAPI\Controllers\UserController.cs:27: [HttpGet]
Api\ElectronicRegisterAPI\Controllers\UserController.cs:28: [Authorize(Roles = "admin")]
Api\ElectronicRegisterAPI\Controllers\UserController.cs:29: public async Task<ActionResult<List<UserDto>>> GetAll()
Api\ElectronicRegisterAPI\Controllers\UserController.cs:35: [HttpGet("{id}")]
Api\ElectronicRegisterAPI\Controllers\UserController.cs:36: [Authorize(Roles = "admin")]
Api\ElectronicRegisterAPI\Controllers\UserController.cs:37: public async Task<ActionResult<UserDto>> GetById(Guid id)
Api\ElectronicRegisterAPI\Controllers\UserController.cs:43: [HttpPut("update/{id}")]
Api\ElectronicRegisterAPI\Controllers\UserController.cs:44: [Authorize(Roles = "admin")]
Api\ElectronicRegisterAPI\Controllers\UserController.cs:45: public async Task<ActionResult> Update(Guid id, UpdateUserDto dto)
Api\ElectronicRegisterAPI\Controllers\UserController.cs:51: [HttpPut("updatepassword/{id}")]
Api\ElectronicRegisterAPI\Controllers\UserController.cs:52: [Authorize(Roles = "student,teacher,admin")]
Api\ElectronicRegisterAPI\Controllers\UserController.cs:53: public async Task<ActionResult> UpdatePassword(Guid id, UpdatePasswordDto dto)
Api\ElectronicRegisterAPI\Controllers\UserController.cs:59: [HttpPost]
Api\ElectronicRegisterAPI\Controllers\UserController.cs:60: [Authorize(Roles = "admin")]
Api\ElectronicRegisterAPI\Controllers\UserController.cs:61: public async Task<ActionResult> Add(CreateUserDto dto)
Api\ElectronicRegisterAPI\Controllers\UserController.cs:67: [HttpDelete("{id}")]
Api\ElectronicRegisterAPI\Controllers\UserController.cs:68: [Authorize(Roles = "admin")]
Api\ElectronicRegisterAPI\Controllers\UserController.cs:69: public async Task<ActionResult> Delete(Guid id)
```

## DTO che contengono identificativi

```text
Domain\ElectronicRegisterAPI.Domain\DTOs\AnswerInputDto.cs:5: public Guid QuestionId { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\AnswerInputDto.cs:7: public List<Guid> SelectedOptionIds { get; set; } = new List<Guid>();
Domain\ElectronicRegisterAPI.Domain\DTOs\AssignedExamDto.cs:7: public Guid Id { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\AssignedExamDto.cs:8: public Guid ClassSubjectId { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\BienniumDto.cs:4: public Guid Id { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\ClaimsContext.cs:5: public record ClaimsContext(Guid UserId, UserRole Role, Guid? StudentId, Guid? TeacherId);
Domain\ElectronicRegisterAPI.Domain\DTOs\CreateExamDto.cs:5: public Guid ClassSubjectId { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\CreateExamDto.cs:6: public Guid? RetakeOfExamId { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\CreateGradeDto.cs:5: public Guid StudentId { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\CreateGradeDto.cs:6: public Guid SubjectId { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\CreateUserDto.cs:8: public Guid? StudentId { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\CreateUserDto.cs:9: public Guid? TeacherId { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\ExamAssignedStudentDto.cs:5: public Guid StudentId { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\ExamDetailDto.cs:7: public Guid Id { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\ExamDetailDto.cs:8: public Guid ClassSubjectId { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\ExamDetailDto.cs:9: public Guid? RetakeOfExamId { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\ExamDetailDto.cs:23: public bool IsRetake => RetakeOfExamId.HasValue;
Domain\ElectronicRegisterAPI.Domain\DTOs\ExamForStudentDto.cs:5: public Guid Id { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\ExamStudentInputDto.cs:5: public Guid StudentId { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\ExamSummaryDto.cs:7: public Guid Id { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\ExamSummaryDto.cs:8: public Guid ClassSubjectId { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\ExamSummaryDto.cs:9: public Guid? RetakeOfExamId { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\ExamSummaryDto.cs:22: public bool IsRetake => RetakeOfExamId.HasValue;
Domain\ElectronicRegisterAPI.Domain\DTOs\GradeDto.cs:5: public Guid Id { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\GradeDto.cs:6: public Guid StudentId { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\GradeDto.cs:7: public Guid SubjectId { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\GradeDto.cs:9: public Guid TeacherId { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\InstitutionAuthDtos.cs:6: public sealed record InstitutionChoiceDto(Guid InstitutionId, Guid MembershipId, string Name, IReadOnlyList<string> Roles);
Domain\ElectronicRegisterAPI.Domain\DTOs\InstitutionAuthDtos.cs:9: public Guid InstitutionId { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\InstitutionAuthDtos.cs:10: public Guid MembershipId { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\InstitutionAuthDtos.cs:15: public sealed record TenantContextDto(Guid UserId, Guid InstitutionId, Guid MembershipId, IReadOnlyList<string> Roles);
Domain\ElectronicRegisterAPI.Domain\DTOs\OpenAnswerReviewInputDto.cs:5: public Guid AnswerId { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\QuestionDto.cs:7: public Guid Id { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\QuestionForStudentDto.cs:7: public Guid Id { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\QuestionOptionDto.cs:5: public Guid Id { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\QuestionOptionForStudentDto.cs:5: public Guid Id { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\StartExamDto.cs:5: public Guid SubmissionId { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\StudentDto.cs:5: public Guid Id { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\StudyAreaDto.cs:5: public Guid Id { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\StudyPathDto.cs:5: public Guid Id { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\SubjectDto.cs:5: public Guid Id { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\SubmissionAnswerOptionReviewDto.cs:5: public Guid Id { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\SubmissionAnswerReviewDto.cs:7: public Guid AnswerId { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\SubmissionAnswerReviewDto.cs:8: public Guid QuestionId { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\SubmissionResultDto.cs:7: public Guid SubmissionId { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\SubmissionResultDto.cs:8: public Guid ExamId { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\SubmissionReviewDto.cs:7: public Guid Id { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\SubmissionReviewDto.cs:8: public Guid ExamId { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\SubmissionReviewDto.cs:10: public Guid StudentId { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\SubmissionReviewDto.cs:27: public Guid? GradeId { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\SubmissionReviewDto.cs:29: public Guid? ReviewedByUserId { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\SubmissionSummaryDto.cs:7: public Guid Id { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\SubmissionSummaryDto.cs:8: public Guid ExamId { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\SubmissionSummaryDto.cs:9: public Guid StudentId { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\SubmissionSummaryDto.cs:21: public Guid? GradeId { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\TeacherDto.cs:5: public Guid Id { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\UpdateExamDto.cs:5: public Guid ClassSubjectId { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\UpdateExamDto.cs:6: public Guid? RetakeOfExamId { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\UpdateGradeDto.cs:5: public Guid SubjectId { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\UserDto.cs:5: public Guid Id { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\UserDto.cs:8: public Guid? StudentId { get; set; }
Domain\ElectronicRegisterAPI.Domain\DTOs\UserDto.cs:11: public Guid? TeacherId { get; set; }
```

## Raw SQL, background, Grade, Audit, caller legacy

```text
Api\ElectronicRegisterAPI\BackgroundServices\ExpiredExamSubmissionWorker.cs:12: namespace ElectronicRegisterAPI.Api.BackgroundServices
Api\ElectronicRegisterAPI\BackgroundServices\ExpiredExamSubmissionWorker.cs:14: internal class ExpiredExamSubmissionWorker : BackgroundService
Api\ElectronicRegisterAPI\Controllers\ApiControllerBase.cs:12: protected ClaimsContext CurrentCaller()
Api\ElectronicRegisterAPI\Controllers\ApiControllerBase.cs:25: return new ClaimsContext(userId, role, studentId, teacherId);
Api\ElectronicRegisterAPI\DependencyInjection\ServiceCollectionExtensions.cs:16: services.AddHostedService<ElectronicRegisterAPI.Api.BackgroundServices.ExpiredExamSubmissionWorker>();
Api\ElectronicRegisterAPI\Security\LegacyEndpointGate.cs:36: !await legacy.IsTokenAllowedAsync(new ClaimsContext(userId, role, student, teacher), http.RequestAborted))
Application\ElectronicRegisterAPI.Application\Managers\AuthManager.cs:136: public async Task<UserDto?> GetCurrentUserAsync(ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\ExamManager.cs:57: public async Task<List<ExamSummaryDto>> GetAllAsync(ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\ExamManager.cs:76: public async Task<ExamDetailDto?> GetByIdAsync(Guid id, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\ExamManager.cs:117: public async Task<List<AssignedExamDto>> GetAssignedAsync(ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\ExamManager.cs:140: public async Task<Guid> AddAsync(CreateExamDto dto, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\ExamManager.cs:209: public async Task UpdateAsync(Guid id, UpdateExamDto dto, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\ExamManager.cs:277: public async Task UpdateStudentsAsync(Guid id, UpdateExamStudentsDto dto, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\ExamManager.cs:328: public async Task PublishAsync(Guid id, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\ExamManager.cs:366: public async Task CloseAsync(Guid id, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\ExamManager.cs:399: public async Task CancelAsync(Guid id, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\ExamManager.cs:432: public async Task DeleteAsync(Guid id, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\ExamManager.cs:462: public async Task<StartExamDto> StartAsync(Guid id, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\ExamManager.cs:511: public async Task<SaveAnswersResponseDto> SaveAnswersAsync(Guid id, SaveExamAnswersDto dto, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\ExamManager.cs:544: public async Task<SubmissionResultDto> SubmitAsync(Guid id, SubmitExamDto dto, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\ExamManager.cs:570: public async Task<SubmissionResultDto> GetResultAsync(Guid id, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\ExamManager.cs:627: private Task WriteLifecycleAuditAsync(Exam exam, ClaimsContext caller, string action, string beforeJson) =>
Application\ElectronicRegisterAPI.Application\Managers\ExamManager.cs:628: _auditRepository.AddAsync(new AuditEvent
Application\ElectronicRegisterAPI.Application\Managers\ExamSubmissionManager.cs:47: public async Task<List<SubmissionSummaryDto>> GetByExamAsync(Guid examId, SubmissionStatus? status, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\ExamSubmissionManager.cs:99: public async Task<SubmissionReviewDto> GetByIdAsync(Guid id, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\ExamSubmissionManager.cs:229: public async Task ReviewAsync(Guid id, ReviewOpenAnswersDto dto, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\ExamSubmissionManager.cs:309: public async Task FinalizeAsync(Guid id, FinalizeSubmissionDto dto, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\ExamSubmissionManager.cs:408: var grade = new Grade
Application\ElectronicRegisterAPI.Application\Managers\GradeManager.cs:34: public async Task<int> CountAsync(ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\GradeManager.cs:44: public async Task<List<GradeDto>> GetAllAsync(ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\GradeManager.cs:61: public async Task<GradeDto?> GetByIdAsync(Guid id, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\GradeManager.cs:99: ClaimsContext caller
Application\ElectronicRegisterAPI.Application\Managers\GradeManager.cs:113: return new GradePageDto { Items = items, TotalCount = totalCount };
Application\ElectronicRegisterAPI.Application\Managers\GradeManager.cs:116: public async Task<GradeStatisticsDto?> GetStatisticsAsync(ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\GradeManager.cs:125: return new GradeStatisticsDto {
Application\ElectronicRegisterAPI.Application\Managers\GradeManager.cs:131: public async Task<GradeFiltersDto> GetFiltersAsync(ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\GradeManager.cs:170: return new GradeFiltersDto
Application\ElectronicRegisterAPI.Application\Managers\GradeManager.cs:208: public async Task<List<GradeDto>?>GetGradesBySubjectNameAsync(string subjectName, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\GradeManager.cs:229: public async Task<List<GradeDto>>GetGradesByDateAsync(DateOnly date, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\GradeManager.cs:250: public async Task<Guid?> AddAsync(CreateGradeDto dto, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\GradeManager.cs:289: var grade = new Grade
Application\ElectronicRegisterAPI.Application\Managers\GradeManager.cs:303: public async Task<bool> UpdateAsync(Guid id, UpdateGradeDto dto, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\StudentManager.cs:19: public async Task<int> CountAsync(ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\StudentManager.cs:24: public async Task<List<StudentDto>> GetAllAsync(ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\StudentManager.cs:30: public async Task<StudentDto?> GetByIdAsync(Guid id, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\StudentManager.cs:37: public async Task<List<StudentDto>> GetStudentsByLastNameAsync(string lastName, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\StudentManager.cs:72: private static Guid? TeacherRestriction(ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\SubjectManager.cs:29: public async Task<int> CountAsync(ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\SubjectManager.cs:38: public async Task<List<SubjectDto>> GetAllAsync(ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\SubjectManager.cs:44: public async Task<SubjectDto?> GetByIdAsync(Guid id, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\SubjectManager.cs:56: public async Task<SubjectDto?> GetSubjectByNameAsync(string name, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\SubjectManager.cs:75: public async Task<List<SubjectDto>>GetSubjectsByTeacherIdAsync(Guid teacherId, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\SubjectManager.cs:147: private async Task<List<Subject>> GetVisibleSubjectsAsync(ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Managers\UserManager.cs:52: public async Task<bool> UpdatePasswordAsync(Guid id, UpdatePasswordDto dto, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Mappers\ExamSubmissionMapper.cs:21: public static SubmissionResultDto ToSubmissionResultDto(ExamSubmission s, decimal? officialGradeValue, ClaimsContext? caller)
Application\ElectronicRegisterAPI.Application\Mappers\GradeMapper.cs:28: result.Add(new GradeDto
Application\ElectronicRegisterAPI.Application\Mappers\SubmissionVisibility.cs:11: public static SubmissionResultDto Apply(SubmissionResultDto dto, ClaimsContext? caller)
Application\ElectronicRegisterAPI.Application\Mappers\SubmissionVisibility.cs:25: public static SubmissionReviewDto Apply(SubmissionReviewDto dto, ClaimsContext caller)
Application\ElectronicRegisterAPI.Application\Services\ExamSubmissionProcessor.cs:41: public async Task<SubmissionResultDto> ProcessSubmitAsync(Guid submissionId, SubmitExamDto? dto, SubmitReason reason, ClaimsContext? caller)
Application\ElectronicRegisterAPI.Application\Services\ExamSubmissionProcessor.cs:173: var grade = new Grade
Business\ElectronicRegisterAPI.Business\Services\UserService.cs:63: public void EnsureCallerCanChangePassword(ClaimsContext caller, Guid targetUserId)
Domain\ElectronicRegisterAPI.Domain\DTOs\ClaimsContext.cs:5: public record ClaimsContext(Guid UserId, UserRole Role, Guid? StudentId, Guid? TeacherId);
Domain\ElectronicRegisterAPI.Domain\Interfaces\Managers\IAuthManager.cs:15: Task<UserDto?> GetCurrentUserAsync(ClaimsContext caller);
Domain\ElectronicRegisterAPI.Domain\Interfaces\Managers\IExamManager.cs:10: Task<List<ExamSummaryDto>> GetAllAsync(ClaimsContext caller);
Domain\ElectronicRegisterAPI.Domain\Interfaces\Managers\IExamManager.cs:11: Task<ExamDetailDto?> GetByIdAsync(Guid id, ClaimsContext caller);
Domain\ElectronicRegisterAPI.Domain\Interfaces\Managers\IExamManager.cs:12: Task<List<AssignedExamDto>> GetAssignedAsync(ClaimsContext caller);
Domain\ElectronicRegisterAPI.Domain\Interfaces\Managers\IExamManager.cs:13: Task<Guid> AddAsync(CreateExamDto dto, ClaimsContext caller);
Domain\ElectronicRegisterAPI.Domain\Interfaces\Managers\IExamManager.cs:14: Task UpdateAsync(Guid id, UpdateExamDto dto, ClaimsContext caller);
Domain\ElectronicRegisterAPI.Domain\Interfaces\Managers\IExamManager.cs:15: Task UpdateStudentsAsync(Guid id, UpdateExamStudentsDto dto, ClaimsContext caller);
Domain\ElectronicRegisterAPI.Domain\Interfaces\Managers\IExamManager.cs:16: Task PublishAsync(Guid id, ClaimsContext caller);
Domain\ElectronicRegisterAPI.Domain\Interfaces\Managers\IExamManager.cs:17: Task CloseAsync(Guid id, ClaimsContext caller);
Domain\ElectronicRegisterAPI.Domain\Interfaces\Managers\IExamManager.cs:18: Task CancelAsync(Guid id, ClaimsContext caller);
Domain\ElectronicRegisterAPI.Domain\Interfaces\Managers\IExamManager.cs:19: Task DeleteAsync(Guid id, ClaimsContext caller);
Domain\ElectronicRegisterAPI.Domain\Interfaces\Managers\IExamManager.cs:20: Task<StartExamDto> StartAsync(Guid id, ClaimsContext caller);
Domain\ElectronicRegisterAPI.Domain\Interfaces\Managers\IExamManager.cs:21: Task<SaveAnswersResponseDto> SaveAnswersAsync(Guid id, SaveExamAnswersDto dto, ClaimsContext caller);
Domain\ElectronicRegisterAPI.Domain\Interfaces\Managers\IExamManager.cs:22: Task<SubmissionResultDto> SubmitAsync(Guid id, SubmitExamDto dto, ClaimsContext caller);
Domain\ElectronicRegisterAPI.Domain\Interfaces\Managers\IExamManager.cs:23: Task<SubmissionResultDto> GetResultAsync(Guid id, ClaimsContext caller);
Domain\ElectronicRegisterAPI.Domain\Interfaces\Managers\IExamSubmissionManager.cs:11: Task<List<SubmissionSummaryDto>> GetByExamAsync(Guid examId, SubmissionStatus? status, ClaimsContext caller);
Domain\ElectronicRegisterAPI.Domain\Interfaces\Managers\IExamSubmissionManager.cs:12: Task<SubmissionReviewDto> GetByIdAsync(Guid id, ClaimsContext caller);
Domain\ElectronicRegisterAPI.Domain\Interfaces\Managers\IExamSubmissionManager.cs:13: Task ReviewAsync(Guid id, ReviewOpenAnswersDto dto, ClaimsContext caller);
Domain\ElectronicRegisterAPI.Domain\Interfaces\Managers\IExamSubmissionManager.cs:14: Task FinalizeAsync(Guid id, FinalizeSubmissionDto dto, ClaimsContext caller);
Domain\ElectronicRegisterAPI.Domain\Interfaces\Managers\IGradeManager.cs:10: Task<int> CountAsync(ClaimsContext caller);
Domain\ElectronicRegisterAPI.Domain\Interfaces\Managers\IGradeManager.cs:11: Task<List<GradeDto>> GetAllAsync(ClaimsContext caller);
Domain\ElectronicRegisterAPI.Domain\Interfaces\Managers\IGradeManager.cs:12: Task<GradeDto?> GetByIdAsync(Guid id, ClaimsContext caller);
Domain\ElectronicRegisterAPI.Domain\Interfaces\Managers\IGradeManager.cs:13: Task<GradePageDto> GetPagedAsync(int pageNumber, int pageSize, Guid? subjectId, Guid? studentId, DateOnly? date, ClaimsContext caller);
Domain\ElectronicRegisterAPI.Domain\Interfaces\Managers\IGradeManager.cs:14: Task<GradeStatisticsDto?> GetStatisticsAsync(ClaimsContext caller);
Domain\ElectronicRegisterAPI.Domain\Interfaces\Managers\IGradeManager.cs:15: Task<GradeFiltersDto> GetFiltersAsync(ClaimsContext caller);
Domain\ElectronicRegisterAPI.Domain\Interfaces\Managers\IGradeManager.cs:17: Task<List<GradeDto>?> GetGradesBySubjectNameAsync(string subjectName, ClaimsContext caller);
Domain\ElectronicRegisterAPI.Domain\Interfaces\Managers\IGradeManager.cs:18: Task<List<GradeDto>> GetGradesByDateAsync(DateOnly date, ClaimsContext caller);
Domain\ElectronicRegisterAPI.Domain\Interfaces\Managers\IGradeManager.cs:19: Task<Guid?> AddAsync(CreateGradeDto dto, ClaimsContext caller);
Domain\ElectronicRegisterAPI.Domain\Interfaces\Managers\IGradeManager.cs:20: Task<bool> UpdateAsync(Guid id, UpdateGradeDto dto, ClaimsContext caller);
Domain\ElectronicRegisterAPI.Domain\Interfaces\Managers\IStudentManager.cs:10: Task<int> CountAsync(ClaimsContext caller);
Domain\ElectronicRegisterAPI.Domain\Interfaces\Managers\IStudentManager.cs:11: Task<List<StudentDto>> GetAllAsync(ClaimsContext caller);
Domain\ElectronicRegisterAPI.Domain\Interfaces\Managers\IStudentManager.cs:12: Task<StudentDto?> GetByIdAsync(Guid id, ClaimsContext caller);
Domain\ElectronicRegisterAPI.Domain\Interfaces\Managers\IStudentManager.cs:13: Task<List<StudentDto>> GetStudentsByLastNameAsync(string lastName, ClaimsContext caller);
Domain\ElectronicRegisterAPI.Domain\Interfaces\Managers\ISubjectManager.cs:7: Task<int> CountAsync(ClaimsContext caller);
Domain\ElectronicRegisterAPI.Domain\Interfaces\Managers\ISubjectManager.cs:8: Task<List<SubjectDto>> GetAllAsync(ClaimsContext caller);
Domain\ElectronicRegisterAPI.Domain\Interfaces\Managers\ISubjectManager.cs:9: Task<SubjectDto?> GetByIdAsync(Guid id, ClaimsContext caller);
Domain\ElectronicRegisterAPI.Domain\Interfaces\Managers\ISubjectManager.cs:10: Task<SubjectDto?> GetSubjectByNameAsync(string name, ClaimsContext caller);
Domain\ElectronicRegisterAPI.Domain\Interfaces\Managers\ISubjectManager.cs:11: Task<List<SubjectDto>> GetSubjectsByTeacherIdAsync(Guid teacherId, ClaimsContext caller);
Domain\ElectronicRegisterAPI.Domain\Interfaces\Managers\IUserManager.cs:10: Task<bool> UpdatePasswordAsync(Guid id, UpdatePasswordDto dto, ClaimsContext caller);
Domain\ElectronicRegisterAPI.Domain\Interfaces\Security\IInstitutionTokens.cs:19: Task<bool> IsTokenAllowedAsync(ClaimsContext caller, CancellationToken cancellationToken = default);
Domain\ElectronicRegisterAPI.Domain\Interfaces\Services\IExamSubmissionProcessor.cs:9: Task<SubmissionResultDto> ProcessSubmitAsync(Guid submissionId, SubmitExamDto? dto, SubmitReason reason, ClaimsContext? caller);
Domain\ElectronicRegisterAPI.Domain\Interfaces\Services\IUserService.cs:15: void EnsureCallerCanChangePassword(ClaimsContext caller, Guid targetUserId);
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\ExamRepository.cs:49: var entity = await _context.Exams.FromSqlRaw("SELECT * FROM exam WHERE id = {0} FOR UPDATE", id)
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\ExamSubmissionRepository.cs:50: // Use FromSqlRaw with FOR UPDATE to acquire row lock within the existing transaction
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\ExamSubmissionRepository.cs:63: var lockedEntityQuery = _context.ExamSubmissions.FromSqlRaw(sql, id).AsTracking();
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\ExamSubmissionRepository.cs:205: var lockedQuery = _context.ExamSubmissions.FromSqlRaw(sql, submissionId).AsTracking();
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\GradeRepository.cs:96: .Select(g => new Grade
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\GradeRepository.cs:134: return new GradeStatistics
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\GradeRepository.cs:150: var gradeEntity = new GradeEntity
Infrastructure\ElectronicRegisterAPI.Infrastructure\Repositories\GradeRepository.cs:188: return new Grade
Infrastructure\ElectronicRegisterAPI.Infrastructure\Security\LegacyAccess.cs:14: public Task<bool> IsTokenAllowedAsync(ClaimsContext caller, CancellationToken cancellationToken = default) => AllowedAsync(caller.UserId, caller, cancellationToken);
Infrastructure\ElectronicRegisterAPI.Infrastructure\Security\LegacyAccess.cs:16: private async Task<bool> AllowedAsync(Guid userId, ClaimsContext? token, CancellationToken cancellationToken)
```

## Percorsi indiretti e processi interni

Student -> Class -> ClassSubject -> Teacher/Subject; Grade -> Student/ClassSubject; Exam -> ClassSubject/Retake; Submission -> Exam/Student/Grade; Answer -> Submission/Question -> Option. Ogni join richiede filtro SQL tenant e FK coerente, non filtro in memoria.

Worker e processor sono processi interni privilegiati ancora single-tenant: batch globale 50, scope per elemento, nessun SuperAdmin. AuditRepository non espone letture HTTP: i JSON restano dati didattici da attribuire. Grade ordinari da GradeManager, ufficiali da processor/finalize; Audit append nella medesima transazione. Non modificati.

Classificazione: Institution/Campus/Membership/ruoli sono strutture MT1; Auth nuovo globale + membership verificata MT2; repository e manager didattici DA MIGRARE e tutte le 73 route legacy BLOCCATE ai nuovi token; StudyArea/StudyPath NON RAGGIUNGIBILI HTTP; user identita GLOBALE intenzionale ma CRUD account non globale nel modello futuro. A parziale e B preparatorio, C/D/E non migrati.
