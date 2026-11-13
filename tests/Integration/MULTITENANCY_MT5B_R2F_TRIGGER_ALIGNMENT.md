# MT-5B-R2F — allineamento SQL_MODE trigger MT-4C

21 ottobre 2026, conclusione circa 11:25 Europe/Rome. Risolto il blocco SQL_MODE
documentato in R2 mediante **sole cinque ricreazioni autorizzate sulla 13306**.
Nessuna inizializzazione, configurazione utenti o restore ripetuti.

## Preflight e target verificati

| Voce | Baseline, sola lettura | Isolata, unico target DDL |
| --- | --- | --- |
| Host TCP / porta | 127.0.0.1:3306 | 127.0.0.1:13306 |
| Listener / PID | 0.0.0.0:3306, PID 9408 | 127.0.0.1:13306, PID 2156 |
| Eseguibile | C:\MAMP\bin\mysql\bin\mysqld.exe | Identico binario MAMP |
| Versione / hostname SQL | 5.7.24 / CES01622 | 5.7.24 / CES01622 |
| Datadir SQL | C:\MAMP\db\mysql\ | `<repo>/migrations/mt4/private/mt5a-mamp-13306/data/` |
| DATABASE() | electronic_register_test | electronic_register_test |
| Charset/collation schema | utf8mb4 / utf8mb4_unicode_ci | Identici |
| Tabelle / trigger / FK / indici | 32 / 53 / 98 / 139 | 32 / 53 / 98 / 139 |

Processo, listener e identità SQL verificati prima del lavoro, prima delle singole
ricreazioni e alla validazione finale; nessun fallback. Acquisiti nuovi snapshot
SQL di dati e schema. Validator MT4A 7/7 e MT4C 5/5 a zero su entrambi i server,
98 FK senza orfani su ciascuno. Dati e oggetti iniziali coincidenti salvo le cinque
differenze SQL_MODE attese. Nessun test .NET o fixture eseguito.

### Assenza di writer

Nessun processo dotnet/testhost rilevato. Sull'istanza isolata le query reali a
PROCESSLIST escludevano connessioni diverse dal verificatore corrente; INNODB_TRX
restituiva zero transazioni, EVENTS zero eventi. Controlli ripetuti immediatamente
prima di ogni coppia DROP/CREATE e alla fine. Non c'erano writer da sospendere:
nessun kill, modifica account, cambio configurazione o GLOBAL read_only applicato.
Manutenzione limitata all'istanza privata, senza altri client ammessi durante DDL.

## Definizioni preservate e differenze iniziali

Acquisiti **tutti i campi information_schema.TRIGGERS e SHOW CREATE TRIGGER per
tutti i 53 trigger su entrambi i server**, prima del primo DROP. Conservati
separatamente sotto:
`migrations/mt4/private/mt5a-mamp-13306/r2f-20261021/`:

| File privato | SHA256 |
| --- | --- |
| baseline-triggers-before.json | 42650CC668D99C71983F9EFFD363CC84586B3B58692F25376EB207E8117423E5 |
| isolated-triggers-before.json | 92C129C709F41138E8D105A6D692D611CAB154C7F8DCC0BDA0FEC5D8BBE6F6FA |

Unica differenza di definizione/metadati di esecuzione: sull'isolata mancava
NO_AUTO_CREATE_USER nel SQL_MODE dei cinque trigger autorizzati. Gli altri flag,
corpi, DEFINER, schema/tabella, timing/evento, ordine e charset/collation coincidevano.

SQL_MODE iniziale isolato, soltanto per i cinque trigger:
`ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION`.

SQL_MODE della baseline e risultato finale isolato:
`ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION`.

I campi CREATED/Created sono stati salvati come provenienza: i timestamp fra due
istanze già differivano per il restore R2 e non costituiscono il corpo o il contesto
di esecuzione. Le cinque ricreazioni autorizzate aggiornano necessariamente i loro
timestamp; non si dichiara di averli retrodatati. Sugli altri 48 trigger sono stati
confrontati e preservati **anche** questi campi, oltre a tutti gli altri metadati.

## Backup prima della DDL

Prima del primo DROP acquisito e verificato:
`migrations/mt4/private/mt5a-mamp-13306/r2f-before-trigger-alignment.sql`.

- Solo electronic_register_test sulla 13306, client MySQL 5.7.24, single-transaction,
  routine/eventi/trigger inclusi, GTID OFF, niente --force o add-drop-table.
- Exit code 0, footer presente, 32 CREATE TABLE e 53 definizioni trigger, 162.090 byte.
- SHA256 **E254C71744723BE2BA6E313CC873E32FE2E4286076A1AA142E59318422E65077**.
- Area privata ereditata dalla radice isolata: soltanto proprietario workspace,
  account esecutore sandbox, SYSTEM e Administrators; esclusa da Git.
- Spazio iniziale verificato: 296.374.153.216 byte disponibili.

Non modificati dump storico, backup precedenti o credenziali. Tutti i cinque file
di ricreazione erano già acquisiti dalla baseline, validati e corredati da SHA256
prima di qualsiasi DDL; il backup è stato nuovamente controllato prima dell'applicazione.

## Interventi realmente eseguiti

Cinque coppie **DROP TRIGGER / CREATE TRIGGER**, ciascuna una sola volta:

| Trigger | Tabella / timing / evento | ACTION_ORDER | Ora avvio coppia |
| --- | --- | ---: | --- |
| mt4c_course_event_delete | course_administration_event / BEFORE DELETE | 1 | 11:22:56 |
| mt4c_course_event_insert | course_administration_event / BEFORE INSERT | 1 | 11:23:00 |
| mt4c_course_event_update | course_administration_event / BEFORE UPDATE | 1 | 11:23:04 |
| mt4c_enrollment_revision_insert | student_enrollment / BEFORE INSERT | 2 | 11:23:07 |
| mt4c_enrollment_revision_update | student_enrollment / BEFORE UPDATE | 2 | 11:23:11 |

Per ogni coppia: identità dei due server e quiescenza ricontrollate; client TCP
esplicito sulla 13306 con database test e filtro --one-database/USE esplicito.
Applicati SET SESSION sql_mode, character_set_client e collation_connection
derivati dalla baseline, poi DROP qualificato del solo trigger previsto e CREATE
ricavata dal suo SHOW CREATE TRIGGER effettivo. Nessuna riscrittura di corpo,
DEFINER, tabella/timing/evento o stringhe SQL. Il delimitatore client non fa parte
della definizione SQL. Nessun SET GLOBAL eseguito.

Contesto di creazione di tutti e cinque:

- DEFINER **root@localhost**.
- CHARACTER_SET_CLIENT **utf8mb4**.
- COLLATION_CONNECTION **utf8mb4_general_ci**.
- DATABASE_COLLATION **utf8mb4_unicode_ci**, schema già verificato uguale.

I tre trigger del ledger sono unici nei rispettivi gruppi e conservano ordine 1.
I due trigger revision sono ultimi, ordine 2 dopo i rispettivi trigger MT4A;
drop e ricreazione con definizione originale li riappendono correttamente.
Queste condizioni sono state validate prima del primo DROP e ricontrollate
sul server dopo ciascuna coppia. Non necessarie clausole FOLLOWS/PRECEDES nuove.

I CREATE erano già compilati e funzionanti sul server baseline della stessa
versione, con dipendenze/schema uguali; header, corpo e contesto erano stati
confrontati prima della DDL. Nessuna creazione di trigger temporanei per provarli.
Commit impliciti gestiti come previsto: nessun errore DDL, retry o rollback
distruttivo automatico; marker ed esiti per le cinque coppie conservati privatamente.

## Validazione finale sul database reale

| Verifica | Esito |
| --- | --- |
| Tabelle / trigger / FK / indici | **32 / 53 / 98 / 139** |
| FK orfane | **0 su tutte le 98** |
| MT4A 003 | **7 controlli zero** |
| MT4C 006 | **5 controlli zero** |
| SQL_MODE cinque trigger | Identico alla baseline, incluso NO_AUTO_CREATE_USER |
| Tutti i 53 corpi e DEFINER | Identici alla baseline |
| Charset/collation e ACTION_ORDER | Identici alla baseline |
| Altri 48 trigger | Invariati, inclusi timestamp di creazione |
| Dati di tutte le 32 tabelle | Identici allo snapshot iniziale e alla baseline |
| Tabelle/colonne/indici/FK | Nessuna modifica |
| Ledger / revision / holding | Ledger vuoto, revision zero, holding Provisioning |
| Baseline 3306 | Snapshot iniziale/finale e metadati SHOW CREATE completi identici |
| GLOBAL sql_mode | Invariato e identico su entrambi i server |

Confronti su definizioni complete e su valori serializzati in esadecimale,
NULL distinti e molteplicità delle righe; non semplici conteggi. Fingerprint del
nuovo snapshot semantico isolato, coincidente con quello baseline:
`FC6DF04E520A46B4084D9007A825D40DC354CE0C231CB80A0E20133886D92F22`.
Esito completo in `r2f-20261021/final-verification.json`.

Non restano differenze di definizione, ordine o contesto di esecuzione. I timestamp
delle cinque CREATE cambiano come conseguenza prevista dell'intervento; gli altri
48 restano invariati. Nessuna continuità fisica dei file InnoDB della 3306 viene
dedotta dalle letture: sono certificati i dati, metadati e GLOBAL mode osservati.

## Backup finale completo e limite del client di dump

Nuovo dump privato:
`migrations/mt4/private/mt5a-mamp-13306/r2f-final-mt4c.sql`.
162.090 byte, exit code 0, footer/inventario 32/53 verificati, SHA256
**CB24A5A028E2EF566A37AFFB9603656F4118F4DEDE71F33D40FF78D87B400EE1**.
Anche le tuple del backup prima/dopo coincidono per tutte le 32 tabelle.

**Il dump grezzo da solo non preserva il flag:** il client continua a omettere
NO_AUTO_CREATE_USER nei cinque SET sql_mode del dump, pur essendo il flag ora
presente sul server. Verificata esplicitamente la discrepanza su quei cinque e
solo quei cinque trigger, senza alterare il dump per nasconderla.

Il backup completo da conservare e utilizzare insieme comprende quindi:

| Componente | SHA256 |
| --- | --- |
| Dump r2f-final-mt4c.sql | CB24A5A028E2EF566A37AFFB9603656F4118F4DEDE71F33D40FF78D87B400EE1 |
| r2f-20261021/isolated-triggers-after.json, tutti i 53 SHOW CREATE e metadati | A7623D91BD56CAA1AFB0BEE5A88218496401CC5E73D04CFD8B58232DA6AB551E |
| r2f-20261021/final-backup-sqlmode-overlay.sql, sole cinque ricreazioni con contesto esatto | 952BEE2ADC9A7ABC62F468E00298C4879116F53091CEA3C5B1D6036EE5592B77 |

Manifest del set: `r2f-20261021/final-backup-bundle.json`; hash ricalcolati a
chiusura e confronto tuple in `backup-integrity-final.json`. L'overlay archivia le
stesse cinque definizioni già applicate, **non è stato eseguito dopo il backup**.
Un eventuale futuro restore richiede quel contesto/overlay e una nuova
autorizzazione: non usare il solo dump come prova di equivalenza SQL_MODE e non
eseguire l'overlay automaticamente su un server non verificato.

## Stato Git e chiusura

Branch refactor-database-entities, HEAD invariato
92cb8141b9555a36c00e517cb4cddc50d0dd7485. Questo checkpoint aggiunge solo il presente
report pubblico e materiale nella directory privata ignorata; restano le modifiche
.gitignore e i candidati R1, nonché i precedenti report locali. Nessun dump,
credenziale o log aggiunto a Git; `git check-ignore` e `git diff --check` verificati.

Nessuna modifica a runner/guardie, candidati MT5B, SQL canonici, codice applicativo,
JWT o frontend. Nessun commit/push. Nessun candidato 000–005 applicato, test .NET,
teardown o altra riparazione. Entrambi i server lasciati attivi con gli stessi PID.

La 13306 è ora la baseline isolata MT4C per una successiva fase MT5B **ancora da
autorizzare**. Il presente report risolve il blocco storico R2 senza riscriverlo.
Lavoro fermato al termine di R2F.

**MT-5B-R2 ISOLATED BASELINE READY**
