# ElectronicRegister - MT-4B

**MT-4B completata nel perimetro autorizzato: due suite consecutive 644/644 Passed,
zero Failed/Skipped; tutti i 607 test precedenti preservati e 37 nuovi Passed.
Rollout chiuso, nessun cutover o MT-4C.**

## 1. Git iniziale

Branch refactor-database-entities, HEAD 01702b57a19fc19079c1b7c005789ef69aa394a2.
Workspace con checkpoint MT3/MT4A non committati: preservati.
[Stato iniziale](TestResults/mt4b/initial-git-status.txt). Nessun reset, pull, commit o push.

## 2. Baseline e preflight

Baseline eseguita: **51 unit +556 integration =607/607 Passed**, zero Failed/Skipped.
Identita esattamente uguali a MT4A final-2: [verifica](TestResults/mt4b/baseline-verification.json).
[Preflight](TestResults/mt4b/preflight.txt): electronic_register_test loopback, MariaDB 10.4.32,
holding MT3A_LEGACY_TEST Provisioning, 48 trigger (40 MT3A +8 MT4A). Nuove tabelle MT4A gia
presenti. Nessun DDL o migrazione richiesti; nessun backup/DROP/reset/restore eseguito in MT4B.
STRICT_TRANS_TABLES tramite ER_TEST_STRICT_SESSION=1 sulle sole sessioni test. GLOBAL invariato.
Tooling backup dei test legacy usa il client XAMPP con opt-in MariaDB preesistente. Logging EventLog
disabilitato solo nel processo test per evitare il sink Windows non accessibile nel sandbox.

## 3. Modello esistente

Institution e fondazione/tenant; Campus sede locale; StudyPath definizione; CourseEdition concreta;
Class gruppo con BienniumStudyPath obbligatorio e CourseEditionId nullable; Enrollment rapporto
individuale. Student.ClassId resta assegnazione corrente. Nessuna equivalenza Biennium/edizione,
nessun backfill storico, nessun account richiesto per iscrivere uno Student locale.
Letti report MT4A/MT3E, modello ITS, release gate, design, isolamento e contratto esami; schema,
trigger e mapping reali verificati. [Schema/trigger](TestResults/mt4b/schema-trigger-verification.txt).

## 4. Endpoint effettivi

15 nuove action: 10 CourseEdition, 1 Campus, 4 Enrollment. Route e contratti completi in
[COURSE_FRONTEND_CONTRACTS](../../docs/api/COURSE_FRONTEND_CONTRACTS.md).
Nessuna route amministrativa User, Membership, Audit o stato didattico aggiunta.

## 5. Manager

IInstitutionCourseManager / InstitutionCourseManager riusa InstitutionSchoolAuthorization e
IInstitutionProfileResolver. Ogni metodo ricava ICurrentTenant e rivalida IInstitutionAuthManager;
manager non riceve owner dal client. Admin sulle operazioni amministrative, Student sul proprio
storico soltanto. Controller sottili, nessun DbContext o ClaimsContext legacy nei nuovi percorsi.

## 6. Repository

ICourseStructureRepository MT4A esteso, stessa registrazione scoped e stesso DbContext. Nuovo
partial Queries senza duplicare repository. Proiezioni dedicate AsNoTracking, owner in SQL,
filtri e Skip/Take lato server. Vecchi contratti MT4A conservati per compatibilita; il metodo interno
SetEnrollmentStatusAsync non viene esposto dal manager/API.

## 7. Autorizzazioni

InstitutionCourseAccess richiede unico schema InstitutionAccess, membership/Institution Active,
ruoli esatti rivalidati e CourseApi && SchoolApi. Nessun ruolo globale User. Teacher non ha accesso
amministrativo. Student richiede ruolo e profilo della membership attuale. Gate rifiuta marker
combinati/duplicati, AllowAnonymous, schemi o policy alternative; nessun bypass per prefisso URL.
JWT validation MT2 invariata: sola nuova registrazione policy. Nuovo flag server-side default false.

## 8. CourseEdition

Create: owner server, StudyPath scoped, code64/name200 non bianchi, date facoltative ordinate,
status tecnico esplicito fra i quattro MT4A. Update sostituisce soltanto Code/Name/date e richiede
Version; owner/path/status/CreatedAt non ammessi. By-code e filtro StudyPath scoped, codici locali
uguali A/B, unicita locale DB case-insensitive. Nessuna attivazione/completamento dedotta dalle date.

## 9. Campus

Consultazione paginata owner/name/id, campi amministrativi esistenti e IsActive nullable preservato.
Link con parent scoped e INSERT owner esplicito, FK e unicita SQL. Nessuna sede primaria o unlink;
inattivita non elimina link. Non viene inventato un workflow di ammissibilita basato sugli stati.

## 10. Class

Prima attribuzione esplicita mediante repository MT4A: owner, BienniumStudyPath/StudyPath coerenti,
CourseEditionId shadow concurrency token e trigger di immutabilita. Stessa attribuzione idempotente;
altra edizione 409. Percorso incompatibile/parent esterno 404. Legacy NULL rimane valido; nessuna
modifica a Student.ClassId/ClassSubject/Exam/Grade. Procedura di correzione ancora da concordare.

## 11. StudentEnrollment

Create con data/stato espliciti, riferimenti scoped e owner da TenantContext. UNIQUE tenant/student/
edition mantenuta anche dopo cessazione; edizioni distinte consentite. Letture per Student e per
edizione, dettaglio Admin, /me da profilo corrente. DTO personale senza StudentId. Nessun User,
Membership o profilo creato; nessuna derivazione da ClassId e nessuna data retroattiva inventata.

## 12. Storico e audit

Enrollment mantiene data nota, eventuale conclusione e stato corrente; non e uno storico append-only.
Audit MT3A ammette exam/exam_submission e platform senza dati didattici: non supporta questi nuovi
tipi. Nessun EntityType fittizio, nessun evento globale non attribuibile. Creazioni/descrizioni non
producono ancora audit amministrativo completo; il limite e esplicito nel contratto. Non si inventano
actor/date legacy. Ogni nuova scrittura ammessa e atomica; non si rivendica atomicita con un audit
non disponibile. Policy e rappresentazione audit precedono l'apertura delle rettifiche sensibili.

## 13. Operazioni deliberate chiuse

Nessuna route per transizioni, riattivazioni, secondo episodio nella stessa edizione, rettifica date
Enrollment, trasferimento Class, unlink Campus o DELETE fisico. Nessun MT4C/onboarding iniziato.
Status tecnici espliciti accettati soltanto in creazione; letture gestiscono tutti i valori persistibili.

## 14. Writer e mapping

INSERT CE/Enrollment/ponte esplicitano InstitutionId. Le nuove API usano InsertEditionAsync e
InsertEnrollmentAsync: singoli INSERT parametrizzati senza rilettura EF dei timestamp per sola PK.
Validazioni e precheck sono condivisi coi writer MT4A Add*, preservati per compatibilita. I timestamp
restano default DB e il successivo GET usa owner/id. Il ponte non ha timestamp generati da rileggere. UPDATE descrittivo usa owner+Id nel predicato,
senza scrivere owner/path/status. Class usa shadow owner esistente e owner concurrency token;
concorrenza CourseEditionId preservata. TemporaryGuidValueGenerator MT4A e scaffold/sidecar non
modificati. Nuove entity generate e vincoli SQL MT4A invariati. Errori conflitto tradotti 409 soltanto
sulla nuova superficie mediante CourseApiErrors, senza esporre SQL o cambiare il modulo esami.
[Verifica sorgenti protetti](TestResults/mt4b/protected-source-verification.json).

## 15. Query SQL

Proiezioni CE per owner/code/path con ordinamento Code/Id; Enrollment con join composite owner/Id
all'edizione e filtro Student o edizione; Campus con EXISTS scoped sul ponte; Class con shadow owner
ed editionId. Nessun SELECT * nelle query nuove, nessun caricamento globale prima del filtro.
Controlli esistenza e autorizzazione aggiungono round trip costanti, non N+1 per riga.
[Osservazioni](TestResults/mt4b/query-observations.json): 32 comandi contenenti riferimenti Course
nel campione misto A/B, escluse identita/autorizzazioni, clock e overhead sessioni/transazioni;
nessuna misura del totale round trip o garanzia di throughput.
[Comandi catturati](TestResults/mt4b/course-sql.json): INSERT owner esplicito, UPDATE descrittivo
owner/id e UPDATE Class owner/concurrency token, WHERE scoped prima del paging, nessun SELECT *.
Un SELECT scalare CURRENT_TIMESTAMP(6) senza lettura di risorse mantiene il clock DB dell'update.

## 16. Concorrenza

Unicita SQL decide le gare su Code, Student/edizione e Campus/link. Class usa concurrency token e
trigger immutabile. Update descrittivo: transazione con SELECT id WHERE owner/id FOR UPDATE,
rilettura proiettata sotto lock e confronto Version SHA256 opaca dell'intero snapshot tecnico;
UPDATE solo descrittivo con owner/id, UpdatedAt dall'orologio della sessione DB (coerente coi
default MT4A) e monotono alla precisione microsecondi. Version non
e una credenziale. Version obsoleta 409; nessun retry indiscriminato. Nessun lock globale/tenant.
Test simultanei con due Admin distinti e nuovi scope HTTP, piu letture/scritture A/B.

## 17. Test A/B

Mt4bHttpTests avvia Program reale con Kestrel, JWT reali e MariaDB. Due istituti temporanei Active,
percorsi/sedi/classi/studenti distinti, utenti condivisi con membership separate e secondo Admin A.
Nuovi DbContext per riletture, digest dei dati scolastici/account/membership e teardown per ID/owner.
Prove positive, cross-tenant/missing indistinguibili, unicita, path, campus molti-a-molti, assegnazione
esplicita, enrollment indipendente, /me separato, Student senza account, stati tecnici e date.

## 18. JWT e revoca

Ogni action amministrativa attraversata con Teacher/Student, flags chiusi e JWT revocato. Membership
sospesa, Institution sospesa e ruoli rimossi dopo emissione token negano nuove operazioni. Selection,
legacy e anonimo respinti. Profilo A mancante non usa B. Stesso User seleziona storici distinti.
Marker combinati, policy/schema alternativi e AllowAnonymous testati anche con contesto inizializzato.

## 19. Bootstrap

Nuove prove Development/Production con Program reale, worker registrato e arresto ordinato tramite
host riutilizzato MT3C. OpenAPI presente soltanto Development. Nessun flag operativo attivato.
Prove bootstrap nuove e precedenti superate nei targeted-3/4; controller Course policy corretta,
nessun endpoint tecnico worker aggiunto. Inventari: [Development](TestResults/mt4b/routes-Development.json),
[Production](TestResults/mt4b/routes-Production.json).

## 20. Inventario route

Inventario da EndpointDataSource, non dedotto da 135. Nuove action con metodo, route, policy,
ruolo, flag, manager, repository, owner e test positivo/negativo. Le prove precedenti conservano
le 73 legacy, 24 School, 33 Exam e 135 MVC precedenti; aggiunta categoria Course separata di 15.
Nessuna assertion originale di ruolo/scope rimossa; conteggi estesi all'inventario nuovo.
Inventario reale: **150 route MVC Production; 155 route Development, incluse cinque metadata**.
Le 15 nuove route sono amministrative salvo GET /enrollments/me Student. Nessun DELETE nuovo.

## 21. Performance

Tutte le liste max100/default20, paginazione esplicita deterministica; nessun count incluso. Indici
MT4A tenant/code, tenant/path, tenant/student/edition, tenant/edition, ponte reverse e Class/edition
riutilizzati. [EXPLAIN e inventario indici](TestResults/mt4b/index-explain.txt) sul DB test con
campione vuoto/ridotto: optimizer sceglie anche il solo prefisso owner e filesort, non si rivendica
uso forzato dell'indice piu selettivo o una misura di carico. Nessun indice speculativo, Redis o filtro in memoria. Ordinamento puo richiedere filesort
su liste scoped, misure su fixture piccole non sono benchmark/carico produttivo. Offset non garantisce
snapshot tra pagine in presenza di modifiche concorrenti. Version calcolata soltanto sulle righe gia
scoped e paginate. Query authorization incluse nelle osservazioni, dati/parametri personali non esposti.

## 22. Test aggiunti

Nuova suite Mt4bHttpTests; host MT3C esteso con parametro CourseApi facoltativo. Inventory MT2,
AuditBootstrap, MT3D/MT3E estesi conservando garanzie precedenti. Nessun test cancellato o Skip.
**37 nuovi casi**. targeted-1: 13 Passed/24 Failed per filtri/ordinamenti applicati dopo una proiezione
record non traducibile; corretti prima della proiezione, senza filtro in memoria. targeted-2: 37/37
Passed, zero Failed/Skipped. Successiva verifica dei writer elimina il readback EF per sola PK e
allinea il clock dei timestamp alla sessione DB; **targeted-3: 81/81 Passed**, zero Failed/Skipped,
include MT4A, bootstrap, gate legacy e worker recovery. Gli scenari worker legacy chiamano
ResetScenarioAsync e modificano intenzionalmente sei tabelle Esami della fixture (audit_event, exam,
exam_student, exam_submission, grade, submission_answer): i digest globali dopo una suite che li
include non sono una misura valida di assenza di scritture del solo modulo MT4B. Nessun loro test
e stato cambiato. targeted-4 isola nuovamente i 37 nuovi casi con digest completi prima/dopo per
certificare che le nuove API non alterino quei dati. **targeted-4: 37/37 Passed**, zero Failed/Skipped;
[verifica completa](TestResults/mt4b/module-data-verification.json), [prima](TestResults/mt4b/persisted-module-before.json)
e [dopo](TestResults/mt4b/persisted-module-after.json): tutte le colonne/righe delle **31 tabelle identiche**. Le suite complete conservano inventari finali e
verifiche ownership/assenza residui; non viene rivendicata uguaglianza byte-per-byte degli scenari
Esami deliberatamente resettati dai test precedenti.
Un tentativo di build durante la baseline ha incontrato DLL bloccate dal testhost: compilazione
isolata riuscita, build normale ripetuta dopo la fine della baseline. Nessuna modifica alle assertion
di sicurezza per far passare le prove.

## 23. Due suite definitive

Build finale completa verde, zero warning/errori. Due suite complete consecutive, senza modifiche
ai 357 sorgenti tra le esecuzioni (solo documentazione aggiornata):

| Run | Unit Passed | Integration Passed | Totale Passed | Failed | Skipped |
| --- | ---: | ---: | ---: | ---: | ---: |
| Baseline | 51 | 556 | 607 | 0 | 0 |
| final-1 | 51 | 593 | 644 | 0 | 0 |
| final-2 | 51 | 593 | 644 | 0 | 0 |

[TRX baseline](TestResults/mt4b/baseline/), [final-1](TestResults/mt4b/final-1/),
[final-2](TestResults/mt4b/final-2/), distinti per progetto e run. Durata integration 17m26s e
26m51s rispettivamente; la seconda esecuzione piu lenta e comunque integralmente verde.
[Verify-Evidence.py](TestResults/mt4b/Verify-Evidence.py) legge contatori, outcome e identita:
**607 precedenti identici/preservati, 37 nuovi, stesso insieme di 644 nei due run**, nessun Failed
ne NotExecuted. [Verifica e SHA256 dei sei TRX](TestResults/mt4b/verification.json).
Non e un successo dedotto dal solo exit code. [Hash sorgenti](TestResults/mt4b/frozen-source-hashes.json)
ricontrollati prima/dopo i run; nessun test cancellato, Skip o assertion indebolita.

[Stato finale DB](TestResults/mt4b/final-db-verification.json): sola holding MT3A_LEGACY_TEST
Provisioning, zero Campus/edizioni/link/iscrizioni/membership/ruoli/profili temporanei; owner scolastici
e audit coerenti, sette FK MT4A RESTRICT (sei composite), 48 trigger. GLOBAL sql_mode originale
invariato. [VALIDATE SQL](TestResults/mt4b/validate-final.txt): **sette controlli, zero violazioni**.
Il digest integrale dei soli 37 test MT4B certifica tutte le 31 tabelle identiche prima/dopo; le suite
complete conservano snapshot separati e modificano i propri scenari legacy secondo i test
preesistenti, come spiegato nella sezione 22. [Teardown dopo run 1](TestResults/mt4b/final-1-cleanup-verification.json).

[Quattro flag false](TestResults/mt4b/rollout-verification.json) nell'esempio versionato; abilitazioni
soltanto negli host di test. Bootstrap reale Development/Production e inventario completo superati.
[Matrice delle 15 nuove route](TestResults/mt4b/course-route-matrix.csv) con policy, ruoli, rollout,
manager/repository, owner e test positivo/negativo. Nessuna nuova route globale o apertura legacy.
Git diff --check e controllo UTF-8/whitespace nuovi file superati; stato Git finale conservato.
Nessun commit/push, frontend o modulo Esami/worker modificato da MT4B.

## 24. Rischi aperti

Audit amministrativo nuovi tipi non implementato; workflow stato/correzione ancora chiuso. Dati storici
legacy incompleti. Load/restore/TLS/CORS/rate limiting/segreti/monitoraggio/gov membership restano
gate ambientali MT3E; suite locale non autorizza produzione. Concorrenza delle sole descrizioni
API controllata, non promessa di event sourcing o workflow stato generale.

## 25. Decisioni funzionali necessarie

Transizioni/prerequisiti/ruoli/motivazioni; rettifica e audit append-only attribuito; reingresso stesso
rapporto o episodi; correzione Class gia attribuita; unlink e intervalli Campus; manifest verificato per
legacy e relativa provenienza. Non si impongono limiti di durata o divieti di percorsi simultanei.

## 26. Prerequisiti modulo successivo

Concordare policy sospese e supporto audit con owner/actor/atomicita prima di esporre flussi sensibili;
verificare manifest e deployment separatamente. Preservare dati legacy, FK/trigger, isolamento e
rollout chiuso. MT-4C/onboarding/cutover non fanno parte di questo checkpoint.
