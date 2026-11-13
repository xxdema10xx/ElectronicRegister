# Staging HTTP audit completo

Repository `xxdema10xx/ElectronicRegister`, branch `refactor-database-entities`.
Baseline indicata: `c50a5e71ca6acf470180bc7641429e0308ba1d89`; run di riferimento `41138402492`.
Verifica locale: 30 ottobre 2026. Nessun commit, push o avvio workflow.

## Esito e limiti

Il runtime Docker isolato abilita esplicitamente InstitutionCalendar. Il nuovo audit contiene **54 casi in 9 scenari**, accumula PASS/FAIL/BLOCKED, salva un report sanitizzato e fallisce se un solo caso non passa.

**Docker non Ã¨ disponibile nel PATH locale: il runtime HTTP completo Docker/MySQL 8.4.13 non Ã¨ stato verificato. SarÃ  eseguito da GitHub Actions contro API e database reali, senza mock.** I successi SQL, audit 71 tabelle/180 trigger, TLS, seed e startup della run precedente sono informazioni fornite nel task, non nuove attestazioni di questa sessione.

## Difetti e correzioni

1. Il flag InstitutionCalendar mancava: CalendarControllerFeatureProvider rimuoveva il controller. Ora Ã¨ true soltanto nel generatore dello staging Docker; SchoolApi e RolloutApproved restano true. Nessun altro rollout viene aperto.
2. Il primo status inatteso interrompeva lo smoke e perdeva il report. Ogni chiamata ha ora dipendenze esplicite; errori indipendenti vengono accumulati. Anche le asserzioni sulle risposte fanno fallire il proprio caso; soltanto i consumatori di risultati mancanti diventano BLOCKED.
3. Il comando host http controllava la readiness prima dello smoke: una readiness degradata avrebbe nascosto tutti gli altri casi. Nel solo percorso audit il controllo HTTP Ã¨ delegato a https-readiness. Restano obbligatori proprietÃ  dei container, API running, UID non-root, bind loopback, SQL non pubblicato e salute del DB. Start/restart/verify mantengono i controlli precedenti; i gate SQL e preflight restano bloccanti.
4. La stampa delle evidenze era dopo il comando HTTP nello stesso step fail-fast. Raccolta e upload sono ora step always(), limitati ai JSON http-smoke-*.json. Si stampano conteggi ed esiti aggregati e si conservano gli artifact per 14 giorni.
5. Il vecchio flusso non copriva UPDATE/concorrenza delle classi: aggiunti PUT con If-Match dall'ETag del GET e rifiuto 409 della revisione obsoleta. Verificato che UpdateInstitutionClassDto ammette solo name: il nuovo payload rispetta il contratto. Aggiunto anche il diniego calendario 403 allo studente.
6. I replay riusano eventId/payload del calendario e revisione 2/risposte del submit. Nessun retry automatico dopo timeout. Un marker esclusivo impedisce un secondo audit mutante nello stesso namespace: serve un nuovo staging, senza cancellare volumi o dati.
7. Rafforzati receipt calendario (eventId, institution resourceId, revisione 0), settings salvati e voto ufficiale: un solo voto dello studente sintetico, valore 10, materia/docente corretti. Restano gli status originari, le verifiche JWT, i dinieghi, l'isolamento, le opzioni corrette nascoste allo studente e CORS.

Non sono emerse altre incongruenze verificabili nelle route/payload originari oltre al flag mancante. L'esito contrattuale non certifica il comportamento degli handler o del database reali.

## Contratti, policy, fixture e tempi

- Identity: InstitutionIdentityController, LoginDto e SelectInstitutionDto. Login reale, ticket di selezione, membership esplicita, select e contesto. Verifica firma HS256, typ/purpose, issuer, audience, sub, scadenza e institution_id. Ticket e access token non sono intercambiabili; la firma alterata Ã¨ soltanto un caso negativo.
- Classi: InstitutionClassController, manager/repository e InstitutionClassRules; solo InstitutionAdmin, SchoolApi. Nomi entro 20 caratteri, GUID del percorso, ETag e revisione. DELETE riguarda soltanto la classe nuova senza studenti/assegnazioni/storico didattico; resta indipendente dall'esito di read/update. Liste proprie senza classe esterna; GET esterno 404.
- Calendario: InstitutionCalendarController, provider, CalendarFeature, manager/repository, CalendarSettingsCommand/Receipt/Settings. SchoolApi e InstitutionCalendar, ruoli InstitutionAdmin/Staff. Europe/Rome, lead 7 (0..366), reason non vuota, expectedRevision -1 solo con settings assenti in un namespace nuovo. Il repository verifica il replay per eventId prima della revisione. Queste operazioni non richiedono FormativePlans. Il caso esterno usa token B, classe A, mese 2100/9 e non crea periodi.
- Esami: InstitutionExamController, InstitutionExamManager, ExamManager, ExamService e CreateExamDto. ExamApi e SchoolApi; docente A collegato a class_subject, studente A nella classe assegnata. Una domanda type 0, punti 1, due opzioni e una corretta, soglia 60. Name Ã¨ ignorato nel JSON ed Ã¨ derivato dalla materia. Finestra UTC generata al momento della creazione: -5 minuti/+1 ora, durata 15 (>5 e dentro la finestra). Pubblicazione prima dello start. Il flusso non dipende dal calendario o dalla classe CRUD.
- Submission: InstitutionSubmissionController, SaveExamAnswersDto e SubmitExamDto. Student per start/save/submit; submissionId da start, question/option ID dal dettaglio docente. Autosave revisione 1, submit 2, risultato passed/grade 10, replay identico. Risultato della submission A nascosto all'admin B.
- Voti: InstitutionGradeController, GradeDto/GradePageDto e query studentId. Verifica persistenza dopo submit e filtro di A vuoto per B. Il filtro esterno Ã¨ indipendente dal successo del submit.
- Sicurezza: InstitutionEndpointGate, marker School/Exam/Identity, InstitutionAuthentication e policy/ruoli. Le vecchie route /api/Exam e /api/Grade sono assenti. Nessun endpoint diagnostico pubblico, bypass o autorizzazione aggiuntiva.
- HTTP: OperationalPipeline e RuntimePolicy. Readiness/live, HSTS, nosniff, DENY, proxy noto in loopback; CORS positivo/negativo. Sul percorso HTTPS resta la verifica CA/hostname. Il numero di POST Ã¨ sotto il limite reale di 60/minuto, che non Ã¨ stato innalzato.
- Seed: esaminato ops.py. Quattro account/membership attive con ruoli espliciti, due istituzioni e classi/percorso propri; docente, studente, materia e assegnazione di A. Nuovi eventi, nomi classe e descrizioni esame univoci. Esecuzione sequenziale; controllo di concorrenza ottimistica tramite ETag. Gare e lock DB non sono stati attestati offline.

True attesi: SchoolApi, CourseApi, ExamApi, MembershipAdminApi, MembershipWriteApi, InstitutionCalendar. False espliciti: Onboarding, Governance, InstitutionWorker e Identity:Legacy. FormativePlans e InstitutionImport restano non abilitati (chiavi assenti, default fail-closed); il controller FormativePlans Ã¨ escluso dalle route MVC controllate.

## Inventario completo

Tabella generata dallo stesso piano eseguibile di smoke.py --contracts. admin_a/admin_b = InstitutionAdmin di A/B; teacher/student appartengono ad A. Tutti i casi dipendono anche dal setup sicuro e dal seed. OPTIONS Ã¨ gestito da CORS sulla route GET corrispondente. Nessun GUID, token o body sensibile Ã¨ incluso.

| Scenario | Caso | Metodo e route | Atteso | Attore/credenziale | Flag | Dipendenze |
|---|---|---|---:|---|---|---|
| health | https-readiness | `GET /health/ready` | 200 | - | - | - |
| health | liveness | `GET /health/live` | 200 | - | - | - |
| authorization | anonymous-denied | `GET /api/institution/classes` | 401 | - | SchoolApi | - |
| identity | login-admin_a | `POST /api/Auth/identity/login` | 200 | admin_a (login) | - | - |
| identity | choices-admin_a | `GET /api/Auth/institutions` | 200 | admin_a (ticket) | - | login-admin_a |
| identity | selection-admin_a | `POST /api/Auth/select-institution` | 200 | admin_a (ticket) | - | choices-admin_a |
| identity | context-admin_a | `GET /api/Auth/context` | 200 | admin_a | - | selection-admin_a |
| identity | login-admin_b | `POST /api/Auth/identity/login` | 200 | admin_b (login) | - | - |
| identity | choices-admin_b | `GET /api/Auth/institutions` | 200 | admin_b (ticket) | - | login-admin_b |
| identity | selection-admin_b | `POST /api/Auth/select-institution` | 200 | admin_b (ticket) | - | choices-admin_b |
| identity | context-admin_b | `GET /api/Auth/context` | 200 | admin_b | - | selection-admin_b |
| identity | login-teacher | `POST /api/Auth/identity/login` | 200 | teacher (login) | - | - |
| identity | choices-teacher | `GET /api/Auth/institutions` | 200 | teacher (ticket) | - | login-teacher |
| identity | selection-teacher | `POST /api/Auth/select-institution` | 200 | teacher (ticket) | - | choices-teacher |
| identity | context-teacher | `GET /api/Auth/context` | 200 | teacher | - | selection-teacher |
| identity | login-student | `POST /api/Auth/identity/login` | 200 | student (login) | - | - |
| identity | choices-student | `GET /api/Auth/institutions` | 200 | student (ticket) | - | login-student |
| identity | selection-student | `POST /api/Auth/select-institution` | 200 | student (ticket) | - | choices-student |
| identity | context-student | `GET /api/Auth/context` | 200 | student | - | selection-student |
| authorization | selection-cannot-access | `GET /api/institution/classes` | 401 | admin_a (ticket) | SchoolApi | login-admin_a |
| authorization | access-cannot-select | `GET /api/Auth/institutions` | 401 | admin_a | - | selection-admin_a |
| authorization | tampered-signature | `GET /api/institution/classes` | 401 | admin_a (firma alterata) | SchoolApi | selection-admin_a |
| authorization | foreign-membership-selection | `POST /api/Auth/select-institution` | 403 | admin_a (ticket) | - | login-admin_a |
| authorization | foreign-class-hidden | `GET /api/institution/classes/{id:guid}` | 404 | admin_b | SchoolApi | selection-admin_b |
| calendar | calendar-configure-admin_a | `PUT /api/institution/calendar/settings` | 200 | admin_a | SchoolApi, InstitutionCalendar | selection-admin_a |
| calendar | calendar-configure-replay-admin_a | `PUT /api/institution/calendar/settings` | 200 | admin_a | SchoolApi, InstitutionCalendar | calendar-configure-admin_a, selection-admin_a |
| calendar | calendar-configure-admin_b | `PUT /api/institution/calendar/settings` | 200 | admin_b | SchoolApi, InstitutionCalendar | selection-admin_b |
| calendar | calendar-configure-replay-admin_b | `PUT /api/institution/calendar/settings` | 200 | admin_b | SchoolApi, InstitutionCalendar | calendar-configure-admin_b, selection-admin_b |
| calendar | calendar-settings | `GET /api/institution/calendar/settings` | 200 | admin_a | SchoolApi, InstitutionCalendar | calendar-configure-admin_a, selection-admin_a |
| calendar | foreign-calendar-class-hidden | `GET /api/institution/calendar/months/{year:int}/{month:int}/classes/{classId:guid}` | 404 | admin_b | SchoolApi, InstitutionCalendar | calendar-configure-admin_b, selection-admin_b |
| calendar | calendar-student-forbidden | `GET /api/institution/calendar/settings` | 403 | student | SchoolApi, InstitutionCalendar | selection-student |
| classes | classes-admin_a | `GET /api/institution/classes` | 200 | admin_a | SchoolApi | selection-admin_a |
| classes | classes-admin_b | `GET /api/institution/classes` | 200 | admin_b | SchoolApi | selection-admin_b |
| authorization | legacy-exam-retired | `GET /api/Exam` | 404 | admin_a | - | selection-admin_a |
| authorization | legacy-grade-retired | `GET /api/Grade` | 404 | admin_a | - | selection-admin_a |
| classes | class-write | `POST /api/institution/classes` | 201 | admin_a | SchoolApi | selection-admin_a |
| classes | class-read | `GET /api/institution/classes/{id:guid}` | 200 | admin_a | SchoolApi | class-write, selection-admin_a |
| classes | class-update | `PUT /api/institution/classes/{id:guid}` | 204 | admin_a | SchoolApi | class-read, selection-admin_a |
| classes | class-stale-update | `PUT /api/institution/classes/{id:guid}` | 409 | admin_a | SchoolApi | class-update, selection-admin_a |
| classes | class-delete | `DELETE /api/institution/classes/{id:guid}` | 204 | admin_a | SchoolApi | class-write, selection-admin_a |
| exams | student-write-forbidden | `POST /api/institution/exams` | 403 | student | SchoolApi, ExamApi | selection-student |
| exams | teacher-create-exam | `POST /api/institution/exams` | 201 | teacher | SchoolApi, ExamApi | selection-teacher |
| exams | foreign-exam-hidden | `GET /api/institution/exams/{id:guid}` | 404 | admin_b | SchoolApi, ExamApi | teacher-create-exam, selection-admin_b |
| exams | teacher-draft | `GET /api/institution/exams/{id:guid}` | 200 | teacher | SchoolApi, ExamApi | teacher-create-exam, selection-teacher |
| exams | publish | `POST /api/institution/exams/{id:guid}/publish` | 204 | teacher | SchoolApi, ExamApi | teacher-create-exam, selection-teacher |
| submission | student-start | `POST /api/institution/exams/{id:guid}/start` | 200 | student | SchoolApi, ExamApi | publish, selection-student |
| submission | autosave | `PUT /api/institution/submissions/{id:guid}/answers` | 200 | student | SchoolApi, ExamApi | student-start, teacher-draft, selection-student |
| submission | submit | `POST /api/institution/submissions/{id:guid}/submit` | 200 | student | SchoolApi, ExamApi | autosave, selection-student |
| submission | submit-replay | `POST /api/institution/submissions/{id:guid}/submit` | 200 | student | SchoolApi, ExamApi | submit, selection-student |
| authorization | foreign-submission-hidden | `GET /api/institution/submissions/{id:guid}/result` | 404 | admin_b | SchoolApi, ExamApi | student-start, selection-admin_b |
| grades | official-grades | `GET /api/institution/grades` | 200 | admin_a | SchoolApi, ExamApi | submit, selection-admin_a |
| grades | foreign-grade-filter | `GET /api/institution/grades/paged` | 200 | admin_b | SchoolApi, ExamApi | selection-admin_b |
| cors | cors-allowed | `OPTIONS /api/institution/classes` | 204 | - | SchoolApi | - |
| cors | cors-denied | `OPTIONS /api/institution/classes` | 204 | - | SchoolApi | - |

## Runner, report e sicurezza

Path, body, token e header sono risolti solo dopo PASS delle dipendenze. Risultati e header necessari restano in memoria, senza entrare nei report. Un login A fallito non blocca login B, teacher/student, health o CORS. Un guasto calendario non blocca classi/esami. Un create-exam fallito blocca i consumatori del suo ID: non vengono inviate richieste con ID nulli o inventati.

Ogni record contiene scenario, name, method, route template, expected, received (null se indisponibile), outcome, category e missing (nomi delle dipendenze non passate). Il report include summary, scenari aggregati e success. FAIL prevale su BLOCKED nello scenario; qualsiasi FAIL/BLOCKED, o un report vuoto, impedisce success.

Categorie come indizi sanitizzati: 404 inatteso sul calendario = feature flag; altri 404 = routing; 401 autenticazione; 403 autorizzazione; 400/422 validazione; 409 business logic; timeout/connessione = transport; 5xx = business logic, o database per readiness. Il solo status non distingue SQL da errori applicativi. Non si acquisiscono body o traceback per una diagnosi piÃ¹ specifica. Assertion copre verifiche di risposta/formato.

I report sono salvati prima di AuditFailure con permessi 0600, anche su setup incompleto/riuso namespace. Non contengono JWT, password, cookie, header, body, URL risolte con query, connection string, dati personali o eccezioni. L'artifact HTTP comprende solo questi JSON, non seed, chiavi o log grezzi. Se falliscono i gate host/preflight del comando http, viene salvato un report con host-preconditions FAIL e tutti i casi BLOCKED, senza contattare l'API. Per guasti precedenti allo startup restano le diagnostiche infrastrutturali esistenti.

## Controlli locali

| Controllo | Esito finale |
|---|---|
| Python unittest discover tools/test_*.py | PASS: 62 test, inclusi 14 test del nuovo motore HTTP |
| Node release-artifacts | PASS: 758 artefatti canonici, invarianti LF/CRLF |
| Node test-release-manifest | PASS: 14 controlli negativi del verificatore reale |
| Node test-mysql84 | PASS offline: adapter, additive/DDL e controlli negativi; SQL storico invariato |
| .NET Unit.Tests Release | PASS: 598 test |
| StagingCalendarFeatureTests | PASS: 2 test senza DB |
| EndpointDataSource, policy e DTO | PASS su tutti i 54 casi; legacy assenti |
| Build soluzione Release | PASS: 0 errori, 10 warning nullable preesistenti in test non modificati |
| Build StagingSupport e ReleaseTransportProbe Release | PASS: 0 warning/errori |
| CodeOrganization check | PASS: 0 violazioni |
| verify-project-artifacts.py | PASS |
| actionlint 1.7.12 | PASS sui due workflow modificati, con config locale per il runner self-hosted preesistente er-disposable-mysql57 |
| git diff --check | PASS |
| HTTP completo Docker/MySQL 8.4.13 | NON ESEGUITO localmente: Docker non disponibile; demandato a GitHub Actions |
| Suite integration completa con SQL, gare/lock DB e HTTPS pubblico EC2 | NON ESEGUITA in questa sessione |

Nessun controllo locale fallito resta irrisolto. Le regressioni Python iniettano 404 da controller escluso, 401, 403, 400/422, 500, 503 e timeout. Verificano inventario/status originari, accumulo, BLOCKED, gate, assenza di segreti anche su body/header/exception ostili, payload replay identici, ETag, nessun retry e report su errore. I mock sono confinati ai test del runner; non sono usati nel percorso CI HTTP reale.

Il test .NET importa runtime e piano Python, usa la stessa registrazione MVC di Program tramite AddApiControllers e materializza EndpointDataSource senza avviare hosted services o aprire SQL. Verifica metodo/template, controller esclusi, flag, policy, ruoli positivi e deserializzazione stretta nei DTO effettivi individuati dai parametri body delle azioni, DataAnnotations e regole pure calendario/classi/esami. Controlli negativi applicano il vero CalendarControllerFeatureProvider con flag false e assente. Questa prova non esegue autenticazione su DB, middleware o handler HTTP.

## File modificati

- infra/staging/host.py: runtime e readiness delegata al percorso audit.
- infra/staging/smoke.py: piano, dipendenze, report e gate.
- tools/test_staging.py: runtime/flag e sicurezza host.
- tools/test_staging_http.py: inventario, fault injection, dipendenze, isolamento, report e workflow.
- tests/Integration/StagingCalendarFeatureTests.cs e StagingCalendarFeatureTests.HttpContracts.cs: provider e contratti MVC/DTO reali.
- Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/DependencyInjection/ServiceCollectionExtensions.cs e Program.cs: estrazione della medesima registrazione MVC condivisa con l'audit; nessun nuovo endpoint o cambio policy.
- .github/workflows/staging-package.yml: audit reale separato, test offline, raccolta/upload sempre.
- .github/workflows/verify.yml: test contrattuali .NET portabili.
- tools/release-artifacts.cjs: nuovo test Python nella chiusura artefatti.
- migrations/release/manifest.json: esclusivamente hash/metadati degli artefatti modificati e nuovo test; nessuna modifica SQL o migrazione.
- docs/quality/STAGING_HTTP_AUDIT_REPORT.md: questo report.

Non modificati i tre file non tracciati preesistenti: MYSQL_84_MIGRATION_REPORT.md e due patch MySQL in staging-evidence. Nessuna modifica a EC2, Nginx, configurazioni Production, SQL o contratti storici.

## Rischi residui e prossima run

La run richiede checkout pubblicato pulito e nuovo STAGE_STATE/volume, come imposto dal workflow. Non sono stati eseguiti commit/push. Un timeout puÃ² lasciare una mutazione committata: si conserva l'evidenza, si bloccano i consumatori e non si ritenta la scrittura. L'audit non riprende lo stesso namespace giÃ  mutato.

Docker deve ancora attestare handler/query reali: login e generazioni IAM, TLS, calendario/replay, classi/ETag, pubblicazione, start/save/submit, voto e isolamento. L'audit preventivo non elimina ogni possibile errore runtime: il nuovo gate raccoglie nella stessa run tutti i fallimenti indipendenti raggiungibili e rende espliciti quelli non eseguibili per dipendenze mancanti.
