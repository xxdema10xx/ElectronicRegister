# MT-5B-R1 — ricostruzione SQL candidata

21 ottobre 2026. **Nuova implementazione candidata**, non recupero degli originali
e non certificazione di equivalenza. Nessuna connessione SQL, scrittura SQL,
migrazione, suite .NET o avvio di istanza eseguiti in questo checkpoint.
La baseline MT4C sulla 3306 non è stata modificata.

## Consegna e provenienza

Pacchetto separato: [migrations/mt5/candidate-r1](../../migrations/mt5/candidate-r1/README.md).
Contiene i sei SQL richiesti, [manifest nuovo](../../migrations/mt5/candidate-r1/manifest-r1.json),
[controllo statico riproducibile](../../migrations/mt5/candidate-r1/verify_static.py)
e [risultato statico](../../migrations/mt5/candidate-r1/static-review.json).
Il runner esistente cerca gli SQL nella propria directory: **non carica questi
candidati**, non è stato modificato né eseguito. Non spostare i candidati prima
della revisione e dell'approvazione del successivo checkpoint.

Fonti effettivamente consultate, con ricerca mirata:

| Fonte | Evidenza utilizzata |
| --- | --- |
| migrations/mt5/README.md e Run-Mt5bPersistence.ps1 | Sequenza 000–005, cardinalità risultati 7/6/2, target isolato, manutenzione, backup, commit impliciti |
| MULTITENANCY_MT5B_PERSISTENCE_REPORT.md | Contratto generation, errore 1442, retention, separazione tecnica/IAM, limiti storici |
| MULTITENANCY_MT4C_NEW_MAMP_RESTORE.md | Baseline nuova MAMP già verificata e backup finale; non una nuova verifica SQL in R1 |
| TestResults/mt5b-persistence/final-migration-manifest.json | Hash degli originali perduti, preservati |
| TestResults/mt5b-persistence/final-new-triggers.json | Nomi/tabella/timing/evento degli 11 trigger storici; non contiene i corpi |
| Mt5bSecurityStateTests.cs | SQL reali attesi, errori 1451/1452/1644, overflow, no-op, owner, backfill, concorrenza e confini tecnici |
| MembershipFixturePurge.cs | Root/owner di sessione, porta e suffisso datadir verificati; ripristino capability |
| InstitutionMembershipSecurityState.cs e ElectronicRegisterContext.Mt5bSecurity.cs | Guid/long, PK, UNIQUE, FK e charset/collation/nome oggetti |
| InstitutionMembershipRepository.cs | Transazioni, mutex Institution e Provisioning su AddAsync/AddRoleAsync/SuspendAsync |
| InstitutionMembershipSecurityStateRepository.cs | Reader scoped AsNoTracking, assenza/negatività negate, nessun fallback zero |
| Persistence/Migrations/Mt5bSecurityStateIntegrity.cs | Inventario esatto degli 11 nomi e fk_membership_security_owner, fail-closed offline |
| ElectronicRegisterContext.Multitenancy.cs, migrations/mt1/001_expand.sql e mt3/001_profile_expand.sql | Identità principal, enum, PK globale e UNIQUE owner-aware preesistente |
| SQL canonici mt3/mt4, inclusi 003/004/005/006 MT4 | Inventario statico dei 53 nomi trigger preesistenti, dipendenze e validator da preservare |
| docs/architecture/INSTITUTION_MEMBERSHIP_MUTATIONS_AND_AUDIT.md | Contratto applicato checkpoint B, limiti MySQL, audit/LastAdmin/JWT futuri |
| Fixture Mt1Integrity, Mt2Identity, Mt3Profile, Mt3aOwnership, Mt3bSchool, Mt3cHttp, Mt3dHttp, Mt4aCourseStructure, Mt4bHttp, Mt5aMembershipAdmin | Soli punti di codice sintetico e teardown: prefissi osservati e rehearsal holding |
| Mt3aOwnershipMigration.cs, ricerca mirata | Inventario offline e distinzione holding/rehearsal; nessun mutatore nuovo |

I percorsi C# sono sotto tests/Integration o sotto
Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/.
Non sono stati riesaminati indiscriminatamente i report MT1–MT5A. Nessun dump
contenente dati è stato letto per ricostruire i corpi SQL. Nessuna fonte esterna
usata per inventare dettagli dell'implementazione storica.

## Contratto e oggetti candidati

Una sola tabella nuova, nessuna nuova tabella User, routine, ledger o route:

```text
institution_membership_security_state
  institution_id      CHAR(36) ASCII ascii_general_ci NOT NULL
  membership_id       CHAR(36) ASCII ascii_general_ci NOT NULL
  security_generation BIGINT signed NOT NULL DEFAULT 0
  PRIMARY KEY (institution_id, membership_id)
  UNIQUE uq_membership_security_identity (membership_id)
  FK fk_membership_security_owner (institution_id, membership_id)
    -> institution_membership (institution_id, id), UPDATE/DELETE RESTRICT
  ENGINE InnoDB, utf8mb4_unicode_ci
```

La FK usa `uq_membership_institution_id`, già canonica MT3: nessuna ALTER della
membership per adattarla ai nuovi file. Ownership resta quella della stessa
Fondazione ITS; User condiviso non conferisce privilegi né identità tenant nuove.
La non negatività non si affida a CHECK ignorati da MySQL 5.7: INSERT richiede 0,
UPDATE vieta NULL, negativo, diminuzione e cambio di identità della sidecar.

| File | Effetto futuro previsto |
| --- | --- |
| 000_preflight.sql | Sette regole SELECT, tutte zero: ambiente/sessione, oggetti predecessori, contratto principal, trigger conservati, integrità membership, holding, confine installazione |
| 001_security_state_expand.sql | Una CREATE TABLE senza IF NOT EXISTS e dieci CREATE TRIGGER; rifiuta installazione presente/parziale prima della prima DDL |
| 002_security_state_backfill.sql | Un solo INSERT SELECT anti-join degli stati mancanti; non modifica righe esistenti, incluse generation nonzero |
| 003_security_state_validate.sql | Sei regole SELECT zero: missing-state, orfani, generation, schema/chiavi/FK, forme trigger core e dipendenze membership |
| 004_membership_delete_error_contract.sql | Undicesimo trigger additivo, senza DROP; SIGNAL 23000/MYSQL_ERRNO 1451 prima della guardia core |
| 005_delete_contract_validate.sql | Due regole SELECT zero: presenza/forma contratto errore e ordine esatto dei due BEFORE DELETE |

000 è utilizzabile su predecessore pulito o installazione completa, non come
approvazione di uno stato intermedio. Il driver deve richiedere i conteggi zero;
un exit code client 0 non certifica l'esito dei validator. 003 non dimostra da sola
l'equivalenza dei corpi trigger: nel prossimo checkpoint occorre confronto integrale.

### Trigger e interazioni

| Nome mt5b_* | Tabella | Timing/evento | Responsabilità |
| --- | --- | --- | --- |
| security_insert | sidecar | BEFORE INSERT | Generation iniziale zero; ownership lasciata alla FK, errore 1452 conservato |
| security_update | sidecar | BEFORE UPDATE | Identità e monotonicità; avanzamento diretto consentito, mai diminuzione |
| security_delete | sidecar | BEFORE DELETE | Retention; sola eccezione root/fixture verificata |
| membership_security_insert | membership | AFTER INSERT | Crea stato zero nella stessa transazione |
| membership_identity_update | membership | BEFORE UPDATE | Vieta cambi Id/InstitutionId/UserId |
| membership_security_update | membership | AFTER UPDATE | Nega missing-state anche no-op; incrementa soltanto per cambio status |
| membership_security_delete | membership | BEFORE DELETE | Retention; rimuove sidecar solo per fixture autorizzata prima del parent |
| role_identity_update | role | BEFORE UPDATE | Vieta rekey membership_id/role; no-op ammesso solo con stato presente |
| role_security_insert | role | AFTER INSERT | Incremento owner-aware per inserimento effettivo |
| role_security_delete | role | AFTER DELETE | Incremento owner-aware per rimozione effettiva, anche durante teardown |
| membership_delete_contract | membership | BEFORE DELETE PRECEDES membership_security_delete | Errore 1451 per cancellazione operativa |

In questa tabella sidecar = institution_membership_security_state, membership =
institution_membership, role = institution_membership_role. Nomi completi nel SQL
e nel manifest; tutti gli 11 coincidono con l'inventario C# e le forme storiche.

Nessuno dei 53 trigger canonici precedenti ha nome coincidente o insiste su queste
tre tabelle: controllo statico sui nomi/target, non semplice sottrazione 64−53.
Le due guardie DELETE sono le sole della medesima coppia timing/evento su membership;
004 usa PRECEDES e 005 richiede ACTION_ORDER 1/2. Il guard identity BEFORE UPDATE
precede naturalmente il security AFTER UPDATE. I trigger MT4C del ledger restano
immutati e continuano a poter bloccare una cancellazione tramite FK RESTRICT.

I tre incrementi usano UPDATE atomico della sidecar con condizione
`0 <= generation < 9223372036854775807`, seguito da ROW_COUNT()=1 obbligatorio.
Nessuna lettura di generation seguita da aggiornamento del valore calcolato nel
client, nessun wrapping. I trigger dei ruoli leggono solo il parent immutabile,
non lo aggiornano: la scelta rispetta le forme INSERT SELECT/DELETE JOIN presenti.
L'INSERT della sidecar non legge né modifica altre righe sidecar: consente il
backfill anti-join. Errori SIGNAL in trigger InnoDB devono annullare lo statement;
commit/rollback e assenza di incrementi persi sono da verificare realmente.

I writer tecnici C# mantengono il mutex Institution e ammettono solo Provisioning.
Non aggiungiamo un vincolo SQL globale «solo Provisioning» che impedirebbe le
fixture privilegiate e i futuri writer IAM: la separazione è nel repository già
versionato. Nessun attore umano o evento storico viene inventato. I futuri
LastAdmin, audit, ExpectedRevision/EventId e revalidation restano fuori scope.

## Matrice di tracciabilità

Tutti i test elencati sono **sorgenti esistenti, non eseguiti** in R1.
Abbreviazioni: T = Mt5bSecurityStateTests; R = README/report persistence/architettura;
EF = entity + mapping; W = InstitutionMembershipRepository; P = MembershipFixturePurge.

| Requisito | Fonte che lo dimostra | Implementazione candidata | Test esistente / limite |
| --- | --- | --- | --- |
| Ownership ITS e identità unica | EF, T, Integrity C# | PK composita, UNIQUE globale MembershipId, FK RESTRICT | T.Sql_InitialStateAndOwnerArePersistent; T.Sql_CompositeForeignKeyRejectsWrongOwnerAndOrphanAfterPrivilegedFixturePurge; T.MappingHasCompositeOwnerRestrictForeignKeyAndNoNavigationGraph |
| Generation iniziale 0 | EF, T | security_insert + membership_security_insert | T.Sql_InitialStateAndOwnerArePersistent |
| BIGINT signed non negativo | long EF, reader >=0, R | BIGINT + insert/update guard | Nessun caso negativo INSERT dedicato nel T attuale: nuova prova futura necessaria |
| Ruolo inserito/rimosso incrementa una volta | T, R | role_security_insert/delete, UPDATE atomico scoped | T.Sql_RoleAddRemoveRestoreNeverRestoresGeneration |
| Stato roundtrip monotono | T, R | membership_security_update | T.Sql_StatusRoundtripAndNoopAreMonotone |
| No-op/duplicato non incrementa | T | AFTER solo INSERT/DELETE effettivi, rekey no-op senza incremento, status comparato | T.Sql_InsertSelectMultirowAndDuplicateNoopRemainCompatible; T.Sql_StatusRoundtripAndNoopAreMonotone |
| SuspendedAt isolato non è revoca | R, W | Incremento condizionato solo a status | Nessun test diretto dedicato nel T attuale |
| Rollback non persiste incrementi | T, InnoDB canonico | Trigger nella transazione DML InnoDB, niente COMMIT nei trigger | T.Sql_RollbackPreservesGenerationAndBothTenants |
| Overflow blocca senza reset | T, R | Tre UPDATE limitati + SIGNAL 1644 se ROW_COUNT diverso da 1 | T.Sql_OverflowRollsBackTheMutationWithoutReset; DELETE ruolo a max richiede prova aggiuntiva |
| Nessuna diminuzione | T, R | security_update | T.Sql_IdentityResetAndOperationalDeleteAreRejected, caso generation |
| Identità membership immutabile | T, R | membership_identity_update con confronto BINARY null-safe | Stesso test, casi owner/user/id |
| Identità sidecar immutabile | EF, R | security_update | Nessun caso sidecar-rekey dedicato nel T attuale |
| Role rekey vietato | T, R | role_identity_update | Stesso test, caso role-update; rekey MembershipId richiede prova aggiuntiva |
| No delete/recreate operativo | T, R | security_delete, membership_security_delete, FK; 004 PRECEDES core | T.Sql_IdentityResetAndOperationalDeleteAreRejected, state-delete/membership-delete e duplicate id |
| Errore storico membership DELETE 1451 | T, R | 004 SIGNAL 23000 / MYSQL_ERRNO=1451 | Stesso test membership-delete; 005 controlla ordine |
| Missing-state fail-closed | reader C#, T, R | UPDATE condizionali con rowcount, guard no-op, FK, reader invariato | T.Sql_MissingStateFailsClosedForRoleAndStatus; T.ScopedReaderUsesPersistedGenerationAndFailsClosedOutsideOwnerOrWhenAbsent |
| A non modifica B dello stesso User | T, EF | Scope institution_id+membership_id, owner immutabile | Diversi T, inclusi InitialState, RoleAddRemove, Rollback, ConcurrentChanges |
| INSERT SELECT e DELETE JOIN | T, R, fixture Mt3aOwnership | Ruoli leggono parent ma scrivono soltanto sidecar | T.Sql_InsertSelectMultirowAndDuplicateNoopRemainCompatible; T.Sql_RoleAddRemoveRestoreNeverRestoresGeneration |
| Concorrenza senza incrementi persi | T | UPDATE atomico per riga sidecar | T.Sql_ConcurrentChangesHaveNoLostOrDuplicateIncrements; non dimostrato staticamente |
| Mutex Institution e snapshot aggiornato | W, T | Repository preservato, nessun lock finto nel SQL | T.Sql_InstitutionMutexSerializesAndSecondTransactionSeesCommittedGeneration |
| Writer tecnico solo Provisioning | W, T, architettura | Nessuna modifica al repository; trigger non duplicano incremento EF | T.TechnicalWritersCannotMutateOperationalInstitutionsEvenForGlobalAdmin; T.TechnicalProvisioningUsesSqlGenerationWithoutInventingHumanActorOrProfiles |
| Backfill solo mancanti, mai reset | T, R | 002 INSERT SELECT anti-join senza upsert/IGNORE | T.Sql_BackfillInitializesMissingOnlyWithoutResettingExistingGeneration |
| Teardown sintetico e holding esclusa | P, R, fixture mirate | Tre predicati identici, owner espliciti, root, ambiente isolato, allowlist codice GUID | DisposeAsync delle fixture; test specifici di negazione non-root/wrong-env/holding da aggiungere nella futura prova |
| Nessuna API/ledger IAM storico | T, architettura | Solo tabella sicurezza; nessun INSERT eventi o modifica HTTP | T.PersistenceIntroducesNoHttpMutationSurface |

Le lacune di copertura sopra sono dichiarate, non colmate attribuendo ai test
assertion che non contengono. Non impediscono di revisionare i candidati; impediscono
di certificarne ora la persistence senza prove aggiuntive sul server isolato.

## Guardie fixture e differenze ricostruite

La capability viene valutata da tre trigger, sempre come congiunzione completa:
login root (USER, non CURRENT_USER del definer), DB test, MySQL 5.7.24, porta 13306,
suffisso datadir isolato, FIND_IN_SET dell'owner esplicito e codice sintetico
con prefisso osservato e suffisso GUID di 32 cifre esadecimali.
Allowlist candidata: MT1, MT1_REHEARSAL, MT2, MT3, MT3_REHEARSAL, MT3A, MT3B,
MT3C, MT3D, MT4, MT4B, MT5A, MT5BP. Non ammette il codice della holding reale.
I rehearsal sono riconoscibili solo dopo il relabel già esplicito nelle fixture.
Il prefisso MT4 osservato non concede diritti senza tutti gli altri controlli.

Nessun token/parametro HTTP controlla la capability; nessun flag client IsSystem.
La sola variabile di sessione, la sola porta o il solo root non bastano. La
cancellazione dei ruoli continua a incrementare e a negare missing-state, anche
durante teardown. Il parent può eliminare la sidecar solo dopo le verifiche;
un successivo errore FK deve rollbackare anche quella eliminazione.
Non è una difesa da root/DDL: un DBA che relabelasse la holding o alterasse i
trigger supera il modello di minaccia. Per questo il driver/test deve preservare
l'identità della holding reale e la manutenzione è esclusivamente tecnica.

**Dettagli storici non ricostruibili con certezza:** corpi esatti, commenti,
messaggi liberi, sette/sei/due regole originali e allowlist SQL esatta dei prefissi.
Non vengono dichiarati recuperati. Le seguenti sono scelte nuove da revisionare,
coerenti col contratto dimostrato, non supposizioni di identità storica:

- Validator espliciti ricostruiti e gate di installazione in 001/002/004 mediante
  PREPARE di DO 0 sul target corretto, altrimenti identificatore inesistente.
  Non creano routine ausiliarie o tabelle e non autorizzano scritture sulla 3306.
  Richiedono driver abort-on-error: --force non è ammesso. Non difendono da un DBA.
- 001 rifiuta anche trigger mt5b_ preesistenti prima della CREATE; nessun replay
  EXPAND o riparazione automatica. 004 duplicato fallisce su CREATE TRIGGER.
- Controllo missing-state più restrittivo anche su status/role UPDATE no-op.
  Nessuna fonte dimostra quel dettaglio storico; è una scelta candidata
  conservativa, da provare e approvare, non una retro-certificazione.
- Confronti BINARY sulle identità vietano anche modifiche della sola grafia del
  GUID; allowlist fixture finita derivata dalle fixture effettivamente lette.
- Messaggi nuovi non promettono identità testuale col passato. Numeri richiesti
  dai test conservati: 1644 per SIGNAL 45000, 1451 per DELETE operativo e 1452 FK.

Nessun requisito funzionale essenziale resta senza una proposta SQL tracciata.
L'identità binaria con gli originali è impossibile e **non è un obiettivo** di R1.
Se il review richiedesse un dettaglio storico non documentato come prerequisito,
quel dettaglio sarebbe un BLOCKER: non va dedotto dagli hash né inventato.

## SHA256 del nuovo pacchetto

| Candidato | SHA256 nuovo |
| --- | --- |
| 000_preflight.sql | 98E3B424847C4C1408E4F4DC402ABDC1E318E4707CAA51DA6978D241B01753DF |
| 001_security_state_expand.sql | F7EA78B7A046FD45A98C4CE7AE50274F495B6A7F20A8DF256BDDEDADAE7383B0 |
| 002_security_state_backfill.sql | 471D37F679C9B5B323D70681CA02FCE12CDBB0EEACAA3EC6FCB35CE2E6E8B35A |
| 003_security_state_validate.sql | E4B86BB9822E0A722002C1DE6C30E8DB9DD483DF22C0F4C19E52F031F14B556C |
| 004_membership_delete_error_contract.sql | 264BA4652F0380D80AA5F14EAC29CF51C80CA72C1992CA71C0F33C43290E04A7 |
| 005_delete_contract_validate.sql | 092539C2C212CAF293C3DCA1AC5471F96AF38985732545F99D8DFAC6643776C7 |

Tutti diversi dagli originali. Manifest storico invariato, SHA256 del file
`A35A467CC3EA3BC62A6A1F95D3B62BEFB8606333AED70C8F6998C749D86D5941`.
Il nuovo manifest conserva entrambi gli hash per confronto e non afferma equivalenza.
Congela anche gli hash degli SQL canonici predecessori, che non sono stati modificati.

## Verifica statica e limiti

Eseguito solo:
`python migrations/mt5/candidate-r1/verify_static.py`.
Esito **STATIC_REVIEW_CHECKS_PASSED**: sei file/hash, quote/commenti/parentesi e
delimitatori bilanciati, 11 nomi/forme coincidenti con fonti C#/metadata storici,
53 nomi preesistenti senza collisioni, tre predicati fixture identici, tre incrementi
atomici limitati con ROW_COUNT obbligatorio, sette/sei/due regole validator.
Controllate assenza DROP/TRUNCATE/ALTER distruttive, GRANT, uso mysql.*, cambi
GLOBAL, UPDATE del parent dai ruoli, reset/upsert generation e IF NOT EXISTS in EXPAND.

La sintassi è stata revisionata per costrutti MySQL 5.7: DELIMITER client,
SIGNAL/MYSQL_ERRNO, PRECEDES, BIGINT signed, trigger multipli, information_schema
e PREPARE/DO. **Non è stato invocato un parser/server MySQL per certificarla**.
Il controllo lessicale non sostituisce la compilazione dei CREATE TRIGGER.
I risultati dei validator sono progetti di query, non conteggi ottenuti dal DB oggi.

Non dimostrabili staticamente: errori 1442 su tutti gli statement, ordine runtime,
rollback multi-row, locking/deadlock, concorrenza, overflow effettivo, comportamento
INSERT IGNORE/REPLACE, permessi dei DEFINER e teardown completo delle fixture.
Statement futuri che leggano la sidecar mentre un trigger la modifica possono
fallire chiusi: ExpectedRevision va letto separatamente sotto mutex.
Backfill ripetibile prima del cutover non è un recupero automatico di stati persi
dopo JWT. DDL a commit implicito: nessun rollback globale promesso o DOWN distruttivo.

Proiezione, non risultato verificato: una tabella + 11 trigger + una FK + due
indici porterebbero MT4C a 33/64/99/141. I conteggi non provano i contratti.
Non si dichiara PERSISTENCE READY sul nuovo PC né un nuovo 738/738 Passed.

## Packaging e Git

Branch `refactor-database-entities`, HEAD invariato
`92cb8141b9555a36c00e517cb4cddc50d0dd7485`.
Modificata solo la .gitignore principale tra i file già tracciati: sei eccezioni
SQL per i percorsi esatti di candidate-r1, non un generico !*.sql.
La .gitignore locale candidata permette solo sei SQL e documentazione/manifest/
verificatore dichiarati; tutto il resto resta escluso.

`git status --short --untracked-files=all` mostra i sei candidati e i nuovi artefatti
di review. I cinque report locali precedenti restano non tracciati e non alterati.
`git check-ignore -v --no-index` conferma le sei eccezioni; backup.sql, server.log,
private/client.ini e datadir/ibdata1 nel pacchetto restano esclusi, così come i
dump e gli strumenti della directory mt4/private. Gli SQL perduti nella directory
del runner restano assenti e ignorati. Nessuna credenziale o dump aggiunto al pacchetto.
`git diff --check` superato; manifest storico, SQL MT1/MT3/MT4 e codice applicativo
immutati. Nessun commit, push, staging o modifica al runner pubblico.

## Prossimo checkpoint proposto — nessuna esecuzione autorizzata qui

1. Approvare separatamente candidati e piano di istanza/prova. Verificare di nuovo
   processo/listener/SQL della baseline 3306: versione, CES01622, datadir MAMP,
   database e schema MT4C. Acquisire nuovo backup logico privato e SHA256 della
   sola base test, senza importare datadir o dati operativi.
2. Verificare disponibilità della 13306; directory privata nuova e ACL, spazio,
   file config e comando mysqld da presentare prima di inizializzare/avviare.
   Target proposto: stesso binario MySQL 5.7.24, bind 127.0.0.1, porta 13306,
   datadir assoluto `<repo>/migrations/mt4/private/mt5a-mamp-13306/data/`.
   Non esiste una nuova istanza creata da R1. Non modificare la baseline 3306.
3. Dopo autorizzazione, inizializzare l'istanza distinta e predisporre credenziali/
   DEFINER tecnici privati, senza copiare mysql.* dal server baseline. Verificare
   entrambi i server e il datadir assoluto prima di ogni operazione. Preparare
   electronic_register_test sull'isolata dal backup MT4C verificato, verificando
   definizioni/dati/98 FK/53 trigger e validator invariati prima dei candidati.
4. Revisionare il driver di esecuzione R1: input esplicito della directory candidata
   e manifest nuovo, target completo bloccato, abort-on-error, backup prima del DDL,
   source hash e verifiche zero obbligatorie. Non copiare alla cieca il vecchio
   runner: assume file persi nella propria directory e un client dump MySQL 8.
   Verificare anche normalizzazione dei separatori del path in PowerShell
   (`Replace('\\','/')` nel sorgente non equivale a sostituire una singola barra
   inversa); nessuna guardia va rimossa per far coincidere il target.
5. Verificare le guardie fixture prima di ammettere teardown: root, versione,
   porta, datadir, owner espliciti, whitelist codici e holding reale esclusa.
   I test/helper attuali mantengono 13306 e suffisso isolato; non sostituire 3306.
6. Solo con nuova approvazione applicare 000→001→002→003→004→005 sull'isolata,
   una volta, conservando lo stato in caso di errore. Verificare corpi/ordine/chiavi
   integralmente e tutti gli orfani, non soltanto il numero oggetti. Provare backfill
   con dati preesistenti e nonzero, idempotenza e casi negativi della matrice.
7. Successiva autorizzazione per prove SQL/fixture/concorrenza e suite: confrontare
   le stesse 738 identità storiche, zero Failed/Skipped/NotExecuted, controlli finali
   e baseline 3306 preservata. Non anticipare tale esito né iniziare JWT.

Blocchi all'esecuzione: approvazione dei candidati mancante, istanza isolata non
creata, driver R1 non ancora revisionato, prove runtime e lacune negative ancora
da eseguire. Non sono dichiarati risolti dalla verifica statica.

**MT-5B SQL RECONSTRUCTION READY FOR REVIEW**
