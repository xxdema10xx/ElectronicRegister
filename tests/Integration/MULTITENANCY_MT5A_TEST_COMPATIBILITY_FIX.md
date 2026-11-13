# MT-5A — Test compatibility fix e certificazione MAMP

**MT-5A COMPLETATA — 711/711 PASSED ON MAMP MYSQL 5.7.24**

## Perimetro e ambiente

Branch `refactor-database-entities`, HEAD `a002cb66cebecfdcdf892c099cf0518b796b7e1a`.
Workspace preesistente preservato; nessun commit/push/reset o MT5B.
Solo due sorgenti di test modificati rispetto al freeze precedente:

- `tests/Integration/Mt3dHttpTests.cs`, linee 743–744 e 775: test SQL mode.
- `tests/Integration/Mt3eWorkerTests.cs`, linee 245–330: interceptor timeout e relativo caso parametrizzato.

Nessun controller, manager, repository produttivo, entity, worker, autenticazione,
JWT, LegacyEndpointGate, formula voti, frontend o SQL canonico modificato.
Nuove evidenze separate in [mt5a-compatibility](TestResults/mt5a-compatibility/);
i TRX dei tentativi precedenti rimangono conservati.

MySQL **5.7.24 MAMP**, `127.0.0.1:13306`, database `electronic_register_test`,
datadir `migrations/mt4/private/mt5a-mamp-13306/data` sotto il repository.
Binario `C:\MAMP\bin\mysql\bin\mysqld.exe`, listener/PID e identita SQL verificati.
Preflight: 32 tabelle, 53 trigger, 98 FK, 139 indici; ledger e revision presenti,
7 controlli MT4A e 5 MT4C senza violazioni. Nessuna migrazione/restore/reset.

SDK 10.0.401; MySqlConnector 2.4.0. Configurazione solo nei processi figli:
connessione privata verso 13306, `ER_TEST_STRICT_SESSION=1`, client mysqldump
8.0.44 con flavor MySQL, EventLog disabilitato per il test host Windows.
Credenziali esistenti, mai stampate; ambiente ripristinato in finally.

## Correzione SQL mode

La vecchia assertion presumeva GLOBAL non strict, diversamente da MAMP.
Ora acquisisce `@@GLOBAL.sql_mode` prima delle operazioni originali e verifica
l'uguaglianza esatta dopo. Rimane obbligatorio STRICT_TRANS_TABLES sulla sessione.
Tutte le assertion SQL, isolamento, ownership e costo query originali conservate.
Non viene impostato GLOBAL, ne richiesta una configurazione meno restrittiva.

## Correzione timeout

La sostituzione precedente con `SELECT SLEEP(5)` cambiava il result set EF.
L'interceptor ora riconosce esclusivamente il locking read worker di exam_submission,
con tutti e cinque i parametri id/id/institution/student/exam. Richiede una sola
corrispondenza e parametri effettivamente presenti; un cambiamento inatteso della
query fa fallire il test. Inserisce soltanto `AND SLEEP(5)=0` prima di FOR UPDATE:
proiezione, query esterna EF, parametri, predicati ownership e lock conservati.
Il comando interessato ha CommandTimeout=1; nessun altro comando viene ritardato.

Il primo tentativo con cancellazione soft ha restituito zero righe dopo interruzione
SLEEP, producendo KeyNotFoundException: **rifiutato**, TRX conservato (`fixes`).
Si usa quindi `CancellationTimeout=-1` soltanto nel provider DI di questo ramo di test:
alla scadenza il driver chiude fisicamente la connessione. E una modalita documentata
di [MySqlConnector](https://mysqlconnector.net/overview/command-cancellation/),
non una modifica del worker o della configurazione produttiva.

Il tentativo `fixes-2` ha evidenziato che il rollback su connessione chiusa puo mascherare
l'errore originale con InvalidOperationException. La versione finale intercetta
CommandFailedAsync e richiede **MySqlException / CommandTimeoutExpired** al confine
EF/provider. Non accetta InvalidCastException nella catena, ne errori arbitrari.
Se il timeout non e nella catena propagata, e ammesso solo l'esatto errore di rollback
su connessione chiusa, con timeout provider comunque obbligatoriamente osservato.
Un cronometro richiede almeno 800ms dalla reale iniezione alla conclusione.

Restano digest A/B identici dopo il fault, prova di assenza scritture parziali e
rollback effettivo attraverso nuove letture; seguono recupero reale e AssertTechnical
(unica submission, grade/audit coerenti). Aggiunto un secondo recupero con digest A/B
invariati per dimostrare idempotenza e assenza di modifiche cross-tenant.
I rami connection/grading mantengono la fault originale e ricevono anche questa
verifica aggiuntiva di idempotenza.

La mascheratura dell'errore nel rollback e un limite osservato del reporting delle
eccezioni, non riparato in questo incarico. Non viene certificata la conservazione
dell'eccezione originale attraverso TransactionManager. La garanzia richiesta di
timeout reale, assenza scritture parziali e recupero e verificata separatamente.

## Checkpoint A/B

Build completa senza errori. Tentativi mirati `fixes` e `fixes-2`: 3/4 Passed,
1 Failed (timeout), zero Skipped; esiti conservati, nessuna suite completa avviata
durante la diagnosi. Versione finale `fixes-3`: **4/4 Passed**, zero Failed,
Skipped/NotExecuted, inclusi SQL mode e tutti i rami connection/timeout/grading.
I nomi e il numero dei test non sono cambiati.

## Checkpoint C/D/E e integrita finale

Baseline: **675/675 Passed**, 51 unit e 624 integration, zero Failed/Skipped/NotExecuted.
Il verificatore confronta tutti i nomi con MT4C final-2: zero differenze.
[TRX e verifica baseline](TestResults/mt5a-compatibility/baseline-verification.json).

MT5A: **36/36 Passed** sulla copia MT4C isolata, zero Failed/Skipped/NotExecuted.
Copertura reale di directory/dettaglio, admin A/B, identita condivise, ruoli separati,
JWT/revoche, token legacy/selection, stato membership/istituto, rollout, DTO senza segreti,
SQL scoped e digest read-only. Nessuna nuova route di scrittura.
[Nomi effettivi](TestResults/mt5a-compatibility/targeted-test-identities.json),
[verifica TRX](TestResults/mt5a-compatibility/targeted-verification.json).

Hash dei 370 sorgenti congelati prima delle suite definitive, incluse le due correzioni.
Final-1: **711/711 Passed** (51 unit + 660 integration), zero Failed/Skipped/NotExecuted.
Verificati singoli outcome, contatori TRX, 675 identita precedenti e 36 aggiunte esattamente
uguali al checkpoint MT5A. Hash sorgenti e SQL canonici invariati dopo final-1.
[Verifica final-1](TestResults/mt5a-compatibility/final-1-verification.json).
Final-2: **711/711 Passed** (51 unit + 660 integration), zero Failed/Skipped/NotExecuted.
Stesse identita e outcome per progetto dei due run. Tutti i 675 casi storici preservati;
36 nuove identita esattamente corrispondenti al checkpoint MT5A. Nessun cambio sorgenti
tra le suite; i soli due test autorizzati differiscono dal freeze storico.
[Verifica final-2](TestResults/mt5a-compatibility/final-2-verification.json),
[hash sorgenti](TestResults/mt5a-compatibility/after-final-2-source-verification.json).

| Checkpoint | Unit | Integration | Totale Passed | Failed / Skipped / NotExecuted |
| --- | ---: | ---: | ---: | --- |
| Baseline storica | 51 | 624 | 675 | 0 / 0 / 0 |
| MT5A mirati | — | 36 | 36 | 0 / 0 / 0 |
| Final-1 | 51 | 660 | 711 | 0 / 0 / 0 |
| Final-2 | 51 | 660 | 711 | 0 / 0 / 0 |

Le matrici da nomi effettivi confermano tutti i moduli eseguiti e Passed:
MT1 21, MT2 61, profili 21, MT3A 39, MT3B 39, MT3C 56, MT3D 55,
MT3E 37, MT4A 30, MT4B 37, MT4C 31, MT5A 36, altri integration 197 e unit 51.
Bootstrap Development/Production reali, JWT, revoche, LegacyEndpointGate, worker e audit
sono inclusi nei TRX, senza modifiche alle rispettive implementazioni.
[Matrice final-2](TestResults/mt5a-compatibility/final-2-module-summary.json),
[casi per modulo](TestResults/mt5a-compatibility/final-2-module-identities.json),
[bootstrap](TestResults/mt5a-compatibility/final-2-bootstrap-identities.json).

## Integrita finale e rischi

Connessioni nuove sulla 13306: 32 tabelle, 53 trigger con definizioni/eventi/timing
invariati, 98 FK con definizioni composite/regole invariate e **zero orfani**, 139 indici
invariati. Ledger e definizione revision corretti; sette controlli MT4A e cinque MT4C
a zero violazioni. Solo holding tecnica Provisioning, 15 User e nessun residuo di
membership/ruoli/profili/campus/edizioni/iscrizioni/ledger istituzionali temporanei.
GLOBAL sql_mode invariato; nessuna pulizia manuale o restore.
[Integrita](TestResults/mt5a-compatibility/final-integrity.json),
[98 FK](TestResults/mt5a-compatibility/final-fk-integrity.json),
[identita](TestResults/mt5a-compatibility/final-isolated-identity.json).

Originale 3306 interrogato esclusivamente in lettura. Snapshot identita/schema/stati/
conteggi prima/dopo identici anche allo snapshot precedente, SHA256
`599891D4994047BAD77F86F3BE3AE09FFFA9C5322B53CFCF7748768E93136CE0`.
Nessuna fixture, DDL/DML o backup esportato sull'originale durante questo incarico.
Non si afferma un digest di ogni cella originale; le fixture legacy possono modificare
intenzionalmente dati didattici della sola copia isolata.

`git diff --check` superato. Rollout default false; nessuna configurazione produttiva
cambiata. Ambiente dei figli ripristinato, nessun segreto nei TRX pubblicati, nessuna
credenziale nuova. TRX separati con hash, nessun vecchio TRX sovrascritto.
[Certificazione verificata](TestResults/mt5a-compatibility/final-certification.json),
[manifest TRX](TestResults/mt5a-compatibility/trx-sha256-manifest.json).

Resta documentato il limite diagnostico del rollback dopo hard cancellation descritto
sopra; nessuna modifica applicativa per correggerlo inclusa. La prova verifica timeout
provider e integrita/recupero, senza certificare la propagazione dell'eccezione originale.
Questa certificazione locale non dichiara prontezza produzione. Last-admin transazionale,
audit membership e onboarding verificato restano prerequisiti del futuro MT5B,
che non e stato iniziato. Nessun commit/push o cutover.
Nessuna certificazione MariaDB 10.4 viene dedotta dalle esecuzioni MySQL 5.7.
