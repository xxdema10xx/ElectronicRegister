# ElectronicRegister - MULTITENANCY MT-4C

**MT-4C COMPLETATA: due suite consecutive 675/675 Passed, zero Failed/Skipped. Tutte le 644 identita precedenti preservate. Rollout chiuso.**

## 1. Git e baseline

Branch `refactor-database-entities`, HEAD `01702b57a19fc19079c1b7c005789ef69aa394a2`.
Workspace iniziale gia modificato da MT3/MT4A/MT4B, preservato senza reset/checkout/pull/commit/push.
Inventario iniziale in `TestResults/mt4c/initial-git-status.txt`. Il diff Git include checkpoint precedenti:
`source-delta.json` confronta invece il freeze MT4B con i file C# attuali (cinque nuovi, quindici modificati,
nessuno rimosso). Documentazione obbligatoria, schema, trigger e firme reali verificati prima degli interventi.

Baseline reale: **51 unit + 593 integration = 644/644 Passed**, zero Failed/Skipped. Identita esattamente
uguali ai TRX MT4B final-2, non soltanto conteggio o exit code (`baseline-verification.json`).
Build iniziale verde; TRX baseline separati per progetto. SDK .NET `10.0.401`, target net10.0.

## 2. Ambiente MariaDB

Solo `127.0.0.1:3306/electronic_register_test`, MariaDB `10.4.32` XAMPP Windows.
`ER_TEST_STRICT_SESSION=1` esplicito per i test; runner SQL imposta soltanto la sessione strict.
GLOBAL sql_mode originale `NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION` non modificato.
Holding `MT3A_LEGACY_TEST` Provisioning. Nessun tenant operativo attivato, nessun altro DB modificato.

## 3. Schema nuovo

`student_enrollment.revision BIGINT NOT NULL DEFAULT 0`, non negativo, shadow concurrency token EF.
Ledger `course_administration_event` con PK `(institution_id,id)`, action e riferimenti tipizzati:
CourseEdition obbligatoria, Enrollment/Campus/Class opzionali, actor User e membership, valori minimi
prima/dopo, motivo, revision e istante UTC. Il ledger dedicato copre anche le transizioni senza duplicarle
in AuditEvent. Non vengono introdotti EntityType polimorfi/fittizi o eventi platform didattici.
Schema finale: 32 tabelle, 53 trigger (48 precedenti + cinque MT4C).

## 4. Migrazioni e preflight

Script canonici in `migrations/mt4/`: `004_enrollment_audit_expand.sql`,
`005_enrollment_audit_backfill.sql`, `006_enrollment_audit_validate.sql`; runner `Invoke-Mt4c.ps1`.
Prima del DDL: database esatto/loopback, versione, schema, owner, indici/FK/trigger e stato migrazione.
Backup completo delle 31 tabelle precedenti, leggibile, con definizioni, trigger e fine dump verificati.
SHA256 `80797A2C793366040D9B231544FA1AC7CD0770E5706F7AB6EDB806E88BBC022A`.
Dump privato ignorato da Git; `backup-verification.json` conserva hash e verifiche, nessun dato sensibile.
Il restore non e stato eseguito/certificato: nessun reset o recupero distruttivo.

EXPAND eseguito una volta; rerun su stato completo/parziale rifiutato prima di mutazioni.
BACKFILL read-only rieseguito due volte: il default determina Revision0, non inventa eventi/date.
VALIDATE cinque controlli, tutti zero. Trigger SIGNAL invece di affidarsi a CHECK ignorabili da MySQL5.7.
Non sono state rieseguite preparazioni MT3C o alterati trigger MT4A.

## 5. Regole degli stati

Solo `enrolled -> completed`, `withdrawn`, `cancelled`. Ogni transizione richiede data esplicita
>= EnrollmentDate e motivo non bianco max1000. Nessun termine normativo di durata aggiunto,
nessuna data di fine dedotta da UTC, Class o CourseEdition. Terminale -> enrolled/altro terminale
rifiutato. I quattro stati tecnici gia accettati nella creazione MT4B continuano a essere validi,
anche per record terminali con CompletionDate precedentemente non nota.

## 6. Eventi append-only

UPDATE e DELETE normali del ledger sempre negati con SIGNAL. Nessuna API di modifica/cancellazione.
Eccezione privilegiata di manutenzione esclusivamente per teardown: sessione SQL root, DB esatto test,
owner esplicito nella variabile `@er_mt4c_purge_owner`, codice tenant sintetico `MT4B_` +32 hex.
La fixture mantiene la connessione aperta e azzera la variabile prima di restituirla al pool.
L'eccezione non vale per holding/tenant reali/altri database; nessun percorso HTTP imposta la variabile.
I test append-only usano normali DML senza eccezione. Il registro non e dichiarato resistente a DBA:
privilegi minimi, backup e retention sono gate operativi separati.

## 7. Ownership audit

Sei nuove FK RESTRICT: cinque composite a membership/edizione/Enrollment/Campus/Class e una globale
all'ActorUser. La FK membership ha tre colonne `(institution_id,actor_membership_id,actor_user_id)`
contro la nuova alternate key `(institution_id,id,user_id)`; impedisce anche riassegnazione dell'attore
referenziato. Le altre FK composite impediscono link cross-tenant; trigger verificano forma/risorsa
specifica e appartenenza Enrollment/edizione. Indici inventariati direttamente in information_schema.
Le sette FK MT4A e le sei composite MT4A rimangono efficaci.

## 8. Tracciabilita actor e membership

Attore e membership provengono dal TenantContext verificato, non dal payload. SQL controlla User,
membership Active, Institution Active e ruolo InstitutionAdmin all'inserimento dell'evento.
Revoche successive non riscrivono il passato. `OccurredAtUtc` imposto da `UTC_TIMESTAMP(6)` e
serializzato UTC/Z; e diverso dalla data amministrativa esplicita. Non si inventano attori legacy.
User globale non conferisce privilegi e i ruoli delle membership A/B non vengono combinati.

## 9. Transazioni

Un solo DbContext scoped e una sola transazione per mutazione: lock Enrollment owner/id FOR UPDATE,
lettura scoped, controllo intenzione/revision/policy, UPDATE owner/id/revision, INSERT evento, commit.
Nessun salvataggio audit separato e nessun secondo TenantContext/TransactionManager.
Le scritture amministrative MT4B mantengono lo stesso confine transazionale del repository.
Eccezioni e cancellazione provocano rollback tramite disposal della transazione.

## 10. Revision e concorrenza

ExpectedRevision obbligatoria; incremento esattamente1 per mutazione riuscita. Una richiesta stale
riceve409, senza ultimo-writer-vince. Overflow/contatore esaurito non viene azzerato. Trigger negano
revision iniziali nonzero, decrementi/salti e cambi stato/data senza incremento. CourseEdition mantiene
la propria Version opaca MT4B e il relativo row lock. Una Class gia attribuita non viene trasferita.

## 11. Idempotenza

EventId client identifica l'intenzione lifecycle/rettifica nella PK tenant/evento. Hash canonico include
risorsa, azione, stato/data, motivo, ExpectedRevision, attore e membership. Stesso Id/contenuto:
stesso receipt registrato, nessun evento o incremento ulteriore, anche dopo risposta HTTP persa.
Id gia usato con contenuto/attore diverso:409. Nessun retry indiscriminato. Il receipt originario puo
precedere una rettifica ulteriore: il client rilegge il dettaglio per conoscere lo stato attuale.
Lock della relazione prima della prima lettura consistente; nessun gap lock su EventId inesistente.

## 12. API nuove

Tutte sotto `/api/institution`, policy `InstitutionCourseAccess`, ruolo corrente InstitutionAdmin,
manager `IInstitutionCourseManager`, repository `ICourseStructureRepository`, flag SchoolApi && CourseApi:

| Metodo | Route | Contratto |
| --- | --- | --- |
| POST | /enrollments/{id}/transition | TransitionEnrollmentDto -> 200 EnrollmentMutationResult |
| PUT | /enrollments/{id}/completion-date | CorrectEnrollmentCompletionDto -> 200 EnrollmentMutationResult |
| GET | /enrollments/{id}/history | paginato, 200 CourseAdministrationEventDto[] |
| GET | /course-editions/{id}/audit | paginato, 200 CourseAdministrationEventDto[] |

400 input/data/motivo, 401 token, 403 ruolo/revoca/flag, 404 inesistente/fuori tenant, 409 conflitto,
413 mutazioni oltre16KiB. DTO rifiutano campi sconosciuti; nessun dettaglio SQL esposto.
Teacher/Student non leggono ragioni o attori interni; history personale ridotta rinviata.
[Contratti frontend](../../docs/api/COURSE_FRONTEND_CONTRACTS.md).

## 13. Audit operazioni MT4B

Eventi atomici: `course-edition.create`, `course-edition.update`, `campus.link`, `class.assign`,
`enrollment.create`, `enrollment.transition`, `enrollment.completion-date.correct`.
Prima/dopo descrittivi minimi, valori lifecycle tipizzati, nessun token/password/payload completo.
Attribuzione Class ripetuta alla medesima edizione e idempotente e non duplica l'evento; nessun
trasferimento. Duplicate creazioni/link restano conflitti e non lasciano audit parziali.

## 14. Rettifiche

Solo CompletionDate di un Enrollment terminale, con nuovo intento, motivo e ExpectedRevision.
Valore precedente anche null conservato, Status invariato, EnrollmentDate/owner/Student/edizione
immutabili. Stessa data corrente409. Nessuna rettifica generica, riattivazione, secondo episodio,
DELETE o eliminazione dell'evento precedente.

## 15. Compatibilita dati legacy

`legacy-before.json` e `legacy-after.json`: hash/numero righe di tutte le colonne originali delle
31 tabelle identici immediatamente prima/dopo DDL. Nessuna iscrizione o data storica derivata da
Student.ClassId, nessuna membership/User creata automaticamente, nessuna edizione fittizia.
I writer tecnici MT4A per fixture/compatibilita restano; SetEnrollmentStatusAsync segue Revision ma
non e usato dai manager istituzionali e non inventa actor. L'audit certifica i flussi amministrativi,
non una cronologia retroattiva o ogni DML di manutenzione privilegiata.

## 16. Isolamento A/B

Fixture temporanee Active A/B reali MT4B, secondo Admin A, identita globale condivisa con membership
separate, profili/studenti/edizioni distinti. GET e mutazioni fuori tenant indistinguibili da risorse
inesistenti; nessuna combinazione di ruoli. Verifiche persistite con nuovi scope/DbContext.
La barriera SQL ferma due transazioni dopo gli UPDATE A/B: entrambe detengono contemporaneamente
i propri row lock e possono proseguire, senza lock globale. Teardown elimina soltanto dati sintetici.

## 17. Test HTTP

Program.cs reale, Kestrel, JWT autentici e MariaDB, nessun mock repository/FK. Checkpoint3:
**59/59 Passed** (37 casi MT4B e 22 nuovi HTTP), zero Failed/Skipped. Coprono tre transizioni,
negazioni terminali, date/motivo/DTO/413, rettifiche, receipt dopo risposta persa, gare, audit MT4B,
ruoli, cross-tenant, revoche e flag. Stabilizzazione successiva: **42/42 Passed**, incluse tutte le
31 nuove prove, bootstrap, discovery/gate e inventario worker. I test attestano anche lo stato DB.

## 18. Test SQL reali

Checkpoint1: append-only, FK/actor e UTC **1/1 Passed**. Checkpoint2: **39/39 Passed**, cioe30 MT4A
preservati e nove nuovi SQL/rollback/concorrenza. I trigger vengono esercitati con MariaDB reale.
Metadata FK/index in `index-explain.txt`, VALIDATE a zero. Il filtro metadata MT4A usa ora il prefisso
letterale `LEFT(constraint_name,7)='fk_mt4_'`: LIKE con underscore wildcard includeva erroneamente
le nuove fk_mt4c_. Le assertion originali sulle sette FK e colonne/regole non sono state indebolite.

## 19. Rollback e fault injection

Interceptor prima e dopo l'effettivo INSERT ledger: nessuna modifica stato/data/revision e nessun
evento residuo dopo errore. Fault dopo INSERT audit per ciascuna delle cinque scritture MT4B:
creazione/descrizione edizione, ponte sede, attribuzione Class, creazione iscrizione, tutte rollbackate.
Verifiche effettuate con nuovi DbContext, non con il solo tracker. Gare: due Admin/stessa revision
un vincitore e409; stesso EventId concorrente due receipt200 e un evento; A/B senza lock globale.

## 20. Bootstrap Development/Production

Bootstrap reale in entrambi gli ambienti superato nei test di stabilizzazione. Policy autenticazione
InstitutionAccess, marker, ruoli e gate verificati dinamicamente. Nessuna modifica ai quattro flag
operativi, nessun cutover. JWT, LegacyEndpointGate, ExamManager, GradeManager, Processor e worker
non sono stati modificati in MT4C. Le prove legacy/worker sono incluse nelle suite finali.

## 21. Inventario route

Inventario dinamico: **154 MVC Production**, **159 endpoint Development** (cinque metadata),
19 action Course (15 MT4B +4 MT4C), 135 MVC precedenti a MT4B e73 legacy conservate.
`routes-Production.json`, `routes-Development.json`, `course-route-matrix.csv` e `route-verification.json`
in TestResults/mt4c riportano policy, ruoli, rollout, manager, repository, ownership e prove.
Nessuna route globale User/membership/audit aperta; prefisso URL non autorizza bypass del gate.
Alcuni test preesistenti rigenerano inventari/query nei propri percorsi mt3*/mt4b: le copie MT4C
identificano il checkpoint corrente. I TRX e i relativi hash delle suite storiche restano separati e
non sono riscritti per simulare un vecchio inventario.

## 22. Performance query

Pagine default20/max100, filtro tenant/parent prima di proiezione e materializzazione; nessun N+1,
grafo EF, filtro tenant in memoria o count aggiuntivo. History omette RequestHash e navigazioni.
Indici `(owner,enrollment,revision_after,utc,id)` e `(owner,edition,utc,id)` rispondono ai due ordinamenti.
EXPLAIN usa entrambi gli indici; il lock su PK inesistente risulta impossible WHERE nel dataset piccolo.
`course-sql.json` cattura40 comandi reali EF A/B, incluso INSERT ledger con owner/actor espliciti;
`query-observations.json` distingue capture, ispezione sorgente lifecycle e EXPLAIN. Fault/barrier
interceptor controllano comandi lifecycle reali. Nessuna certificazione di throughput/p95 su volumi
produttivi; pagine offset profonde e crescita retention vanno misurate prima del rollout.

## 23. File modificati

Cinque nuovi C#: DTO EnrollmentLifecycle, entity scaffold CourseAdministrationEvent, sidecar Mt4c,
partial CourseStructureRepository.Lifecycle e Mt4cEnrollmentTests. Quindici file C# esistenti estesi:
due controller Course, manager/interfacce/DTO, due partial repository, hook MT1 e inventory offline,
cinque file test per fixture/discovery/metadata. Lista precisa in `source-delta.json`.
Nuovi SQL004-006, runner Invoke-Mt4c e Reconcile-Mt4c; due nuovi documenti (questo report e modello
lifecycle) e cinque documenti architettura/contratto aggiornati. Nessun file precedente rimosso.
`protected-source-verification.json`: sedici file protetti identici al freeze MT4B.

Database-first: Scaffold-Mt4 produce candidato dalla DB effettiva; Reconcile-Mt4c riproduce byte per
byte nuova entity, sidecar e hook (`scaffold-reproduction.json`), senza sovrascrivere entity legacy.
Navigazione Class del solo ledger ignorata da EF; FK composite rimane SQL, generazione temporanea
Guid/owner shadow MT4A invariata. Relazioni restanti EF con DeleteBehavior.Restrict.

## 24. Test aggiunti

**31 nuovi casi parametrizzati**, nove SQL e22 HTTP, nel partial della medesima classe Mt4bHttpTests:
non sostituiscono i37 casi precedenti. Nessun test cancellato/Skip o assertion indebolita.
Identita dei644 precedenti verificate alla baseline e in entrambe le suite definitive, senza sostituzioni.
Build ad ogni checkpoint verde; build finale incrementale zero errori/avvisi (clean build precedente
con11 nullable warning preesistenti). Freeze362 sorgenti prima delle suite, verifica hash tra i run.

## 25. Due suite definitive

**Due esecuzioni complete consecutive verdi, con sorgenti invariati**:

| Esecuzione | Unit | Integration | Totale | Failed | Skipped |
| --- | --- | --- | --- | --- | --- |
| Baseline | 51 | 593 | 644 | 0 | 0 |
| Finale1 | 51 | 624 | 675 | 0 | 0 |
| Finale2 | 51 | 624 | 675 | 0 | 0 |

TRX separati per progetto/run in `TestResults/mt4c/baseline`, `final-1`, `final-2`.
`verification.json` conserva SHA256 di ciascun TRX e confronto delle identita: tutti i644 casi precedenti
sono Passed in entrambi i run, stessi31 nuovi casi, nessun Failed/Skipped. Controllati anche i singoli
outcome e total/executed/passed, non soltanto exit code. `suite-table.json` riassume i sei TRX.

`final-db-verification.json`: soltanto holding Provisioning, zero tenant/membership/profili/Campus/
edizioni/Enrollment/eventi temporanei, numero User originale15, zero violazioni owner, FK RESTRICT
preservate, 32 tabelle/53 trigger e GLOBAL sql_mode invariato. Le fixture legacy Esami mantengono le
proprie scritture di test, senza modifiche di MT4C ai relativi writer. Build, bootstrap Development/
Production, gate/route, MT4A/MT4B, worker MT3E, revoca, concorrenza e fault injection tutti verificati.
`git diff --check` e controllo whitespace/link dei nuovi file completano la validazione locale.

## 26. Rischi aperti

XAMPP/root non certifica hardening operativo o resistenza a DBA. Backup leggibile/hash non equivale
a restore certificato. Da definire retention, privilegi DB di deployment, accesso/masking motivi,
monitoraggio, volumi reali e UX di riconciliazione409. L'Idempotency receipt non sostituisce lettura
dello stato corrente. Nessuna attivazione SchoolApi/CourseApi/ExamApi/InstitutionWorker permanente.

## 27. Limitazioni storiche

Stato attuale, data iscrizione nota, data conclusione nota e sequenza di eventi MT4C sono distinti.
Assenza di eventi legacy non dimostra assenza di modifiche pregresse. Nessuna ricostruzione inventata
di actor/date/iscrizioni. Lo storico amministrativo non e una timeline completa retroattiva e non e
esposto automaticamente allo studente. Le suite legacy possono aggiornare le proprie fixture di
Esami nel DB test: il confronto invariato delle colonne originali riguarda precisamente il DDL MT4C.

## 28. Prerequisiti MT5

Decisioni separate per rientri/episodi, rettifica EnrollmentDate/stato, trasferimenti e unlink storici,
workflow CourseEdition.Status, governance identita e onboarding. Manifest legacy verificati, restore,
privilegi e release gate prima di staging/cutover. MT5 non avviato, nessuna fondazione reale creata.
[Modello dettagliato](../../docs/architecture/ENROLLMENT_LIFECYCLE_AND_AUDIT.md),
[release gate](../../docs/architecture/MULTITENANCY_RELEASE_GATE.md).
