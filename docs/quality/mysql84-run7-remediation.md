# Run #7: remediation locale e limiti della certificazione

## Aggiornamento: verifica Docker Desktop reale — 2026-11-02

**NON READY.** I precedenti BLOCKED locali su drift DDL, GUID, fresh install e
CLI Windows sono stati sostituiti da prove runtime MySQL **8.4.13** su un
container isolato. Sono state corrette le differenze dimostrate: CRLF/LF nel
contratto SQL, tipo numerico ACTION_ORDER, escape del predicato nei metadati,
singleton `institution_email_mutex.id` descritto come `int` e aspettative
esatte sui 22 FK impliciti. Nessuna equivalenza generica tra regole FK e
nessun allentamento dei controlli di integrità.

| Area | Risultato locale reale |
|---|---|
| Schema drift e GUID/collation | PASS; metadati reali e regressioni positive/negative |
| Fresh completa, routine, trigger, journal, marker, idempotenza e protocolli SQL | PASS |
| Upgrade: predecessore immutabile, 3995, snapshot, drift rejection, dati, idempotenza, recovery e riavvio | PASS per queste fasi; accettazione DML successiva FAIL |
| Suite richieste: 355 metodi originali, 712 casi | 710 PASS, 2 FAIL, 0 SKIP |
| Nuove regressioni GUID/FK | 12 PASS |
| Mirati lock/readiness/GUID e CLI | 13 PASS |
| Tre CLI Windows con ACL | 3 PASS, inclusi nelle suite |
| Unit .NET ricompilati | 598 PASS |
| Python mysql84 offline | 16 PASS |
| Manifest candidato, 1.102 checksum e predecessore immutabile | PASS |

Residui obbligatori: i due casi tenant `membership-deleted` / `user-deleted`
falliscono sul helper di purge vincolato a 5.7/13306, mantenuto restrittivo.
Il probe INSERT di un hash con newline finale non viene rifiutato: CHAR(64)
memorizza il valore canonico di 64 caratteri prima del trigger, mentre il
predicato ICU sulla stringa originale restituisce falso. Il resto della
matrice di accettazione, fresh-after-upgrade e relativo gate HTTP dipendente
restano **BLOCKED**, senza PASS sostitutivi. Il runtime complessivo non è PASS.

Originale `mysql84-local` preservato: ID
`2299602d4bce333ef0fb626e9b1fb22758ae15ed62edb637357c7ebd4d8bc1b2`,
StartedAt invariato, RestartCount 0. Ambiente principale conservato:
`er84-quality2-local-20261102-a71c`, porta **13384**, volume indipendente.
Controllo finale: **23 container e 23 volumi temporanei indipendenti**
conservati; tre vecchie fixture sono state arrestate con approvazioni
esplicite per liberare risorse AIO. Nessun volume eliminato.

Rapporti completi, durate, identità, file modificati, checksum e tracce:
[Markdown](mysql84-local-real-verification.md) e
[JSON](mysql84-local-real-verification.json). Il JSON separa la storia delle
esecuzioni dagli ultimi esiti e documenta gli SQL finali identici per hash a
quelli verificati runtime. Nessun GITHUB_ACTIONS artificiale, commit, push,
deployment o accesso a EC2/staging/produzione.

## Resoconto storico precedente al provisioning locale reale

Valutazione: **NON READY**. Il lavoro è parziale: non è stata raggiunta la
correzione definitiva richiesta. Non esiste una nuova prova runtime MySQL
8.4.13 e non è stato eseguito un nuovo run GitHub Actions.

Riferimento: [run 41511645062](https://github.com/xxdema10xx/ElectronicRegister/actions/runs/41511645062),
commit `98a207b9a539ddef956adaaef2be567495e48331`.
Nessun commit, push, deployment o accesso a EC2/staging/produzione.

## Evidenze disponibili e mancanti

Il connettore GitHub ha restituito inventario degli artefatti, stati/step dei
job e log grezzi dei job fresh, upgrade e final-gate. Sono conservati localmente
in `.diagnostics/run7-source/`. L'artefatto diagnostico è identificato da
`13178165611`, SHA256
`ca20e05cf292f42b78c10f06765e617e56831fafa883dc77b38d02cab6bdc6b7`.
Il connettore download restituisce un URL ZIP, non i file estratti; il download
via `gh` è stato rifiutato dalle restrizioni di rete del processo locale.
**Il JSON diagnostico completo non è stato letto.** I 14 fallimenti/12 metodi e
i 60 BLOCKED citati nella richiesta restano dati forniti dall'utente, non una
nuova analisi indipendente dei singoli stack trace. Gli artefatti pubblicati
rimangono intatti nel run originale.

Non è disponibile Docker nel PATH. Non è stato individuato un server effimero
MySQL 8.4.13 autorizzato; i binari locali di altre versioni non sono stati usati
come sostituti. Python 3.13 incluso in MySQL Workbench è stato utilizzato per
le verifiche offline, con directory DLL esplicita per il modulo SSL.

## Cause confermate nel codice e modifiche

1. `tools/mysql84_release.py`, `install()`: il vecchio percorso scriveva stdin
   direttamente e confrontava una stringa marker con eventi `(stream, riga)`.
   Quel confronto non può riuscire. Ora `Mysql.execute_script()` usa la stessa
   gestione risposte di `execute()`, mantenendo i DELIMITER del bundle. Errori
   SQL, EOF e timeout avvelenano la connessione, conservano il journal `started`
   e richiedono riconciliazione. Nessun retry del DDL. Un risultato senza
   conferma è dichiarato potenzialmente commesso anche quando il client fallisce.
2. `tests/Integration/SqlLockEvidence.cs`, `WaitCoreAsync()`: query 5.7 usata
   senza selezione server. La strategia ora usa VERSION() reale: 8.4 legge
   `performance_schema.data_lock_waits`, collegando entrambe le transazioni a
   `INNODB_TRX.trx_mysql_thread_id`; 5.7 conserva la query storica. Versioni
   diverse falliscono esplicitamente. Snapshot dell'oggetto connessione, ID
   fisico e generazione delle transizioni impediscono il riuso di evidenza
   dopo chiusura/riapertura. Il pacing non può produrre PASS senza arco SQL.
3. Readiness: `Mt3cProgramHost.StartAsync()` non forniva versione, host e datadir
   attesi del container; inoltre `OperationalPipeline` confrontava la porta
   interna con la porta pubblicata. `Mysql84FixtureTarget.ReadinessArguments()`
   espone l'identità registrata dalla fixture; `ExpectedServerPort` consente una
   porta server configurata esplicitamente, mantenendo il confronto esatto e
   tutti i controlli su database, datadir, marker e routine. Il default resta
   la porta della connessione. Nessuna readiness permissiva.
4. `Quality_ReadinessRequiresReleaseMarkerAndExecutePermission()` copre ora
   separatamente marker storico, closure e marker 8.4 quando pertinente, oltre
   a revoca/concessione/revoca EXECUTE. L'account usa `%` esclusivamente nel
   database di un container posseduto dalla fixture, per il trasporto pubblicato.
5. Suite tenant: l'ownership viene ora verificata per metodo, eliminando la
   doppia assegnazione dei metodi Calendar_, EmailPolicy_ e BulkImport_. Sei
   shard paralleli mantengono processo/database individuali; nessuna nuova
   condivisione fixture è stata autorizzata. Il budget resta 3600 secondi per
   shard, senza aumentare il limite precedente. Fresh precede upgrade nella
   matrice seriale; integrazione dipende dai gate release e diagnostica.

## Punti ancora irrisolti

- **Schema drift `identity_account_state`: BLOCKED.** Il DDL reale 8.4.13 non è
  stato raccolto. Nessuna canonicalizzazione o modifica del contratto è stata
  introdotta per questo errore. `verify_schema()` conserva ora SHOW CREATE
  originale, versione, diff rispetto al contratto e righe di COLUMNS,
  STATISTICS, TABLES, REFERENTIAL_CONSTRAINTS e KEY_COLUMN_USAGE nell'errore
  originale. Il DDL atteso è riportato sotto; quello reale resta da acquisire.
- **GUID/collation: BLOCKED.** `VerifyGuidTypesAsync()` conserva il controllo
  originale, inclusa l'eccezione `int(11)`. In caso di rifiuto aggiunge metadati
  di tutti gli identificativi. Non è stata confermata la colonna responsabile;
  non è stato equiparato `int` a `int(11)` senza la prova server richiesta.
  I casi semantici positivi/negativi della correzione GUID restano da aggiungere.
- **CLI Windows: BLOCKED.** I tre metodi effettivamente scoperti sono assegnati
  a `windows-cli`, esclusi dall'invocazione Linux e riportati come obbligatori
  BLOCKED dal gate finale. Nessun mock, PASS o SKIPPED sostitutivo. Manca ancora
  il provisioning di un host amministrativo Windows protetto con database
  effimero compatibile: non è stato implementato un job Windows eseguibile.
- Fresh completo, sequenza upgrade/recovery, osservatore lock, regressioni
  readiness e GUID e tutte le suite runtime: **BLOCKED localmente**. Il workflow
  prepara l'esecuzione MySQL 8.4.13 con l'immagine già vincolata, ma ciò non
  costituisce una prova di compatibilità delle modifiche.
- Actionlint e misurazione delle durate dei nuovi shard in CI: non eseguiti.

## Test aggiunti e risultati locali

- `tools/test_mysql84_protocol.py`: cinque test totali PASS (circa 1,05 s),
  inclusi due nuovi test su bundle, DELIMITER, errori, EOF, timeout e no-reuse.
  Sono test di processi/pipe con client sintetico, non prove SQL reali.
- `tools/test_integration_partition.py`: cinque test su copertura esatta di 193
  metodi, bilanciamento, determinismo, ownership, routing Windows, rifiuto di
  configurazioni vuote/errate, Theory incompleta e riepilogo duplicati/storia.
- `SqlLockEvidenceTests`: quattro metodi, cinque casi reali, compilati ma non
  eseguiti. Coprono arco reale, connessione estranea, requester riaperto,
  dipendenza SQL assente, blocker chiuso e task non associato.
- Tutti i test Python: **100 PASS, 0 FAIL, 0 errori**, 9,100 s.
- Unit test C#: **598 PASS, 0 FAIL, 0 SKIPPED**, durata test 269 ms.
- Configurazione fixture: **4 PASS, 0 FAIL, 0 SKIPPED**, durata test 15 ms.
- Build integrazione: **0 errori**, 10 warning nullable preesistenti, 2,44 s
  nell'ultima build completa osservata.
- TRX locali: `.diagnostics/run7-local/`; inventario reale discovery:
  `.diagnostics/run7-method-assignments.json`. La discovery non avvia database.

La discovery dei binari reali assegna **355 metodi distinti**: calendar 41,
formative 56, email 26, imports 29, IAM 22, onboarding portabile 40,
tenant 138 (6 × 23), Windows 3 BLOCKED. Ogni metodo ha una sola assegnazione.
Il runner conserva anche i conteggi dei casi scoperti e rifiuta il PASS quando
il TRX contiene meno casi. Il gate confronta inventari, shard, union e discovery
originale; report mancanti o incompleti impediscono PASS.

`final_ci_diagnostic.py` mantiene errori completi nel JSON e artefatti grezzi,
aggiungendo riepiloghi per metodo e occorrenza, BLOCKED, infrastruttura, schema,
protocollo, osservatore lock ed evidenze storiche non certificabili. I gruppi
diagnostici non sostituiscono l'analisi degli stack trace.

## Contratti storici e durata

Rigenerati soltanto manifest/contratto della candidata con
`node tools/mysql84-release.cjs --write`. Il contratto candidato non presenta
modifiche; la chiusura candidata include le nuove sorgenti. La verifica compara
gli input storici con il commit immutabile
`8e7d9ecd42c001f1fa9b30e120535ce1bb0ef6e8`.
Nessun manifest storico o prova pubblicata è stato modificato.

Run #7: circa 61 minuti (dato della richiesta). Nessuna durata runtime
comparabile è disponibile per questa revisione. I sei shard riducono il numero
di metodi tenant per runner a 23 e rimuovono duplicazioni tra domini; il
miglioramento effettivo resta da misurare. Nessuna stima è usata come PASS.

**Conclusione: NON READY.** Restano necessari acquisizione e analisi del JSON
originale completo, metadati 8.4.13, soluzione drift/GUID basata su tali prove,
ambiente Windows e tutte le esecuzioni runtime obbligatorie.

## DDL atteso `identity_account_state`

```sql
CREATE TABLE `identity_account_state` (

  `user_id` char(36) CHARACTER SET ascii NOT NULL,

  `generation` bigint NOT NULL DEFAULT '0',

  `display_name` varchar(160) COLLATE utf8mb4_unicode_ci DEFAULT NULL,

  PRIMARY KEY (`user_id`),

  CONSTRAINT `fk_identity_state_user` FOREIGN KEY (`user_id`) REFERENCES `user` (`id`) ON DELETE RESTRICT

) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
```
