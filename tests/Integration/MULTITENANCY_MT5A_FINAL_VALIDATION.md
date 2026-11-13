# MT-5A — Final test validation su MAMP isolato

## Esito successivo — correzione test e due suite definitive

**MT-5A COMPLETATA — 711/711 PASSED ON MAMP MYSQL 5.7.24**

Corretti esclusivamente i due test storici autorizzati: confronto GLOBAL prima/dopo
e timeout reale del provider con query/ownership conservate. Nessuna modifica applicativa.
Mirati 4/4; baseline 675/675 con identita storiche esatte; MT5A 36/36;
final-1 e final-2 entrambe **51 unit + 660 integration = 711/711 Passed**,
zero Failed/Skipped/NotExecuted. Identita/outcome uguali, hash sorgenti invariati.

Integrita finale SQL verificata: 98 FK senza orfani, 53 trigger invariati,
ledger/revision e validatori MT4A/MT4C corretti, nessuna fixture istituzionale residua.
Originale 3306 invariato negli snapshot; GLOBAL invariato, rollout chiuso.
Limite diagnostico del rollback dopo hard cancellation esplicitamente documentato.
[Report correzioni, risultati e limiti](MULTITENANCY_MT5A_TEST_COMPATIBILITY_FIX.md).

Il tentativo precedente segue integralmente come storico: il suo esito 673/675 e i
relativi TRX falliti non sono stati cancellati o sostituiti.

## Storico — tentativo precedente alla correzione dei test

**MT-5A PARZIALE — VALIDATION BLOCKED**

Build e smoke superati. Baseline eseguita realmente: **673/675 Passed**,2Failed,
0Skipped/NotExecuted. Tutte675 identita storiche presenti. Due test storici hanno
assunzioni/fault injection non compatibili con l'ambiente corrente. Come richiesto,
checkpointC36test MT5A e checkpointD due suite711 **non avviati**. Nessun codice,
test, autorizzazione, schema o GLOBAL sql_mode modificato per ottenere il verde.

## 1. Branch, HEAD e stato Git

Branch refactor-database-entities; HEAD a002cb66cebecfdcdf892c099cf0518b796b7e1a.
Workspace iniziale con modifiche MT5A/recovery preesistenti, preservate.
[Git iniziale](TestResults/mt5a-mamp-validation/initial-git-status.txt),
[HEAD](TestResults/mt5a-mamp-validation/initial-head.txt).
Nessun commit/push/reset/checkout/MT5B.370sorgenti C# e6SQLcanonici confrontati con
freeze/hash precedenti: tutti invariati. Modificati soltanto documentazione/evidenze e
aggiunti wrapper diagnostici privati, senza alterare applicazione o test.

## 2. Ambiente MAMP isolato e identita effettiva

Letture preliminari dei sei documenti richiesti, progetti, fixture/host, backup helper e
verificatori TRX esistenti. SDK **10.0.401**, targetnet10.0; Pomelo9.0.0/MySqlConnector2.4.0.

PID corrente letto dal pid-file e verificato contro processo/listener, senza considerare
permanente il PID storico. In questa esecuzione era ancora24340:

| Identita | Valore |
| --- | --- |
| Server | MySQL5.7.24 MAMP |
| Binario | C:\MAMP\bin\mysql\bin\mysqld.exe |
| Listener | 127.0.0.1:13306 |
| Hostname | DESKTOP-QUOAG32 |
| Datadir | C:\Users\Lorenzo\ElectronicRegister\migrations\mt4\private\mt5a-mamp-13306\data\ |
| Database | electronic_register_test |

Credenziali esistenti lette dal client.ini privato; nessun reset/password nuova. File
ignorato da Git, nessuna connection string/password nei comandi condivisi o report.
[Identita](TestResults/mt5a-mamp-validation/isolated-preflight-identity.json).

## 3. Schema MT-4C e preflight

Verificati32tabelle,53trigger,98FK,139indici, ledger presente e Revision BIGINT(20)
NOT NULL DEFAULT0. Validatori canonici003/006 eseguiti esclusivamente in lettura:
7controlli MT4A e5MT4C, tutte violazioni0. Nessuna migrazione, restore, nuova
inizializzazione, DROP o preparazione MT3C.
[Preflight](TestResults/mt5a-mamp-validation/preflight.json),
[MT4A](TestResults/mt5a-mamp-validation/preflight-mt4a-validator.json),
[MT4C](TestResults/mt5a-mamp-validation/preflight-mt4c-validator.json).

Il ramo EnsureCreated della fixture e condizionato a database vuoto: qui32tabelle,
quindi viene effettuata la verifica del modello, senza ricostruire lo schema.
Le prove storiche di idempotenza/backup restano quelle originali; nessuna ulteriore
migrazione di versione e stata eseguita fuori dalle fixture autorizzate.

## 4. Configurazione e propagazione ai processi

Override soltanto nel processo figlio wrapper e nei suoi discendenti:
TEST_MYSQL_CONNECTION verso127.0.0.1:13306/electronic_register_test, credenziali private;
ER_TEST_STRICT_SESSION=1; MYSQLDUMP_FLAVOR=MySQL;
MYSQLDUMP_EXE=C:\Program Files\MySQL\MySQL Server 8.0\bin\mysqldump.exe;
Logging__EventLog__LogLevel__Default=None per l'host Windows.

Verificati i percorsi effettivi: IntegrationFixture legge la variabile, DI usa la stessa
connessione, ProgramHost/AuditBootstrap/Phase2C passano la connessione della fixture alla
configurazione in memoria. Il backup helper passa esplicitamente host/port/user derivati
dalla connessione e confronta target/utente/porta con TEST_MYSQL_CONNECTION.
Nessuna fallback3306 individuata nei consumer dei test; i target invalidi dei test negativi
sono rifiutati prima dell'accesso, senza utilizzare database operativi.

Campione runtime netstat: testhostPID9472 con tre connessioni stabilite a13306, nessuna
connessione di test a3306 nel campione. Non e una traccia continua di ogni pacchetto:
si aggiunge ai guard/configurazione e all'analisi dei consumer.
[Connessioni osservate](TestResults/mt5a-mamp-validation/baseline-live-connections.json).

Valori ambiente precedenti conservati e ripristinati in finally; ambiente chiamante ancora
su3306 come prima dell'incarico. Nessuna MYSQL_PWD residua. Log/TRX prima privati, controllati
per la password dell'istanza prima di copiarli nelle evidenze; nessun segreto rilevato.
Nessuna appsettings operativa o flag produttivo cambiato.

## 5. Client dump, build e checkpointA

Client **mysqldump8.0.44**, server sempreMAMP5.7.24. Verificati help/opzioni e backup reale
della sola copia isolata con --column-statistics=0/--no-tablespaces/--set-gtid-purged=OFF:
32definizioni/53trigger/footer e SHA256 verificati. Nessun flavorMariaDB improprio.
[Probe](TestResults/mt5a-mamp-validation/dump-client-probe.json).

Build completa soluzione con --no-restore --no-incremental: **zero errori**,11warning
nullable preesistenti; nessun pacchetto/progetto modificato.
[Build](TestResults/mt5a-mamp-validation/build/solution-build.log).

Smoke **4/4 Passed**,0Failed/Skipped/NotExecuted:

- AuditBootstrapTests.RealProgram_StartsAuthenticatesDocumentsAndStops, Development e Production.
- Mt2IdentityTests.IdentitySelectionSeparatesFoundationsAndRolesWithoutLegacyProfiles.
- Mt4bHttpTests.Mt4cSql_AppendOnlyCompositeOwnersAndUtcAreEnforcedByRealMariaDb.

Il nome storico dell'ultimo test contiene RealMariaDb: in questo run e stato eseguito
suMySQL5.7, non rappresenta una nuova prova MariaDB. Host/Program/DI/JWT e SQL reali.
[TRX smoke](TestResults/mt5a-mamp-validation/smoke/Integration/),
[verifica](TestResults/mt5a-mamp-validation/smoke-verification.json).

## 6. CheckpointB — baseline storica675

Eseguiti i progetti separatamente, filtro che esclude soltanto Mt5aMembershipAdminTests:

| Progetto | Totale | Passed | Failed | Skipped/NotExecuted |
| --- | ---: | ---: | ---: | ---: |
| Unit | 51 | 51 | 0 | 0 |
| Integration | 624 | 622 | 2 | 0 |
| Totale | 675 | 673 | 2 | 0 |

Durata integration circa2m48s. Non si dichiara675/675 dal solo exit code.
[TRX Unit](TestResults/mt5a-mamp-validation/baseline/Unit/),
[TRX Integration](TestResults/mt5a-mamp-validation/baseline/Integration/),
[verifica contatori/outcome/hash](TestResults/mt5a-mamp-validation/baseline-verification.json).

## 7. Identita dei test precedenti e regressioni per modulo

675nomi univoci esattamente uguali a MT4C final-2, nessun test mancante/aggiunto nella
baseline. Total/executed/passed/failed e outcome individuali verificati: nessun NotExecuted.
Vecchi TRX MT5A falliti e risultati MariaDB storici non sovrascritti o reinterpretati.

| Gruppo da nomi effettivi | Passed / Totale | Failed |
| --- | ---: | ---: |
| MT1 Institution/Membership | 21/21 | 0 |
| MT2 identity/Microsoft/JWT | 61/61 | 0 |
| MT3 profili | 21/21 | 0 |
| MT3A ownership SQL | 39/39 | 0 |
| MT3B scoped repository | 39/39 | 0 |
| MT3C HTTP e preflight | 56/56 | 0 |
| MT3D, esclusi metodi Mt3e | 54/55 | 1 |
| MT3E worker/fault/recovery | 36/37 | 1 |
| MT4A SQL | 30/30 | 0 |
| MT4B API | 37/37 | 0 |
| MT4C lifecycle/audit | 31/31 | 0 |
| Altri integration storici | 197/197 | 0 |
| Unit | 51/51 | 0 |

Le categorie MT3E/MT4C sono partial delle classi Mt3dHttpTests/Mt4bHttpTests, distinte
tramite prefisso effettivo del metodo. Non e certificato l'intero MT3D o MT3E in questo run.
[Matrice](TestResults/mt5a-mamp-validation/baseline-module-summary.json),
[tutti i nomi/outcome per modulo](TestResults/mt5a-mamp-validation/baseline-module-identities.json).

## 8. Primo errore — assunzione GLOBAL non strict

Caso: Mt3dHttpTests.HttpSqlHasTenantParametersConstantListCostAndExplicitOwnedWrites.
In [Mt3dHttpTests.cs](Mt3dHttpTests.cs), linea773, verifica sessione STRICT_TRANS_TABLES
e poi Assert.DoesNotContain("STRICT_", global).

GLOBAL della copia contieneva gia STRICT_TRANS_TABLES prima dei test ed e rimasto identico.
Il test interpreta erroneamente una configurazione iniziale strict come prova di modifica
globale. Il precedente MariaDB aveva GLOBAL non strict, condizione diversa documentata.
Classificazione: **assunzione ambientale nel test**, non nuovo errore schema/permessi/SQL
tenant-aware. Nessuna evidenza di modifica globale prodotta dal sistema in questo run.
L'asserzione non e stata cambiata e GLOBAL non e stato indebolito per farla passare.

## 9. Secondo errore — fault injection timeout

Caso: Mt3dHttpTests.Mt3eConnectionTimeoutAndGradingFailuresRecoverWithoutPartialWrites,
kind="timeout". [Mt3eWorkerTests.cs](Mt3eWorkerTests.cs), linee245–249 e273.
L'interceptor sostituisce il primo comando FOR UPDATE con **SELECT SLEEP(5)** e timeout1s,
poi il test pretende la parola "timeout" nell'eccezione. Il run ha ricevuto invece una
InvalidCastException e fallisce quell'asserzione; il TRX conserva il messaggio troncato
dal formatter xUnit. Non si inventa lo stack completo dell'eccezione interna.

Classificazione: **compatibilita dell'iniezione nel test con MySQL/provider**, da correggere
separatamente. La query originale e una lettura EF di ExamSubmission; la sostituzione cambia
anche forma/tipo del result set, introducendo un'interferenza estranea alla timeout policy.

Verifica diagnostica aggiuntiva, solo SELECT sulla13306 e nessuna fixture:
SELECT con hint locale MAX_EXECUTION_TIME(1000) e SLEEP(5) restituisce normalmente **1**
in circa1043ms, senza errore SQL. MySQL5.7 documenta che SLEEP usato da solo puo restituire1
all'interruzione invece di generare errore; se e parte di una query l'interruzione produce
errore. [Manuale MySQL5.7](https://docs.oracle.com/cd/E17952_01/mysql-5.7-en/miscellaneous-functions.html).

E coerente con la query di fault che ritorna uno scalare anziche l'entita attesa; resta
un'inferenza sul percorso completo CommandTimeout/MySqlConnector, **non** una sua riproduzione
integrale. Non e dimostrata una regressione del worker o modificato il provider.
Il caso fallisce prima delle proprie assertion successive su digest/recovery: quella
specifica garanzia timeout non viene dichiarata certificata.
[Probe read-only](TestResults/mt5a-mamp-validation/sleep-compatibility-probe.json).

## 10. Proposte di correzione — NON applicate

Da approvare e revisionare in un incarico separato, conservando nomi e numero test:

1. Test GLOBAL: acquisire GLOBAL sql_mode prima dell'esecuzione e confrontarlo dopo per
   uguaglianza, mantenendo l'obbligo STRICT_TRANS_TABLES sulla sessione. Verifica che GLOBAL
   non cambi, senza pretendere che la configurazione iniziale sia meno restrittiva.
2. Fault timeout: mantenere proiezione/shape e predicati tenant della SELECT originale;
   introdurre un ritardo come parte della query, non sostituirla con uno scalare. Verificare
   timeout reale del provider con tipo/codice appropriato, mantenendo tutte le assertion
   digest/assenza scritture parziali e recovery. Non accettare InvalidCastException come timeout.
3. Provare prima soltanto i due casi interessati suMySQL5.7.24; poi ripetere baseline675,
   nuovi36 e due711 se tutto verde. Nessuna riduzione di assertion, skip o modifica JWT/gate.

Nessuna correzione applicativa e giustificata dalle evidenze attuali. Non e stata eseguita
alcuna proposta, ne cambiato codice di test durante questa certificazione.

## 11. CheckpointC —36 test MT5A

**Non eseguito**, per arresto al checkpointB. Il precedente36/36 sulla3306 MT3A rimane
storia conservata nel report MT5A, non una nuova certificazione sulla13306 MT4C.
Directory/dettaglio/read-only/DTO/flag/SQL MT5A non dichiarati nuovamente certificati qui.

## 12. CheckpointD — due suite711

**Non avviate**, come richiesto dopo baseline non verde. Nessun final-1/final-2 nuovo,
nessun risultato711/711, nessun test disabilitato. Non e stato eseguito un ciclo di rerun
per ottenere artificialmente il verde. Le omissioni dei checkpoint sono arresti procedurali,
non outcome Skipped/NotExecuted inventati nei TRX.

## 13. JWT, revoche e autorizzazioni

Tutti61casi MT2 sono Passed, incluse le identita reali dei casi di token non valido,
ruoli/stato modificati dopo emissione, selezione e isolamento membership. Rimangono anche
9unit MembershipAuthorizationTests Passed nella suite51. Program/InstitutionAccess e
legacy gate non sono stati modificati; nessuna attribuzione attraverso User.Role introdotta.
Questo attesta i casi eseguiti, non sostituisce il checkpointC non avviato.

## 14. Worker MT3E

36/37 metodi/casi Mt3e sono Passed; fallisce soltanto il ramo timeout descritto sopra.
Le altre prove fault injection, timer, shutdown/recovery presenti nei TRX sono Passed;
anche i4 Phase3_WorkerRecoveryTests legacy sono Passed. Non si dichiara MT3E interamente
certificato finche il ramo timeout non prova tutte le proprie assertion fino al termine.

## 15. Audit MT4C e bootstrap

31/31 casi con prefisso Mt4c sono Passed: trigger SQL, transizioni/rettifiche, audit,
rollback/fault e concorrenza/idempotenza secondo i nomi effettivi nel TRX.30/30MT4A e37/37MT4B
Passed. Nessun ledger reimpiegato per membership o audit retroattivo.

Smoke2bootstrap e tutti i casi baseline RealProgram/RealBootstrap risultano Passed,
in Development/Production con Program reale e DI. Elenco esatto in
[bootstrap-identities.json](TestResults/mt5a-mamp-validation/bootstrap-identities.json).

## 16. Ownership e integrita SQL dopo arresto

Connessioni nuove: tutte98FK con parent/colonne/ordine/regole invariate e **zero orfani**;
53trigger con definizioni eseguibili/eventi/timing invariati;139indici invariati.
Validatori003/006:7e5controlli tutti0. Revision/ledger conservano schema MT4C.
Controllate tutte le tabelle con InstitutionId: nessun owner nullo/esterno alla holding.
[FK](TestResults/mt5a-mamp-validation/baseline-stop-fk-integrity.json),
[integrita](TestResults/mt5a-mamp-validation/baseline-stop-integrity.json).

## 17. Stato originale3306

Istanza originale interrogata solo in lettura, nessuna fixture/teardown/DDL/DML/restore.
VERSION/hostname/porta/datadir/schema/trigger/stati/conteggi identici allo snapshot della
faseA e prima/dopo questa validazione. GLOBAL sql_mode invariato.
SHA256 degli snapshot prima/dopo:
`599891D4994047BAD77F86F3BE3AE09FFFA9C5322B53CFCF7748768E93136CE0`.
Nessun nuovo dump3306 esportato in questa fase (backup autorizzati soltanto sulla copia).
Si confrontano identita/schema/stati/conteggi, non si pretende un nuovo digest di ogni cella.
[Verifica originale](TestResults/mt5a-mamp-validation/original-verification.json),
[prima](TestResults/mt5a-mamp-validation/original-before.txt),
[dopo](TestResults/mt5a-mamp-validation/original-after.txt).

## 18. Stato isolato13306

Istanza lasciata attiva. Solo holding Provisioning,15User,0membership/ruoli/profili/Campus/
CourseEdition/Enrollment/ledger e nessun residuo istituzionale temporaneo identificato.
Nessuna cancellazione manuale dopo i test; solo i teardown previsti. GLOBAL sql_mode
strict iniziale invariato. Le fixture legacy modificano intenzionalmente dati Esami della
copia: non si dichiara ogni cella isolata identica prima/dopo la baseline.

## 19. Differenze MySQL/MariaDB

Questa e un'esecuzione suMySQL5.7.24, non una nuova esecuzione MariaDB. I due ostacoli sono
una configurazione GLOBAL iniziale differente e una fault query SLEEP con comportamento
documentato diverso da quanto il test presume. Schema, privilegi, backup client e preflight
non hanno fallito;622integration superano realmente il percorso su questo motore.

## 20. Sicurezza e assenza modifiche

Nessun frontend, password operativa, servizio/porta, appsettings, JWT, LegacyEndpointGate,
ruolo/policy o flag produttivo modificato. Nessun nuovo account o privilegio creato.
Le uniche scritture SQL sono fixture/teardown della baseline sulla13306; probe diagnostico
SLEEP e verifiche finali sono read-only. Migrazioni canoniche/sorgenti congelati invariati.
[Verifica finale](TestResults/mt5a-mamp-validation/final-verification.json).

## 21. Rischi aperti

Certificazione bloccata da2test storici, checkpointC/D mancanti. Non si attiva rollout,
non si indebolisce GLOBAL e non si tratta un cast error come timeout valido. Le prove
locali non certificano hardening root, carico, TLS/CORS, retention, restore operativo,
monitoraggio o prontezza produzione. Le proposte devono essere validate senza perdere
copertura di SQL mode, timeout, rollback e recovery.

## 22. Prerequisiti MT5B

Prima completare MT5A con baseline675,36nuovi e due711/711 reali. Last-admin transazionale,
audit membership distinto e onboarding/inviti verificati restano requisiti progettati,
non mutazioni gia aperte. Nessun MT5B autorizzato/avviato, nessun cutover.

## 23. Esito e conservazione evidenze

673Passed/2Failed/0Skipped nella baseline675; smoke4/4. Vecchi tentativi MT5A,TRX e hash
conservati. Nuovi TRX separati per progetto e run, con verificatore riproducibile
[Verify-Run.ps1](TestResults/mt5a-mamp-validation/Verify-Run.ps1).
git diff --check superato; nessuna modifica applicativa o test durante il tentativo.

**MT-5A PARZIALE — VALIDATION BLOCKED**
