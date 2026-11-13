# ElectronicRegister - MT-3C

**MT-3C COMPLETATA nel perimetro HTTP selettivo Student/Teacher/Subject: due suite consecutive 482/482 Passed, zero Failed/Skipped, tutti i 426 casi precedenti preservati e 56 nuovi casi.** Rollout operativo chiuso: `Multitenancy:SchoolApi:Enabled` assente, falso o non valido nega accesso. Il flag viene abilitato esclusivamente dagli host di test. Nessun cutover, MT-3D non iniziata; MT-3 complessiva resta parziale fino ai checkpoint D/E. Gli snapshot dei checkpoint precedenti nei documenti architetturali restano storici.

## 1. Git e baseline reale

Branch `refactor-database-entities`, HEAD iniziale `01702b57a19fc19079c1b7c005789ef69aa394a2`. Stato preesistente: una riga vuota finale in `tests/Integration/EXAM_BUSINESS_RULES_UPDATE.md` e cartella non tracciata `Frontend/ElectronicRegisterReact/.vscode/`. Preservato il contenuto; soltanto quella riga vuota finale e stata normalizzata per soddisfare diff --check, comunicandolo all'utente. Frontend invariato. Nessun reset, checkout, pull, commit o push. [Stato iniziale](TestResults/mt3c/initial-state.txt).

Build iniziale riuscita. Il DB XAMPP effettivo e **MariaDB 10.4.32**, non il MySQL 5.7.24 dei report storici. Database esclusivo `electronic_register_test`. La prima baseline ha restituito **48 unit + 306 integration Passed, 72 Failed, zero Skipped**: mancavano strict mode e gli artefatti SQL-only MT3A. Con strict mode, **48 + 345 Passed, 33 Failed**, zero Skipped. Questi risultati sono conservati in [baseline](TestResults/mt3c/baseline/) e [baseline-strict](TestResults/mt3c/baseline-strict/); non sono presentati come suite verdi.

Dopo ripristino dei prerequisiti: **48 unit + 378 integration = 426/426 Passed, zero Failed/Skipped**, prima delle modifiche alle API. [TRX unit](TestResults/mt3c/baseline/unit.trx), [TRX integration verificato](TestResults/mt3c/baseline-ready/integration.trx). Il run intermedio [baseline-restored](TestResults/mt3c/baseline-restored/) aveva un solo errore dovuto al percorso del client backup MySQL 8 assente.

## 2. Ripristino necessario dei prerequisiti locali

Preflight: schema creato da EF, owner shadow gia presenti con sentinel zero, zero Institution/membership, zero trigger MT3A, zero FK composite MT3A, manifest assente, cinque colonne tecniche SQL-only assenti. Questa differenza impediva di verificare le garanzie di integrita richieste: non e stata aggirata indebolendo test o assertion.

Tool esplicito `Backend/ElectronicRegisterAPI/tools/Mt3cTestPreparation`: consente soltanto loopback/test DB, strict mode, forma EF non migrata senza tenant assegnati, nessuna membership o vincolo/trigger MT3A preesistente. Ripristina le cinque colonne e gli script canonici **006/007/009/010/011 MT3A**, senza modificare questi script. Holding tecnica `4d543341-0000-4000-8000-000000000001`, `MT3A_LEGACY_TEST`, **provisioning**; nessun account o membership permanente. Owner zero del seed attribuiti alla sola holding esplicita. Backfill transazionale, neutralizzazione `updated_at`, preflight del grafo e confronto dei digest di tutte le celle legacy prima/dopo.

Backup completo schema/dati prima di ogni DDL, con verifica di leggibilita/completamento e SHA-256. Dump locali privati ignorati da Git in `Backend/ElectronicRegisterAPI/mt3c-evidence/private/`; non includerli nei commit. Nessun DROP/reset database o disabilitazione FK. DDL non transazionale; nessuna promessa di restore certificato.

Il primo tentativo si e fermato su tre residui delle prove negative iniziali, che il DB incompleto aveva erroneamente accettato: Subject `Missing owner`, audit `mt3a.verify` orfano e `platform.bootstrap` con forma non valida. Rimossi esclusivamente i tre ID della fixture, dopo backup. Ispezionate le cinque colonne gia aggiunte e la sola holding provisioning, poi ripresa esplicita `resume-reviewed-columns-only`; nessun retry cieco o riclassificazione di audit sconosciuti. Il confronto finale dei digest legacy e passato. Nessuna modifica a AuditEvent, audit didattico, worker o manager esami/voti.

Il client XAMPP non accetta i flag mysqldump MySQL 8: il tooling MT3A conserva il comportamento precedente salvo opt-in `MYSQLDUMP_FLAVOR=MariaDB` e `MYSQLDUMP_EXE` esplicito. Identici controlli su target, backup completo e hash; nessun effetto sui servizi API. L'utente ha autorizzato temporaneamente strict mode globale: valore originale `NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION`, aggiunto solo `STRICT_TRANS_TABLES` durante le prove. **Valore originale ripristinato e verificato anche su una nuova sessione** a fine validazione: [evidenza](TestResults/mt3c/sql-mode-restored.txt). Una futura esecuzione della suite richiede nuovamente strict mode; non si promette baseline verde con la configurazione XAMPP permissiva originale.

## 3. Percorso di sicurezza

HTTP -> schema **InstitutionAccess** (firma HS256, typ/purpose/audience/lifetime MT2) -> requirement MT2 -> query membership con UserId+InstitutionId+MembershipId, Institution Active e insieme esatto dei ruoli correnti -> `CurrentTenant` scoped esistente -> policy `InstitutionSchoolAccess` e ruolo dell'action -> filtro globale -> controller -> manager istituzionale MT3B -> rivalidazione e profilo -> repository tenant-aware -> SQL scoped -> DTO.

`InstitutionSchoolAccess` ha un solo authentication scheme, InstitutionAccess. Nessun Bearer alternativo, nessun ruolo legacy `admin` equivalente a InstitutionAdmin. I ruoli dell'action usano il RoleClaimType `institution_role`; MT2 li confronta con la singola membership selezionata. Aggiunta o rimozione di ruoli invalida il vecchio token, richiedendo nuova selezione. Nessun ruolo sommato tra A e B.

Si riutilizza ICurrentTenant MT2: nessun altro TenantContext/accessor, nessun contesto ricostruito dal controller, nessun fallback ai campi User.Role/StudentId/TeacherId. Le risoluzioni personali sono UserId+InstitutionId+MembershipId e owner del profilo. Student/Teacher `me` non ricevono ID. GUID in query/header/body non cambiano il tenant verificato.

## 4. Gate e rollout

`InstitutionSchoolEndpointAttribute` deriva da AuthorizeAttribute e imposta la policy dedicata. Soltanto le tre nuove classi lo applicano. Il filtro globale LegacyEndpointGate conserva integralmente il ramo legacy. L'esenzione scolastica richiede marker con policy esatta, assenza di AllowAnonymous, assenza di schemi alternativi/policy estranee, flag server-side vero, identita autenticata e accessor scoped gia inizializzato. Marker con policy alterata o contesto assente produce 403. Non usa prefissi URL, non ammette genericamente token InstitutionAccess.

Authentication e authorization middleware precedono MVC; il filtro autorizzativo viene eseguito prima di model binding e action. Una configurazione incompleta non inizializza il contesto e non apre l'action. Le dipendenze opzionali del gate consentono ai vecchi host DI non HTTP di costruire i servizi; il ramo istituzionale senza tali dipendenze rimane chiuso. Il test worker con ValidateOnBuild/ValidateScopes rimane verde.

Flag server-side `Multitenancy:SchoolApi:Enabled`, default-deny tramite TryParse, separato da JWT/ruoli/ownership. Esempio versionato falso; nessun appsettings runtime cambiato. Test con JWT valido e flag assente prova il diniego di **tutte le 24 action** e persistenza invariata. Non e una regola "tenant Active passa". Nessuna Institution reale attivata.

## 5. Matrice completa Student/Teacher/Subject

A = InstitutionAdmin; T = Teacher; S = Student. Ruoli legacy minuscoli. ISM = IInstitutionStudentManager; ITM = IInstitutionTeacherManager; ISubM = IInstitutionSubjectManager. Tutte le nuove route sono HTTP migrate ma operative soltanto con flag esplicito; tutte le legacy restano separate e gated.

Test abbreviati: SR = StudentReadsRespectAdminTeacherAndOwnProfileAcrossBothTenants; TD = TeacherDirectoryAndSelfProfileAreLocalForEveryRole; SU = SubjectsIntersectAssignmentsForAllLookupsIncludingByTeacher; CRUD = AdminCrudReturnsRealLocationAndPersistsOnlyOwnTenant; XT = CrossTenantIdsAreIndistinguishableFromMissingAndNeverChangePersistedData; FL = ForeignLinksAreRejectedAtomicallyByManager. Ogni action e inoltre verificata da EveryActionRejectsInvalidTokenBeforeAnyWrite, CommittedRevocationBlocksAllActionsAndWrites e DefaultRolloutRejectsValidScopedTokensForAllActions.

| Metodo | Route legacy | Ruoli legacy | Nuova route | Ruoli | Manager.metodo | Risorsa/visibilita | Stato/test HTTP |
| --- | --- | --- | --- | --- | --- | --- | --- |
| GET | /api/Student | teacher,admin | /api/institution/students | A,T | ISM.GetAllAsync | tenant/classi assegnate | migrata; SR |
| GET | /api/Student/count | teacher,admin | /api/institution/students/count | A,T | ISM.CountAsync | count SQL stessa visibilita | migrata; SR |
| GET | /api/Student/{id} | teacher,admin | /api/institution/students/{id:guid} | A,T | ISM.GetByIdAsync | owner+ID+assegnazioni | migrata; SR,XT |
| GET | /api/Student/bylastname/{lastName} | teacher,admin | /api/institution/students/bylastname/{lastName} | A,T | ISM.GetByLastNameAsync | ricerca scoped | migrata; SR |
| GET | - | - | /api/institution/students/me | S | ISM.GetOwnProfileAsync | profilo personale membership | nuova; SR |
| POST | /api/Student | admin | /api/institution/students | A | ISM.AddAsync | owner server; classe/membership scoped | migrata; CRUD,FL |
| PUT | /api/Student/update/{id} | admin | /api/institution/students/{id:guid} | A | ISM.UpdateAsync | nome/cognome, nessun trasferimento | migrata; CRUD,XT |
| DELETE | /api/Student/{id} | admin | /api/institution/students/{id:guid} | A | ISM.DeleteAsync | owner; Grade impedisce delete | migrata; CRUD,XT |
| GET | /api/Teacher | teacher,admin,student | /api/institution/teachers | A,T,S | ITM.GetAllAsync | directory solo tenant | migrata; TD |
| GET | /api/Teacher/count | teacher,admin | /api/institution/teachers/count | A,T | ITM.CountAsync | count SQL tenant | migrata; TD |
| GET | /api/Teacher/{id} | teacher,admin,student | /api/institution/teachers/{id:guid} | A,T,S | ITM.GetByIdAsync | owner+ID | migrata; TD,XT |
| GET | - | - | /api/institution/teachers/bylastname/{lastName} | A,T,S | ITM.GetByLastNameAsync | directory tenant | nuovo lookup; TD |
| GET | - | - | /api/institution/teachers/me | T | ITM.GetOwnProfileAsync | profilo personale membership | nuova; TD |
| POST | /api/Teacher | admin | /api/institution/teachers | A | ITM.AddAsync | link membership esistente autorizzata | migrata; CRUD,FL |
| PUT | /api/Teacher/update/{id} | admin | /api/institution/teachers/{id:guid} | A | ITM.UpdateAsync | owner+ID | migrata; CRUD,XT |
| DELETE | /api/Teacher/{id} | admin | /api/institution/teachers/{id:guid} | A | ITM.DeleteAsync | ClassSubject impedisce delete | migrata; CRUD,XT |
| GET | /api/Subject | teacher,admin,student | /api/institution/subjects | A,T,S | ISubM.GetAllAsync | catalogo/assegnazioni/propria classe | migrata; SU |
| GET | /api/Subject/count | teacher,admin,student | /api/institution/subjects/count | A,T,S | ISubM.CountAsync | count SQL stessa visibilita | migrata; SU |
| GET | /api/Subject/{id} | teacher,admin,student | /api/institution/subjects/{id:guid} | A,T,S | ISubM.GetByIdAsync | owner+ID+assegnazioni | migrata; SU,XT |
| GET | /api/Subject/byname/{name} | teacher,admin,student | /api/institution/subjects/byname/{name} | A,T,S | ISubM.GetByNameAsync | nome scoped | migrata; SU |
| GET | /api/Subject/byteacher/{id} | teacher,admin,student | /api/institution/subjects/byteacher/{teacherId:guid} | A,T,S | ISubM.GetByTeacherAsync | stessa ClassSubject visibile | migrata; SU |
| POST | /api/Subject | admin | /api/institution/subjects | A | ISubM.AddAsync | nome UNIQUE nel tenant | migrata; CRUD |
| PUT | /api/Subject/update/{id} | admin | /api/institution/subjects/{id:guid} | A | ISubM.UpdateAsync | owner+ID | migrata; CRUD,XT |
| DELETE | /api/Subject/{id} | admin | /api/institution/subjects/{id:guid} | A | ISubM.DeleteAsync | ClassSubject impedisce delete | migrata; CRUD,XT |

Inventario **dinamico di tutte le 102 action correnti**, non soltanto il conteggio storico: [Production](TestResults/mt3c/routes-Production.json), [Development](TestResults/mt3c/routes-Development.json). Include metodo, route, controller/action, ruoli, policy, manager e marker di migrazione. **73 legacy + 5 identita MT2 + 24 scolastiche nuove**. Nessun duplicato metodo/route. Le 73 legacy sono ancora tutte attraversate dai test MT2 con ticket e JWT A/B; i gate di ruolo dell'audit sono preservati e le nuove route contate separatamente. Nessuna action legacy acquisisce InstitutionAccess/InstitutionSchoolAccess.

## 6. Contratti HTTP e business policy

DTO preesistenti riutilizzati senza InstitutionId. POST Student riceve classId e membershipId opzionali **in query**, POST Teacher membershipId opzionale in query; non sono autorizzazioni e vengono verificati dal manager/repository. Non sono aggiunti campi selezionabili di ownership. Update conserva solo i campi gia ammessi. POST non crea User, membership o ruoli.

GET collezioni valide: **200 con []** anche senza assegnazioni/risultati. GET singola risorsa fuori tenant, invisibile o inesistente: **404**, risposta indistinguibile per GUID esterno/inesistente. Creazione **201**, body Guid reale, Location istituzionale seguita con GET positivo. PUT/DELETE **204**; input invalido **400**; token assente/non valido **401**; policy/ruolo/revoca/profilo mancante **403**; conflitto di unicita/dipendenza **409**. GlobalExceptionHandler esistente gestisce ArgumentException/UnauthorizedAccessException/KeyNotFoundException/BusinessRuleException senza dettagli SQL o connessioni. Nessun cambiamento delle convenzioni legacy.

Student: A gestisce tenant; T legge studenti di sole classi assegnate; S solo `me`, directory vietata. Teacher senza assegnazioni riceve lista/count vuoti. Teacher: directory locale per A/T/S con profilo corrente richiesto ai non admin; count solo A/T; `me` solo T; scritture solo A. Membership collegata deve essere esistente, Active, stesso istituto, ruolo pertinente e senza link incoerente; errori rollbackano il nuovo profilo.

Subject: A catalogo locale; T proprie assegnazioni; S propria classe. ByTeacher interseca **la stessa assegnazione visibile**, non solo SubjectId: stessa materia in altra classe e stesso docente con altre materie non ampliano i diritti. TeacherId B produce [] nello scope A. Student senza ClassId non riceve materie. Nome uguale A/B ammesso, duplicato entro A 409. Delete con ClassSubject e Student con Grade rimangono 409 senza alterare dipendenze.

## 7. Prove reali HTTP/MySQL A/B

`Mt3cHttpTests` avvia il vero entry point Program.cs con Kestrel su porta effimera. Nessun mock manager/repository, nessun auth handler fittizio, nessun worker disabilitato. JWT ottenuti tramite login/password reale e selezione istituto, salvo token negativi deliberatamente riscritti e rifirmati con chiave solo test.

Fixture temporanea: A/B Active, membership Admin/Teacher/Student, User condivisi A/B con profili distinti, secondo Admin A, classi assegnate/non assegnate, due materie per istituto con nomi omonimi, materia condivisa tra classi/docenti nello stesso istituto. User.Role globale deliberatamente incoerente con la membership, User.TeacherId A presente per provare l'assenza di fallback in B.

Ogni nuova action riceve richieste reali positive pertinenti e negative. CRUD per ciascuna famiglia: Guid reale, follow-up Location, INSERT owner esplicito verificato da DbContext nuovo, PUT riletto via nuova richiesta, DELETE verificato via nuovo DbContext. GET/PUT/DELETE A con ID B e GUID inesistente: 404 comparato, digest di A/B invariati da nuove connessioni. POST con classe/membership B negata atomicamente; link valido crea solo profili e lascia User/membership/ruoli invariati. Ricerche di nomi esclusivi B non rivelano B, count A invariato. Nomi soggetto uguali creati via HTTP in A/B. A/B concorrenti con stessa identita non condividono scope.

T/S non scrivono alcuna famiglia: POST/PUT/DELETE 403 e digest persistiti invariati. Query/header/body InstitutionId B non cambiano owner A. Directory, dettaglio, ricerca, count e profili personali testati nei due istituti. Me ignora ID arbitrari del client. Profilo mancante nega senza fallback. Teardown limitato agli ID/tenant della fixture, in ordine FK; nessun tenant operativo permanente.

## 8. JWT e revoca

Per **tutte le 24 action**: header assente, JWT legacy admin, selection ticket, token scaduto e audience errata -> 401; token firmato con membership di altro User -> 403. Nessuna scrittura, digest A/B invariati. Token A/B validi provati separatamente con la stessa identita e profili diversi.

Per tutte le action dopo commit DB: membership Suspended, Institution Suspended, rimozione InstitutionAdmin, rimozione Teacher, aggiunta ruolo -> 403. B resta accessibile. Multiruolo A Admin+Teacher non conferisce admin in B Teacher; profilo richiesto anche in presenza di ruoli. MT2 conserva tutti i casi aggiuntivi di firma/issuer/type/purpose, claim duplicati/mancanti, revoca e selezione incoerente. Nessuna revoca retroattiva di operazioni gia autorizzate in corso viene promessa.

## 9. Bootstrap e OpenAPI

Program reale **Development e Production**, ValidateScopes/ValidateOnBuild attivi, tutti gli schemi/policy risolti, accessor scoped, filtri MVC, tre controller e worker produttivo registrati. Una richiesta JWT reale raggiunge il DB in entrambi. Inventario verifica policy, ruoli, manager ammessi, assenza InstitutionId nelle route e duplicati; helper verifica ApplicationStopped dopo StopAsync e completamento dell'entry point.

Development: `/openapi/v1.json` 200 e ogni metodo delle 24 action presente con route corretta e DTO esistenti; [path esportati](TestResults/mt3c/openapi-school-paths.json). Production: OpenAPI 404 secondo configurazione preesistente. L'inventario runtime e la policy reale sono la prova dell'autorizzazione; non si deducono i controlli JWT dalla sola descrizione OpenAPI. GlobalExceptionHandler resta operativo per errori previsti con 400/403/404/409; niente 500 applicativi nei casi autorizzativi/business testati.

## 10. Query e overhead HTTP misurato

[Comandi SQL e misure HTTP](TestResults/mt3c/http-query-observations.json), catturati dal DiagnosticListener EF reale durante richieste seriali. Nessun valore dei parametri o credenziale esportato. Liste, dettagli e count di ogni famiglia e profili personali:

| Caller | Query per lista/dettaglio/count | Composizione |
| --- | ---: | --- |
| Admin | 3 | authorization HTTP + rivalidazione manager + query risorsa |
| Teacher | 5 | precedenti + rivalidazione resolver + lookup profilo |
| Student | 5 | precedenti + rivalidazione resolver + lookup profilo |
| Me Student/Teacher | 5 | policy + manager + resolver + profilo + risorsa |

Overhead di autorizzazione: 2 query Admin, 4 query T/S rispetto alla singola lettura della risorsa. Non introdotta cache per evitare autorizzazioni obsolete. Il costo deriva dalle verifiche esistenti MT3B, conservate intenzionalmente. SQL ha InstitutionId prima della materializzazione, EXISTS per ClassSubject/profili, COUNT(*) server-side; repository AsNoTracking preservati e relativa assertion MT3B verde. Una lista Student con 2 e poi 7 righe ha lo stesso numero di comandi: nessun N+1. Nessun caricamento globale seguito da filtro in memoria, Redis o indice speculativo. I tempi osservati sono locali su poche righe: **non benchmark o garanzia di prestazioni produttive**.

## 11. Checkpoint e validazione

Checkpoint 1: build/baseline ripristinata e test negativo HTTP prima delle route [1/1](TestResults/mt3c/checkpoint-1/). Checkpoint 2 Student: build e [6/6](TestResults/mt3c/checkpoint-2/). Checkpoint 3 Teacher: build e [7/7](TestResults/mt3c/checkpoint-3-corrected/). Il filtro iniziale troppo ampio del checkpoint 3 includeva casi Subject non ancora esposti: 4 errori 404 conservati, filtro corretto senza indebolire assertion. Checkpoint 4 Subject+JWT+legacy: build e [44/44](TestResults/mt3c/checkpoint-4/). Checkpoint 5: bootstrap/security [48/48](TestResults/mt3c/checkpoint-5/), HTTP esteso [57/57](TestResults/mt3c/http-final/), poi aggiunta prova concorrente prima delle suite finali.

Build soluzione completa riuscita, zero errori; soli warning nullable preesistenti. Numeri definitivi e confronto per identita dei casi riportati in [verification.json](TestResults/mt3c/verification.json). TRX distinti per progetto/run in [final-1](TestResults/mt3c/final-1/) e [final-2](TestResults/mt3c/final-2/). Nessun test cancellato/disabilitato; assertion legacy preservate e inventari amplificati per distinguere le nuove action.

| Esecuzione | Unit Passed | Integration Passed | Totale Passed | Failed | Skipped |
| --- | ---: | ---: | ---: | ---: | ---: |
| Baseline ripristinata | 48 | 378 | 426 | 0 | 0 |
| Final-1 | 48 | 434 | 482 | 0 | 0 |
| Final-2 consecutiva | 48 | 434 | 482 | 0 | 0 |

Nessuna modifica del codice tra final-1 e final-2. Lo script [Verify-Evidence.ps1](TestResults/mt3c/Verify-Evidence.ps1) legge realmente XML, controlla ogni outcome, assenza skip, identita dei 426 precedenti, 56 nuovi casi tutti MT3C e uguaglianza degli insiemi finali; esporta SHA-256 per ogni TRX in verification.json. Le suite includono MT1, MT2, Profiles, MT3A, MT3B, Fasi 1-4, audit finale e bootstrap.

56 nuovi casi: 55 in Mt3cHttpTests (teorie parametrizzate comprese) e uno preflight. Comprendono tutte le action, ruoli, token/revoca, CRUD e dipendenze, collegamenti e rollback, GUID cross-tenant, search/count, profili, multiruolo, flag, input, metadata/context, concorrenza, SQL e bootstrap Dev/Production.

Stato DB finale verificato da nuova connessione in [final-db-state.txt](TestResults/mt3c/final-db-state.txt): sola holding provisioning, zero membership e link profilo, zero Student/Teacher/Subject fuori holding, **27 FK composite e 40 trigger**. Nessun tenant o account reale permanente aggiunto.

`git diff --check` completo **passato** dopo normalizzazione della sola riga vuota finale preesistente in EXAM_BUSINESS_RULES_UPDATE.md; nessun contenuto del report storico modificato. Controllati separatamente anche i nuovi file non tracciati.

## 12. File e separazione delle garanzie

Api: tre nuovi Institution*Controller; nuovo InstitutionSchoolEndpointAttribute; policy aggiunta a InstitutionAuthentication; ramo marker/context/rollout in LegacyEndpointGate; esempio appsettings default false. Infrastructure: sola portabilita esplicita backup nel tooling Mt3aOwnershipMigration. Domain/Business/Application e repository/manager MT3B invariati.

Test: Mt3cHttpTests, Mt3cPreflightTests, Mt3cProgramHost nuovi; Mt2IdentityTests e AuditBootstrapTests aggiornano la classificazione degli endpoint conservando le prove legacy. Tool esplicito Mt3cTestPreparation e ignore backup privati; report/evidenze MT3C; aggiornamenti Design e Isolation Matrix. Nessun frontend, Exam/Submission/Grade manager, ConvertToGrade, lock, worker, formula, recupero, randomizzazione, AuditEvent o CourseEdition cambiato.

| Livello | Garanzia | Prova/limite |
| --- | --- | --- |
| JWT | firma/scopo/audience/lifetime e identita strutturalmente validi | casi MT2 + tutti gli endpoint MT3C con token negativi |
| Policy HTTP | schema esclusivo, membership/stato/ruoli correnti, flag e contesto | pipeline Program reale, revoca prima action, gate alterato/contesto assente |
| Manager | permessi e profilo corrente, regole di business e rivalidazione | suite MT3B preservata + HTTP A/B, dipendenze e link |
| Repository | query scoped e visibilita assegnazioni | SQL catturato, liste/dettagli/count/byteacher HTTP, persistenza riletta |
| SQL | owner immutabile, FK composite e unicita scoped | MT3A ripristinata, test originali con errori MySQL specifici |
| Operativita | flag chiuso e holding provisioning | default negato con JWT valido, nessun onboarding/cutover |

SQL non autentica utenti; JWT valido non autorizza risorse esterne; marker non sostituisce la policy; repository non verifica autonomamente JWT; test piccoli non provano carico o recovery produttivi.

## 13. Endpoint ancora bloccati e prerequisiti MT-3D

Tutte le route scolastiche legacy restano protette dal gate, inclusi Student/Teacher/Subject precedenti, Grade, Exam, User, Biennium e altri lookup amministrativi correnti. Ticket e scoped token non raggiungono i loro manager globali. ExamSubmission, Grade, Worker e audit didattico restano non migrati; nessuna apertura indiretta tramite i nuovi controller. Tutte le 24 route nuove restano operative soltanto negli host con flag server-side esplicito.

MT-3D richiede disegno e test separati di autorizzazione risorsa, assegnazioni, letture e mutazioni Exam/Submission/Grade e processi senza JWT, con conservazione di lock, lifecycle e regole business. Audit/worker, account management istituzionale, recovery, manifest legale e cutover restano checkpoint successivi. Nessuna attivazione automatica della holding o membership permanente. Prima di riprodurre le prove su XAMPP configurare strict mode e client backup compatibile; non eseguire il tool di ripristino su un DB gia migrato, su tenant reali o come bootstrap applicativo.
