# ElectronicRegister - MT-3D

Migrazione HTTP istituzionale del modulo Exam/Submission/Grade, ClassSubject dropdown, selezione studenti e continuita delle prove. **MT-3D completata nel perimetro HTTP: due suite 540/540 Passed. Rollout operativo chiuso, nessun cutover, MT-3E non iniziata.** Scansione e dispatch automatico istituzionale offline restano bloccati fino a MT-3E. La piattaforma nel suo complesso non e dichiarata pronta per produzione.

## 1. Stato iniziale e documenti

Branch `refactor-database-entities`, HEAD `01702b57a19fc19079c1b7c005789ef69aa394a2`. Workspace gia contenente modifiche MT3C non committate, relativi nuovi controller/test/evidenze, modifiche architetturali e cartella frontend `.vscode/` non tracciata. Preservati; nessun reset, checkout, pull, commit o push. Il report esami storico appariva gia modificato senza diff di contenuto dopo la normalizzazione whitespace MT3C. Nessun file frontend toccato.

Letti i report MT3C/MT3B/MT3A/MT2, FINAL_BACKEND_AUDIT e i documenti Design, Isolation Matrix e Migration; verificati controller, manager, processor, servizi di grading, repository, entity Question/Option/Violation, audit, worker, ClaimsContext/TenantContext, policy, gate e handler effettivi. Le informazioni del workspace prevalgono sui report storici.

## 2. Preflight SQL e baseline

Connessione esclusiva a loopback **electronic_register_test**, XAMPP **MariaDB 10.4.32**. Non si assume equivalenza automatica con MySQL 5.7. Presenti **27 FK composite, 40 trigger MT3A**, sola holding `MT3A_LEGACY_TEST` in **provisioning**. Nessun tenant operativo attivato e nessuna membership permanente creata. Il tool Mt3cTestPreparation non e stato rieseguito. Nessun nuovo DDL, indice, migrazione o servizio esterno necessario.

Build iniziale riuscita. Baseline verificata: **48 unit + 434 integration = 482/482 Passed, zero Failed/Skipped**. TRX separati in [baseline](TestResults/mt3d/baseline/). Il prerequisito strict e stato soddisfatto **senza cambiare GLOBAL sql_mode**: opt-in esplicito `ER_TEST_STRICT_SESSION=1`, DbConnectionInterceptor TestDatabaseSession e setup del solo OpenAsync del tooling MT3A. Verifica loopback/nome DB prima del SET SESSION, configurazione ad ogni apertura/reset del pool. Non e un cambio globale o persistente e non riutilizza l'autorizzazione temporanea MT3C.

Il valore globale originale resta `NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION`. I test verificano contemporaneamente SESSION strict e GLOBAL invariato. Per il client backup sono mantenuti gli opt-in MT3C `MYSQLDUMP_EXE` XAMPP e `MYSQLDUMP_FLAVOR=MariaDB`. Password/connection string non sono riportate nei documenti/evidenze.

Controllo finale in [final-db-state.txt](TestResults/mt3d/final-db-state.txt): stessa holding provisioning, zero membership/profili temporanei, nessuna riga scolastica fuori holding e GLOBAL invariato. Il conteggio completo e **29 FK composite**, di cui **27 `fk_mt3a_*`** e due FK membership dei profili gia preesistenti; 40 trigger. La distinzione evita di confondere il conteggio MT3A con tutte le FK composite del database.

## 3. Inventario route e SQL

Inventario runtime di **135 action** in [routes-Production.json](TestResults/mt3d/routes-Production.json) e [routes-Development.json](TestResults/mt3d/routes-Development.json): metodo, route reale, controller/action, ruoli, policy, manager, input/output e classificazione. Restano **73 legacy + 5 identita MT2 + 24 MT3C**; aggiunte **33 MT3D**. Nessun conflitto/duplicato metodo-route.

La matrice del modulo, con repository/tabelle/confini e test per ogni route, e in [route-matrix.csv](TestResults/mt3d/route-matrix.csv). Le 18 action Exam e le 12 Grade legacy restano presenti e gated. Le nuove 33 action sono anche tutte elencate, con DTO/ruoli/status, nel [contratto frontend](../../docs/api/EXAM_FRONTEND_CONTRACTS.md).

| Famiglia nuova | Action | Manager pubblico | Persistenza |
| --- | ---: | --- | --- |
| /api/institution/class-subjects | 1 | IInstitutionExamManager | ClassSubject/Class/Subject/Teacher, solo lettura scoped |
| /api/institution/exams | 13 | IInstitutionExamManager | Exam, ExamStudent, Question/Option, ClassSubject, Student, Submission, Audit |
| /api/institution/submissions | 10 | IInstitutionExamManager | Submission, Answer, ponte opzioni, Exam/Question, Grade, Violation, Audit |
| /api/institution/grades | 9 | IInstitutionExamManager | Grade, Student, ClassSubject/Subject, riferimento Submission |
| /api/institution/students?classId= | action MT3C esistente estesa | IInstitutionStudentManager | Class/Student/ClassSubject filtrati SQL |

Raw SQL istituzionale: `InstitutionExamDataScope.LockExam` e `LockSubmission`, query interpolate parametrizzate per ID+InstitutionId (anche StudentId per operazioni personali), entrambe FOR UPDATE e con transazione obbligatoria. SQL legacy preservato nei rami senza scope. Il worker usa una nuova JOIN esplicita a `mt3a_legacy_scope`, e il controllo del dispatch nullo usa la stessa holding. Nessuna scansione istituzionale globale seguita da filtro in memoria.

## 4. Autenticazione, rollout e gate

Nuovo marker `InstitutionExamEndpointAttribute`, policy `InstitutionExamAccess`, unico schema **InstitutionAccess**. Riusa il requirement MT2: firma/typ/purpose/audience/lifetime, membership di quel User e Institution Active, uguaglianza dell'intero insieme ruoli, inizializzazione dell'ICurrentTenant scoped esistente. Ogni nuova action aggiunge i ruoli necessari. Nessun token Bearer legacy o selection ticket come alternativa.

La policy richiede **sia SchoolApi:Enabled sia ExamApi:Enabled**, default-deny via TryParse. Esempio versionato entrambi false, nessun appsettings runtime cambiato. Testano tutte le 33 action con flag falso e con una sola delle due abilitazioni; JWT valido non basta.

LegacyEndpointGate resta globale e non usa prefissi URL. Il ramo migrato riconosce soltanto i marker dedicati, impone policy esatta, assenza di AllowAnonymous/schemi o policy estranei, flag appropriati, identita autenticata e contesto gia inizializzato. Due marker contemporanei negano. Le nuove action non diventano bearer legacy; le vecchie non acquisiscono policy istituzionali. L'inventario MT2 e l'audit preservano i 73 gate originali, classificando separatamente le nuove action.

## 5. Principal, autorizzazione e confine repository

I controller sono sottili e chiamano soltanto IInstitutionExamManager o il manager Student MT3B; nessun DbContext o ricostruzione di TenantContext nei controller. InstitutionExamManager parte da InstitutionSchoolAuthorization.RequireAsync e risolve i profili attraverso il resolver MT3. Il nuovo **ExamPrincipal conserva il TenantContext e l'intero set di ruoli**, con una vista di operazione personale/gestionale. Non riduce la membership a UserRole.

Gli engine ExamManager/ExamSubmissionManager/GradeManager condividono gli stessi algoritmi e ricevono questo principal. Le vecchie interfacce mantengono adapter espliciti da ClaimsContext soltanto per il percorso legacy. VisibilityCaller e un adapter per il mapper di redazione DTO preesistente; non autorizza le nuove operazioni. User.Role, User.StudentId/TeacherId non sono fallback.

InstitutionAdmin opera nel tenant senza richiedere un profilo Teacher implicito. Teacher richiede il proprio profilo e ClassSubject assegnata. Student richiede il proprio profilo e solo assegnazioni/submission personali. Teacher+Student unisce in SQL letture Grade di classi insegnate e propri voti; i risultati personali non gestibili come Teacher usano la vista Student con redazione dei dati provvisori. Admin+Teacher resta Admin del solo istituto selezionato. Nessun ruolo sommato A/B.

`IInstitutionExamRepositoryFactory` crea un insieme esplicito di repository **bound** sullo stesso DbContext/transazione. Richiede che UserId/InstitutionId/MembershipId/ruoli del principal corrispondano all'accessor verificato; senza contesto fallisce. InstitutionExamDataScope e immutabile per operazione e applica owner e visibilita in SQL. Le registrazioni repository legacy non vengono sostituite; gli engine istituzionali ricevono solo le istanze bound costruite dalla factory, inclusi GradeService, SubjectService e AuditRepository. Nessun repository globale entra in quel grafo.

Il processor istituzionale richiede principal personale e corrispondenza completa col repository bound: tenant, identita, membership, ruoli, profili e vista. Non accetta un caller nullo. Questo confine e costruito da servizi server autorizzati, non da DTO/Guid del client.

## 6. Query e ownership nelle scritture

Ogni root query istituzionale usa `EF.Property<Guid>(..., "InstitutionId")`. Exam aggiunge ClassSubject del docente, oppure assegnazione Student; Submission aggiunge Exam visibile e Student proprietario; Grade aggiunge classe assegnata/profilo personale. Metodi di ricerca, count, paging, mappe Student/Subject e verifiche dipendenze usano il medesimo scope.

Le Include di struttura/risposte restano legate al parent scoped e alle PK globalmente uniche, con coerenza di owner imposta dalle FK composite MT3A. Non sono query globali materializzate e filtrate in memoria. Le query dei Grade e delle assegnazioni usano proiezioni, AsNoTracking quando appropriato e aggregati SQL.

Prima di SaveChanges, lo scope valorizza **esplicitamente** InstitutionId per ogni nuova entry owned: Exam, ExamStudent, Question/Option, Submission, Answer, dizionario del ponte opzioni, Grade, Violation e Audit. Verifica owner originale/corrente per update/delete e rifiuta entry non owned. I test controllano INSERT con colonna/parametro owner A, non soltanto il risultato dei trigger.

La shadow property owner e ora anche **concurrency token EF**, senza cambiamenti DDL: UPDATE/DELETE tracked includono InstitutionId nel predicato. [SQL catturato](TestResults/mt3d/write-statements.sql.txt) mostra `UPDATE exam ... WHERE id=... AND institution_id=...`. I read-back generati da EF per updated_at possono usare PK e ROW_COUNT() nello stesso batch dopo l'UPDATE scoped: non sono lookup autorizzativi su GUID esterni; possono leggere soltanto la riga appena aggiornata. Non si afferma che ogni frammento del batch ripeta letteralmente il predicato owner.

Prima di collegare GradeId a Submission si verifica anche StudentId e ClassSubject dell'Exam, non soltanto il tenant/FK. Gli errori di concorrenza attesi nello scope diventano conflitti generici 409. FK/trigger MT3A invariati; nessun owner derivato dal trigger legacy nei writer istituzionali.

## 7. ClassSubject, class filter ed eleggibilita

GET class-subjects proietta ClassSubjectId, ClassId/ClassName, SubjectId/SubjectName, TeacherId/FirstName/LastName. A catalogo locale; T solo proprie assegnazioni; S 403. Filtri in SQL, nessun dropdown globale. Create/Update Exam conserva ClassSubjectId e lo valida nel medesimo scope.

GET students con classId facoltativo conserva il contratto senza filtro. Con filtro verifica classe del tenant e permesso docente, poi filtra Student.ClassId nel database. Classe B/non assegnata/inesistente -> 404; Student directory sempre vietata.

EligibleStudents deriva la classe da Exam -> ClassSubject e restituisce gli studenti della classe, compresi i gia assegnati. Nessun requisito di insufficienza per recuperi. UpdateStudents conserva controlli duplicati, classe/tenant, ExtraTimeMinutes, stato modificabile e ownership; nessuna mutazione dopo rifiuto.

## 8. Exam, AssignedExam e recuperi

Lista/dettaglio/CRUD, assegnazione, publish/close/cancel/delete, assigned e start usano gli engine esistenti con repository bound. Teacher non opera sugli Exam di ClassSubject altrui neanche entro A. A non gestisce B. Student non riceve DTO amministrativi con soluzioni.

AssignedExamDto aggiunge **SubmissionId nullable persistito**, correlato a Exam+Student nello scope. Prima di Start null/assente; dopo Start/submit/finalize stesso ID, anche dopo nuova autenticazione/dispositivo. Nessun nuovo tentativo o ID generato dalla lettura.

Un solo tentativo per Exam. Retake e un Exam distinto, stesso contesto autorizzato e regole preesistenti: Grade affiancati, originale invariato, nessuna media o ammissione basata su insufficienza. Test di retake anche dopo Grade 6/9 e puntatore B rifiutato.

## 9. Sessione, risposte e revision

GET sessione solo Student proprietario: ID/stato/date/revision, struttura studente, risposte persistite. DTO dedicato privo di IsCorrect/AwardedPoints/TeacherComment/SuggestedGrade. Nessuno shuffle a ogni lettura; posizione/ID deterministici, futuro ordine individuale persistito rinviato. Le Include sono scoped al parent e ai relativi vincoli SQL.

Resume non scrive, non crea tentativi e non modifica deadline/StartedAt. Start istituzionale restituisce timestamp gia normalizzato ai microsecondi DATETIME(6); letture DB ricostruiscono UTC senza spostare l'istante. Quindi Start e Resume coincidono anche nella precisione. Lo studente consegnato riceve stato/risposte ma non puo salvarle nuovamente.

Autosave sostituisce lo snapshot completo e conserva la possibilita di risposte parziali. ClientRevision deve superare LastClientRevision; stale o save scaduto restituisce accepted=false e revision corrente. Due device non sovrascrivono dati nuovi con uno snapshot stale. Se il submit manuale fallisce Required, la transazione rollbacka anche snapshot/revision/metadati e conserva l'ultimo autosave riuscito.

Il caricamento opzioni durante SaveAnswers e ora unico per l'insieme degli ID selezionati, eliminando il lookup ripetuto per risposta. Domande/opzioni B, duplicate o di struttura invalida sono rifiutate senza scritture; gli errori applicativi previsti sono classificati 400/404/409 anziche 500.

## 10. Required, submit e Passed

Prima della correzione tre test unit fallivano per Open whitespace, Single vuota e Multiple vuota pur con QuestionId presente: [riproduzione](TestResults/mt3d/reproductions/required-before.trx). Correzione semantica condivisa: Open non whitespace; Single/Multiple almeno un'opzione valida. Non richiede tutte le opzioni corrette e non modifica il grading. Autosave resta parziale; Required si verifica sul manuale, non TimeExpired.

Submit HTTP istituzionale chiama direttamente il ramo del processor bound che acquisisce il lock: la verifica idempotente non viene preceduta da un rifiuto InProgress che impedirebbe i retry. Dopo consegna ritorna il risultato persistito, senza applicare il nuovo body e senza duplicare Grade/audit.

Manuale prima deadline: salva/valida/submit/grading nella stessa transazione. Dopo deadline: nessuna risposta tardiva accettata. POST submit-expired richiede lo Student proprietario autenticato, **nessun body**, deadline raggiunta; usa risposte persistite, non Required, non prolunga deadline o chiude l'Exam. Chiuse vuote zero punti (Grade minimo 1 secondo formula invariata); aperte conservano PendingReview e nessun Grade prematuro. Prima deadline 409; retry dopo consegna idempotente.

Passed e distinto da Status: ufficiale Grade >=6 -> true, <6 -> false, prima ufficialita nulla/omessa per Student. Voti 5/6/9 e override coerenti verificati; closed-only sotto 6 e Graded con Passed=false. Nessun SubmissionStatus.Failed aggiunto. FailedAuto preesistente non viene trasformato in insufficienza: il servizio reale attuale produce Graded o PendingReview; mapper e unit legacy preservano assenza esito ufficiale negli altri stati.

ConvertToGrade, PassingPercent, intervallo 1..10 e regole dei punteggi invariati.

## 11. Violazioni

POST personale sulla sola submission InProgress prima deadline. EventId corrisponde alla **PK ExamViolation.Id gia esistente**: unicita globale anche fra tenant, nessuna migrazione. Type chiuso tab_hidden/window_blur/fullscreen_exit; metadata facoltativo oggetto JSON <=1024 byte UTF8/profondita 4; body <=4096 byte. OccurredAt server UTC. Il nuovo handler tratta il superamento limite trasporto come 413, riprodotto inizialmente come 500 e corretto.

Lock sulla sola submission, evento e ViolationCount atomici. Retry stesso ID/submission/contenuto -> duplicate=true senza incremento. Payload o submission discordante -> 409. Collisione con EventId B non legge il contenuto B: PK 1062 tradotta in conflitto generico e rollback del contatore, con digest A/B invariati. Due eventi concorrenti conservano entrambi gli incrementi.

Limiti server: 5 nuovi eventi/10 secondi e 128 totali per submission, superamento 409. Payload invalido, ritardo, stato consegnato, altro Student, Teacher o tenant B non generano eventi. GET eventi solo A/T autorizzati, massimo 128.

Nessuna penalizzazione, insufficienza, invalidazione o submit per violazioni: segnali del browser non sono prove certe. SubmitReason.Violations non attivato automaticamente.

## 12. Grade, review/finalize e audit

Grade list/id/count/paging/filtri/statistiche e CRUD necessari sono tenant-aware. Teacher solo assegnazioni, Student propri voti; Teacher+Student unisce questi diritti in SQL, non tra istituti. StudentId/ClassSubjectId locali e compatibili; i Grade ufficiali seguono anche Student/Exam della submission. CRUD generico non modifica/cancella Grade collegati: 409, flusso finalize obbligatorio.

Submission list/status, review, finalize/override e result riusano regole e mapper condivisi. Grade unico, review prima finalize per aperte, motivazione override e idempotenza con richiesta concordante preservati. Due attori autorizzati distinti Teacher/Admin e due richieste simultanee finalizzano una sola volta; il modello mantiene un singolo Teacher assegnato per ClassSubject, non inventa co-teaching.

Audit exam.publish/close/cancel e submission.submit/finalize/override resta nella transazione condivisa. Scope assegna owner esplicito dalla risorsa/contesto, non da ActorUserId. EntityType rimane exam/exam_submission riconosciuto dai trigger; nessuna API globale audit. In caso override resta l'evento submission.override previsto dal workflow precedente.

## 13. Transazioni e lock

Stesso ITransactionManager e DbContext per engine, repository, audit e Grade. Nessun nuovo ordine inverso Exam/Submission, lock globale o retry generalizzato.

Exam mutations/Start/Close: prima query transazionale **FOR UPDATE sul singolo Exam con tenant**; dopo acquisizione, query di permesso e struttura. La query di autorizzazione non viene anticipata nel lock con sottoquery che potrebbe fissare uno snapshot REPEATABLE READ prima di una attesa. Close controlla SQL InProgress dopo il parent lock; Start precedenti hanno committato, successivi attendono. Close non blocca indiscriminatamente le submission.

Submit/review/finalize: lock singola Submission con tenant, e StudentId nei flussi personali; poi query di autorizzazione/struttura nello stesso ordine. Nessun Exam FOR UPDATE introdotto nel processor. Violations usa lo stesso parent lock, senza invertire le dipendenze. Un ID B non acquisisce lock B perche il primo predicato include owner A.

Concorrenza HTTP verificata: Start/Start, Start/Close, Start/Cancel, autosave/autosave (revision 1/2), autosave/submit, manuale tardivo/TimeExpired, review/finalize, finalize/finalize, violazione/violazione/retry, sessioni simultanee A/B con stesso User globale. Le prove su dataset piccolo non dimostrano ogni interleaving o carico produttivo; mantengono anche i test concorrenti preesistenti.

## 14. Worker: confine esatto MT3D/MT3E

Il worker reale resta registrato, avviato e arrestato nei bootstrap; **non e migrato multi-tenant**. GetExpiredInProgressAsync legacy seleziona solo righe del manifest tecnico. Il processor legacy con caller nullo ricontrolla che la submission appartenga alla holding, impedendo dispatch diretto per A/B. Nessun null caller come bypass generale e nessun SuperAdmin.

Funzionante in MT3D: expiry HTTP dello Student proprietario, con principal verificato e processor/repository bound; risposta persistita e grading/audit atomici; invocazione controllata dello stesso processor entro quel contesto autenticato. Il worker conserva soltanto il funzionamento legacy della holding.

Ancora **bloccato fino a MT3E**: scansione/dispatch automatico degli istituti quando il browser e offline e invocazioni tecniche senza JWT, che richiederanno un contesto tecnico verificato per item/submission/tenant. Non viene fabbricato un TenantContext o un ruolo Student tecnico per aggirare questo limite. I test invocano il worker reale e il processor nullo su una submission istituzionale scaduta, dimostrano che rimane invariata, poi provano l'expiry HTTP legittimo. Non si dichiara auto-submit istituzionale end-to-end offline operativo.

## 15. Test A/B e garanzie effettive

Mt3dHttpTests usa Program.cs reale, Kestrel, JWT login/selezione reali e MariaDB. A/B temporanee Active, nomi uguali, membership/profili distinti, stesso User globale in entrambe con ruoli differenti nei casi multiruolo; classi assegnate/non assegnate, studenti, docenti, materie, Exam/Student/Submission/Question/Option/Grade creati attraverso HTTP. Un altro Student A con User.Role legacy admin prova l'assenza di fallback/impersonazione.

Per ogni action con ID risorsa: A con ID B e GUID inesistente, stesso status/ProblemDetails salvo traceId per-request, digest completi A/B invariati da nuove connessioni. Header/token negativi, revoca membership/Institution e ruoli, flag singoli/entrambi chiusi sono provati per **tutte le 33 action**, prima del binding. Assigned non divulga ID altrui. Session/answer/submit/result/violation di altro Student A 404. Le liste/count/ricerche restano scoped.

Verifiche persistite includono owner INSERT, SQL UPDATE owner, Grade/audit unici, risposte e revision conservate, timestamp/deadline, collisione EventId e rollback, Grade originali affiancati ai retake, assenza eventi/sanzioni dopo payload illeciti e assenza dispatch worker istituzionale. Nessun mock di repository per dimostrare isolamento DB.

JWT, policy HTTP, principal/manager, scope repository e integrita SQL sono livelli distinti. SQL non autentica utenti; il repository bound richiede il principal verificato costruito dal manager; il marker non sostituisce policy/contesto; test HTTP legacy dimostrano il blocco, non isolamento di repository globali non migrati.

## 16. Performance

[Misure e SQL reali](TestResults/mt3d/http-query-observations.json), incluso costo di autorizzazione/profili per richiesta. Campione locale Teacher/Student: ClassSubject 5 comandi EF; filtro Student classe 6; eligible 8; Exam listing 5; Assigned 6; Resume 6; Review 10; Grade list 8; Grade paginata 9; Result 6. I risultati definitivi nel JSON prevalgono su questo riepilogo campionario.

Il JSON distingue anche i **SET SESSION aggiuntivi solo test** tramite contatore delle aperture, che non fanno parte dei comandi EF ma sono round trip reali in queste prove. Restano handshake/version detection Pomelo preesistenti; nessuna pretesa di benchmark rete/server totale. I tempi includono il percorso HTTP locale, non sono garanzie produttive.

Predicati owner/parametri A, EXISTS/assegnazioni, count lato SQL, JOIN di parent pertinenti, AsNoTracking nelle letture e pagina massima 100 verificati. Exam listing con 1 e poi 4 righe mantiene lo stesso numero query. SaveAnswers carica le opzioni selezionate una volta invece di una query per risposta. Review/Grade richiedono varie query costanti per mappare struttura/nomi e contesto: ottimizzazioni ulteriori sono possibili, senza cache per autorizzazioni revocabili. Le liste Exam/Assigned non sono paginate e possono crescere: rischio volume aperto, non promessa di prestazioni a grande scala. Nessun Redis o indice speculativo.

## 17. Bootstrap, OpenAPI e compatibilita

Development e Production reali: ValidateScopes/ValidateOnBuild, tutti i repository/transaction manager/servizi, schemi e policy risolti, TenantContext scoped, gate, worker registrato e ApplicationStopped al termine. JWT vero legge il DB in entrambi. Nessun worker disabilitato per superare il bootstrap. Nessuna route duplicata.

OpenAPI Development include tutte le 33 nuove action con input/output; Production resta 404 secondo configurazione precedente. Il contratto frontend documenta autenticazione reale e enum numerici; l'OpenAPI non e usato come prova sostitutiva della sicurezza runtime. GlobalExceptionHandler mantiene errori pubblici generici, aggiunge il caso trasporto 413 e non rivela SQL/connection string/dati B.

Tutti i vecchi endpoint restano presenti e rifiutano scoped/selection JWT. User.Role/link globali, ClaimsContext, bridge e gate non rimossi. ExamService, SubmissionGradingService, ConvertToGrade e regole recuperi non modificati. Worker loop non modificato: il blocco minimo e nel repository/processor di dispatch. Nessuna randomizzazione, CourseEdition/StudentEnrollment, UI React o API audit globale.

## 18. File modificati

- Domain: ExamPrincipal; IInstitutionExamManager/IInstitutionExamRepositories; DTO ClassSubject/sessione/violazioni; AssignedExamDto.SubmissionId; estensione Student class filter e processor istituzionale.
- Business: GradeService e SubjectService resi pubblici per comporre le medesime regole con repository bound; algoritmi invariati.
- Application: InstitutionExamManager; adattamento dei tre engine a ExamPrincipal con adapter legacy espliciti; processor bound/idempotenza/Required; Assigned mapper e Student class filter; DI.
- Infrastructure: InstitutionExamDataScope e factory; confine opzionale esplicito nei sette repository condivisi (Exam, Submission, Grade, ClassSubject, Student, Subject, Audit); owner concurrency token nel sidecar; DbUtc; opt-in TestDatabaseSession e setup sessione tooling MT3A. Nessuna entity generata modificata.
- Api: quattro controller istituzionali, marker/policy Exam, ramo gate verificabile, flag esempio false, Student query classId, handler 413.
- Test: Mt3dHttpTests nuovo; tre riproduzioni Required in unit; inventari legacy/audit aggiornati senza indebolire i gate; helper ProgramHost aggiunge flag Exam opzionale.
- Documenti/evidenze: questo report, Design/Isolation Matrix, EXAM_FRONTEND_CONTRACTS, inventari/query/TRX/script di verifica MT3D.

## 19. Suite definitive e riproduzione

Build completa riuscita, soli warning nullable preesistenti. Baseline **48 unit + 434 integration = 482/482**. Entrambe le suite definitive: **51 unit + 489 integration = 540/540 Passed, zero Failed, zero Skipped**, senza modifiche al codice tra le due esecuzioni. Aggiunti **58 casi: 3 unit e 55 integration**. Ogni progetto/esecuzione ha un TRX distinto in [final-1](TestResults/mt3d/final-1/) e [final-2](TestResults/mt3d/final-2/).

[Verify-Evidence.ps1](TestResults/mt3d/Verify-Evidence.ps1) ha verificato realmente contatori, outcome, identita univoche dei test, conservazione di **tutti i 482 precedenti** e uguaglianza degli insiemi delle due suite finali. [verification.json](TestResults/mt3d/verification.json) contiene risultati e SHA256 dei sei TRX. La matrice runtime contiene 64 route pertinenti, incluse 30 legacy preservate, 33 nuove e il filtro Student.

Build finale incrementale: zero errori e zero warning. `git diff --check` e controlli whitespace sui nuovi sorgenti/documenti passati; nessuna modifica frontend, ExamService, SubmissionGradingService o loop del worker. Nessun commit/push eseguito.

Esecuzione riproducibile, configurando prima TEST_MYSQL_CONNECTION esplicita al solo DB di test:

```powershell
$env:ER_TEST_STRICT_SESSION = '1'
$env:MYSQLDUMP_EXE = 'C:/xampp/mysql/bin/mysqldump.exe'
$env:MYSQLDUMP_FLAVOR = 'MariaDB'
dotnet build Backend/ElectronicRegisterAPI/ElectronicRegisterAPI.slnx --no-restore
dotnet test tests/Unit/Unit.Tests.csproj --no-build --logger 'trx;LogFileName=unit.trx' --results-directory <run-directory>
dotnet test tests/Integration/Integration.Tests.csproj --no-build --logger 'trx;LogFileName=integration.trx' --results-directory <run-directory>
```

Le riproduzioni fallite sono conservate: Required semantico, precisione Start/Resume e idempotenza al primo HTTP, fixture Multiple con sole una corretta (corretta la fixture, non la regola), confronto traceId per-request (ora escluso dal confronto di contenuto), read-back EF nel batch e payload Kestrel eccessivo. Nessuna assertion di sicurezza preesistente indebolita o test disabilitato.

## 20. Rischi e prerequisiti MT-3E

Rollout operativo ancora chiuso. MT3E deve migrare scansione e dispatch worker per tenant/item con contesto tecnico esplicito, completare audit operativo, recovery, amministrazione istituzionale e verifica globale del rilascio. Non basta abilitare i due flag per garantire consegna offline. Nessuna attivazione della holding tecnica o onboarding permanente eseguito.

Rischi aperti: liste non paginate/strutture grandi, limiti payload risposte storici, eventi browser non affidabili, assenza benchmark carico/staging e restore certificato; revoca non retroattiva sulle operazioni gia autorizzate in corso. La configurazione strict resta una proprieta esplicita delle connessioni di test, non un cambiamento globale produttivo. Il tooling di preparazione MT3C non deve essere usato come reset o bootstrap su questo schema gia migrato.
