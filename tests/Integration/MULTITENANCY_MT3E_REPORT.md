# ElectronicRegister - MT-3E

Worker istituzionale senza JWT e verifica finale di audit, recovery e isolamento. **MT-3E completata nel perimetro tecnico autorizzato: due suite 577/577 Passed. Rollout operativo chiuso, nessun cutover.** La verifica amministrativa mantiene chiuse le mutazioni non concordate; non equivale a un'amministrazione completa degli istituti. Il checkpoint tecnico, integrazione frontend, staging e produzione hanno giudizi distinti in fondo.

## 1. Git, ambiente e baseline

Branch refactor-database-entities, HEAD iniziale 01702b57a19fc19079c1b7c005789ef69aa394a2. Workspace con MT3C/MT3D non committate e cartella frontend .vscode preesistente: preservati. [Stato iniziale](TestResults/mt3e/initial-git-status.txt). Nessun reset, checkout, pull, commit o push. Letti i documenti obbligatori e verificati i sorgenti locali, senza assumere equivalenza col remoto.

[Preflight](TestResults/mt3e/preflight.txt): solo electronic_register_test loopback, XAMPP MariaDB 10.4.32, holding MT3A_LEGACY_TEST Provisioning, 40 trigger, schema MT3A gia migrato. Le FK composite MT3A sono 27, piu due dei profili. Nessun tooling di preparazione, DDL, DROP/RESET o accesso al DB operativo. GLOBAL sql_mode resta NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION. ER_TEST_STRICT_SESSION=1 riusa l'interceptor MT3D, controlla loopback/nome DB e imposta STRICT_TRANS_TABLES ad ogni apertura della sola sessione di test. Non modifica GLOBAL; le prove MT3D che controllano contemporaneamente SESSION e GLOBAL sono preservate.

Build iniziale verde; baseline eseguita realmente: **51 unit + 489 integration = 540/540 Passed**, zero Failed/Skipped. [TRX baseline](TestResults/mt3e/baseline/). Nessun test precedente rimosso, disabilitato o indebolito. Backup legacy tramite client XAMPP con opt-in MariaDB gia esistente; nessun segreto/dump personale nel report.

[Stato finale DB](TestResults/mt3e/final-db-state.txt): sola holding Provisioning, zero membership/profili/account temporanei, zero righe delle tabelle scolastiche/audit fuori holding. Restano 27 FK MT3A e 40 trigger, GLOBAL invariato anche su nuova sessione. Nessun tenant temporaneo persistente o configurazione operativa attivata.

## 2. Ricognizione e confine MT3D preservato

Confermato: GetExpiredInProgressAsync legacy filtra mt3a_legacy_scope; ProcessSubmitAsync con caller nullo ricontrolla la holding; repository bound non accetta il percorso nullo; submit-expired HTTP richiede Student proprietario e contesto MT2. Queste protezioni restano attive, incluso il test MT3D che esercita worker e null caller con flag istituzionale disabilitato.

Non sono introdotti SuperAdmin, Guid.Empty tecnico, JWT/membership/Student fittizi, un secondo TenantContext o selezione tenant da richieste HTTP. Il worker non legge User.Role e non ricicla ICurrentTenant.

## 3. Architettura e contesto tecnico

ExpiredExamSubmissionWorker resta un unico BackgroundService registrato dal Program reale. Ciclo ogni 15 secondi; lane legacy preservata e lane istituzionale separata. Un errore di scansione legacy non sopprime la lane istituzionale. Flag Multitenancy:InstitutionWorker:Enabled default false, TryParse fail-closed, esempio false; nessun appsettings operativo modificato.

La scansione usa uno scope DI breve; ogni item crea un nuovo scope/DbContext/TransactionManager e risolve IInstitutionExpiryService. SubmissionExpiryStore rilegge SubmissionId+InstitutionId e owner Exam/Student/ClassSubject dal database, esclude la holding e costruisce SubmissionExpiryWork, capability immutabile con costruttore internal accessibile solo alla persistence. Nessun costruttore pubblico, setter o binding JSON; nessuna action accetta questa capability o il servizio tecnico.

Il contesto contiene soltanto InstitutionId/SubmissionId/ExamId/StudentId/ClassSubjectId della prova. Bind crea gli stessi repository del grading con InstitutionExamDataScope tecnico distinto dal principal interattivo. Il processor ha un ingresso internal ProcessExpiryAsync che richiede corrispondenza per identita della capability col repository. Non e il vecchio ingresso con caller nullo: niente bypass generico.

## 4. Query di selezione, limiti e fairness

SQL filtra InProgress e deadline <= cutoff UTC, Institution Active/Suspended, owner coerente dei parent e assegnazione ExamStudent; esclude manifest legacy. Materializza solo ID/owner/deadline mediante proiezione AsNoTracking; nessun caricamento globale di risposte seguito da filtro in memoria. Batch massimo 50 imposto anche dallo store.

Ordine deadline, ID; cursore keyset parametrizzato, cutoff congelato per sweep. Dopo ogni item, anche fallito, il cursore avanza. Una scansione vuota azzera cursore/cutoff, il giro successivo riprova gli arretrati. Nuove scadenze non estendono indefinitamente lo sweep corrente. Stato cursore in memoria per istanza, non e una coda durevole: al riavvio si riparte dalla prima scadenza e l'idempotenza gestisce i gia consegnati. SemaphoreSlim protegge soltanto due chiamate al ciclo della stessa istanza, non serializza tenant fra processi; i test dimostrano A/B con lock submission simultanei.

Un item incoerente nelle relazioni di selezione e escluso: non viene corretto, cancellato o assegnato alla holding. Monitoraggio integrita necessario per tali arretrati. Un errore del singolo dispatch e loggato con ID tenant/submission e tipo errore, senza testo eccezione/SQL/payload nel nuovo ramo; gli altri item proseguono. Il logging legacy conserva il contratto preesistente, da proteggere mediante accessi/retention operativi.

## 5. Lock, ownership e scritture minime

La prima query nella transazione e FOR UPDATE sulla sola Submission con ID, InstitutionId, ExamId e StudentId della capability. Dopo il lock, la query bound ricontrolla i parent e l'assegnazione; il processor verifica di nuovo i riferimenti e stato/deadline persistiti. Nessun nuovo lock Exam, tenant o globale, nessun ordine inverso. LockExam e negato al contesto tecnico.

Il contesto tecnico puo leggere soltanto quella Submission, il suo Exam/struttura/risposte e il Grade collegato. Scrive solo la Submission bersaglio, un nuovo Grade per lo stesso Student/ClassSubject e l'evento submission.submit della stessa risorsa con actor nullo. Consente soltanto campi di correzione/LastSavedAt sulle risposte esistenti: testo e opzioni salvate non vengono riscritti. Add/SaveAnswers/Review sono esplicitamente negati nel repository tecnico; scadenza, inizio, revision, contatore violazioni e finalizzazione non sono modificabili dal suo scope.

Owner shadow esplicito negli INSERT e owner concurrency token nelle scritture EF preservati. SaveAsync verifica owner originale/corrente e coerenza Grade/Student/Exam/ClassSubject, non delega al trigger legacy. FK composite e 40 trigger invariati. ID A con tenant B non risolve una capability; parent/assegnazione diventati incoerenti dopo resolve falliscono sotto lock senza Grade/audit.

## 6. Sospensione e revoca

Decisione conservativa: Institution Active o Suspended puo completare tecnicamente una prova gia avviata con sole risposte salvate. Membership sospesa, ruolo rimosso o User disconnesso non cambiano questa ownership storica e non provocano perdita dati. Institution Provisioning/holding non acquisisce il nuovo percorso. Sospensione/ruoli continuano a bloccare tutte le nuove operazioni interattive MT2. Tre casi parametrizzati confermano 403 interattivo e consegna tecnica con risposte/revision preservate; nessuna nuova membership o auto-riattivazione.

## 7. Scadenza, Required, grading e recuperi

ProcessExpiryAsync non accetta DTO e usa TimeExpired, solo risposte persistite. Non aumenta revision/deadline, non crea submission, non modifica Exam.Status e non applica penalita per ExamViolation. Required vuote non impediscono la consegna: chiuse zero punti/Grade minimo secondo formula esistente; aperte PendingReview senza Grade prematuro. ConvertToGrade, PassingPercent, SuggestedGrade, Passed, review/override/finalize e intervallo 1..10 invariati.

Passed segue il voto ufficiale >=6; FailedAuto non e un'insufficienza. Test con risposte corrette e vuote e retake indipendente conservano Grade originale 10 e recupero 1 senza sostituzione/media. Close continua a fallire con InProgress anche scaduta e puo riuscire dopo l'ultima consegna. Nessuna chiusura automatica Exam.

## 8. Audit operativo

| Evento | Percorso e owner | Actor e atomicita |
| --- | --- | --- |
| exam.publish/close/cancel | Engine MT3D, scope dal contesto verificato e risorsa | User corrente, BeforeJson/AfterJson e transazione condivisa preservati |
| submission.submit interattivo | Processor bound, risorsa/profilo verificati | User corrente, evento unico nel commit grading |
| submission.submit worker | Capability della specifica submission, owner esplicito | ActorUserId null, RequestId worker:<submission-id>, BeforeJson stato precedente, AfterJson source institution-worker/reason/stato, UTC del clock server |
| submission.finalize/override | Workflow docente/admin assegnato MT3D | User corrente, before/after e motivazione preservati, Grade/audit atomici |

Nessun account tecnico inventato, owner non dedotto dall'ActorUserId e nessuna classificazione EntityType ambigua: exam_submission esplicito. Il worker riusa submission.submit e non aggiunge un secondo evento tecnico duplicato. Retry dopo commit ritorna il persistito senza nuovo evento/Grade/timestamp. Nessuna API globale audit. Proprietario storico immutabile tramite scope, concurrency token e trigger MT3A.

## 9. Transazioni, rollback e recovery

Stesso DbContext/transazione per submission, Grade e audit. ITransactionManager riceve il CancellationToken tecnico; cleanup rollback usa CancellationToken.None per non cancellare la pulizia dopo scritture gia eseguite. Il token e controllato prima del lavoro, nelle aperture/scansioni, prima delle scritture e prima del commit. Le letture condivise non accettano tutte un token: una query gia in corso puo terminare al timeout SQL prima del rollback, limite esplicito di latenza di arresto.

Fault injection tramite interceptor EF, repository reali e MariaDB: prima INSERT Grade, dopo INSERT Grade, prima INSERT Audit, dopo INSERT Audit e cancellazione dopo scrittura. Digest da nuove connessioni uguali al pre-fault per A/B; successiva elaborazione completa. Provati errore di grading dopo metadata transazionali, interruzione connessione simulata al confine apertura e vero command timeout MySql su SELECT SLEEP con timeout 1 secondo; nessuna scrittura parziale.

La connettivita e poi ripristinata con nuova connessione reale e il percorso recupera. Non e stato fermato XAMPP, simulato crash hardware, failover o perdita rete durante COMMIT: tali prove non vengono rivendicate. Commit con esito incerto richiede rilettura e retry idempotente, non assunzione di rollback. Un item fallito non blocca B; sweep successivo recupera A. Nessun retry generalizzato immediato e nessuna modifica di FailedAuto per nascondere errori.

## 10. Concorrenza e servizio reale

Worker/worker sulla stessa submission, worker/submit-expired HTTP, worker/manuale tardivo, worker/autosave tardivo: un Grade/audit e nessuna risposta/revision tardiva. Worker/review, worker/finalize e worker/Close conservano transizioni valide, niente Grade parziale. A/B elaborati parallelamente con una barriera dopo scrittura reale dimostrano che entrambi possono possedere lock submission distinti contemporaneamente e usano DbContext differenti.

Il timer del BackgroundService registrato dal Program reale e abilitato esplicitamente solo nel test: consegna senza una chiamata HTTP dello studente, poi ProgramHost attende arresto e ApplicationStopped. Cancellazione prima del ciclo lascia dati invariati; una nuova istanza recupera. Cancellazione dentro la transazione e rollback provati separatamente. I test MT3D di Start/Close/Cancel, autosave/review/finalize e tutti i test legacy restano nella suite.

## 11. Amministrazione istituzionale e identita globale

Esaminati UsersController (file UserController.cs), UserManager, UserRepository, Auth register/RegisterForAdmin, membership/ruoli/profili e risoluzione MT2. UserRepository resta globale per l'identita/bridge legacy: NON e un repository amministrativo istituzionale. Le sue operazioni di elenco/update/password/delete e la creazione account restano gated e non sono chiamate dai nuovi percorsi tecnici.

Nessuna nuova API amministrativa introdotta: scelta minima sicura del checkpoint e verificare e mantenere chiusi account globali, mutazioni membership/ruoli, onboarding e audit globale. MT3C permette gia collegamenti Student/Teacher a membership esistenti autorizzate, non concessione ruoli. MT2 permette login/selezione della propria membership, non gestione di B. InstitutionAdmin non equivale ad admin legacy.

Test con worker abilitato e JWT Admin A/B e selection ticket contro tutte le route Users e registrazione: 401/403, digest scolastici A/B e riga globale dell'utente condiviso immutati (hash confrontato soltanto in memoria, non scritto nei report). Route /api/institution/users, memberships e audit assenti. Nessuna auto-membership email o creazione pubblica di InstitutionAdmin. Governance inviti, identita verificata, ultimo admin, revoche/self-lockout e ciclo account condiviso restano decisioni di prodotto prima di implementare nuove API; rollout interessato chiuso.

## 12. Audit dinamico delle route e percorsi residui

[Inventario Production](TestResults/mt3e/routes-Production.json) e [Development](TestResults/mt3e/routes-Development.json) ricostruiti da EndpointDataSource: includono anche endpoint non MVC, metodo/route, schema/policy/ruoli, dipendenze manager/repository, accesso/scope, rollout e test. Risultati effettivi: **135 route MVC in Production; 140 route in Development, di cui 135 MVC e cinque metadata OpenAPI/Scalar**. Nessuna nuova route MT3E. [Matrice CSV completa](TestResults/mt3e/route-matrix.csv) generata dall'inventario reale; le route metadata Development sono anonime, senza accesso ai dati scolastici.

Program reale: schemi MT2/legacy e policy corretti, TenantContext scoped, gate senza bypass, worker/transaction manager/store/processor risolti con ValidateScopes e ValidateOnBuild; OpenAPI solo Development, arresto ordinato. Nessuna dipendenza scoped catturata da singleton: worker usa solo scope factory/config/logger/cursore. Nessuna API health/export globale nuova; Scalar/OpenAPI sono metadata Development, non dati didattici. Inventario SQL/background/audit in [raw-sql-background-audit-inventory.txt](TestResults/mt3e/raw-sql-background-audit-inventory.txt).

Il solo nuovo lookup cross-tenant e interno ISubmissionExpiryStore.SelectAsync; i manager HTTP non lo ricevono. Legacy query/utility/migrazioni restano presenti ma non sono aperte ai JWT istituzionali. Tutti i test MT2/MT3C/MT3D JWT/revoca/multiruolo/A-B sono preservati e rieseguiti. Nessun frontend, CourseEdition, StudentEnrollment, randomizzazione o nuova infrastruttura.

## 13. Performance e limiti misurati

[EXPLAIN/indici](TestResults/mt3e/dispatcher-explain.txt): idx_exam_submission_expiration(status,deadline_at) esistente selezionato con range sul campione locale; parent per PK/FK, anti-join manifest costante. Nessun nuovo indice speculativo. Il cursore usa anche ID, implicito nelle foglie InnoDB; prestazioni a volume/carico reale non certificate.

[SQL/misura worker](TestResults/mt3e/worker-query-observations.json): campione due submission chiuse vuote, 27 comandi EF osservati nel ramo istituzionale (scansione + risoluzione/lock/grading/audit per item). Il collector esclude le query JOIN mt3a_legacy_scope del ramo legacy e i controlli di holding; NON rappresenta il totale dei round trip, che comprende anche tali controlli, connessioni/handshake/commit e SET SESSION solo test. Nessun benchmark produttivo dichiarato.

Memoria selezione O(50); una struttura esame/risposte per item, nessun grafo globale. Una scansione e un nuovo scope per item, connessioni pool e transaction per submission, elaborazione sequenziale per istanza del timer; piu istanze possono concorrere. Query per item intenzionali per rivalidare owner e transazione, nessun N+1 per risposta. Retry sweep ripete verifiche di sicurezza senza cache revocabili. Esami/risposte molto grandi, throughput seriale e ritardo di poll richiedono dimensionamento; nessun Redis/coda esterna.

## 14. File e validazione

Nuovi: SubmissionExpiryWork e contratti tecnici Domain; InstitutionExpiryService Application; SubmissionExpiryStore Infrastructure; Mt3eWorkerTests partial riusa la fixture MT3D; questo report e release gate. Modificati: DI Application/Infrastructure, processor (solo nuovo ingresso tecnico/audit/check stato), scope/repository submission, worker, cleanup TransactionManager, flag esempio, dichiarazione partial del test MT3D. Formule/servizi di grading e frontend invariati. Documenti Design/Isolation Matrix/contratto frontend aggiornati.

37 nuovi casi integration, nessun nuovo unit necessario per prove che devono dimostrare SQL/transazioni reali. Targeted-6: 41/41 (37 nuovi e quattro legacy worker). Targeted precedenti conservati, inclusi errori fixture iniziali (B non selezionata, aspettativa null su accessor fail-closed, nome Users plurale) e permesso tecnico inizialmente troppo ristretto sui campi di correzione delle risposte; corretti senza indebolire assertion di sicurezza.

Build finale verde, zero errori. Due suite complete consecutive, senza modifiche al codice tra le esecuzioni:

| Run | Unit | Integration | Totale Passed | Failed | Skipped |
| --- | ---: | ---: | ---: | ---: | ---: |
| Baseline | 51 | 489 | 540 | 0 | 0 |
| final-1 | 51 | 526 | 577 | 0 | 0 |
| final-2 | 51 | 526 | 577 | 0 | 0 |

TRX distinti per progetto in [final-1](TestResults/mt3e/final-1/) e [final-2](TestResults/mt3e/final-2/). [Verify-Evidence.ps1](TestResults/mt3e/Verify-Evidence.ps1) ha confrontato contatori reali, outcome, nomi univoci e insiemi dei test: **tutti i 540 precedenti preservati**, 37 nuovi, stessi insiemi nelle due suite. [verification.json](TestResults/mt3e/verification.json) conserva esiti e SHA256 dei sei TRX. Nessun risultato dedotto dal solo exit code.

git diff --check e controlli whitespace sui nuovi sorgenti/documenti superati; [evidenza](TestResults/mt3e/git-diff-check.txt), [stato Git finale](TestResults/mt3e/final-git-status.txt). Nessuna modifica frontend o ai servizi/formule di grading, nessun commit/push. Warning nullable preesistenti osservati nella ricompilazione; build finale incrementale senza warning.

## 15. Release gate, rischi e verdetto

[Checklist completa e recovery](../../docs/architecture/MULTITENANCY_RELEASE_GATE.md). Holding Provisioning preservata; default false in tutti i flag; nessun cutover automatico. Gate ambientali aperti: restore/RPO/RTO certificati, manifest di dati reali, TLS/proxy, CORS AllowAll preesistente, rate limiting generale, segreti/rotazione, alerting backlog/integrita, carico e policy amministrative istituzionali. Questi non si risolvono attivando un flag.

A. **MT-3E completata nel perimetro tecnico autorizzato**: worker offline A/B, capability per item, audit, rollback/recovery applicativo e verifica delle route/gestione utenti. La verifica amministrativa e completata mantenendo chiuse le mutazioni non concordate, come previsto dal requisito di non inventare policy di prodotto; non si dichiara implementata un'amministrazione completa degli istituti o un restore operativo certificato.

B. Backend per ulteriori integrazioni frontend: API documentate utilizzabili in test controllato; worker offline solo con flag esplicito e validato, non per effetto del JWT o dei flag HTTP.

C. Staging controllato: candidato tecnico; installazione non certificata pronta senza gate ambientali, nessun ambiente staging effettivo verificato qui.

D. Produzione: **NON pronto al rollout**. Nessun cutover autorizzato, onboarding completo o promessa di recovery hardware. Se un futuro audit trova una route globale didattica raggiungibile con JWT scoped, e un blocco di rilascio indipendente dai contatori verdi.
