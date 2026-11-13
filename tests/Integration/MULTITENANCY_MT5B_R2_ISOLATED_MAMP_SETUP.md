# MT-5B-R2 — MAMP isolato e restore MT-4C

21 ottobre 2026. Istanza isolata creata e dump importato una volta. **Checkpoint
bloccato alla validazione esatta:** cinque trigger hanno SQL_MODE memorizzato
diverso dalla baseline. Non applicati candidati MT5B, correzioni o test .NET.

## Stato verificato alla ripresa

L'interruzione è avvenuta dopo inizializzazione e configurazione credenziali,
prima dell'avvio TCP definitivo e prima del restore. Alla ripresa:

- `initialization.json` e `credential-setup.json` attestavano esito positivo.
- Bootstrap PID 25072 già terminato, log con shutdown completo regolare.
- Nessun listener 13306; nessun marker `restore.started` né esito restore.
- Configurazioni invariate; snapshot completo 3306 identico a quello iniziale.

Non sono stati ripetuti initialize, ALTER USER, CREATE DATABASE o CREATE USER.
Si è ripreso dall'avvio TCP del datadir già preparato e dal primo import.

## Identità effettive

| Voce | Baseline | Isolata |
| --- | --- | --- |
| Versione SQL | 5.7.24 | 5.7.24 |
| Hostname SQL | CES01622 | CES01622 |
| Eseguibile | C:\MAMP\bin\mysql\bin\mysqld.exe | Stesso binario MAMP |
| PID | 9408, avviato 10:00:35 | 2156, avviato 11:02:13 |
| Listener | 0.0.0.0:3306 | **127.0.0.1:13306** |
| Datadir SQL | C:\MAMP\db\mysql\ | `<repo>/migrations/mt4/private/mt5a-mamp-13306/data/` |
| DATABASE() | electronic_register_test | electronic_register_test |
| Charset/collation database | utf8mb4 / utf8mb4_unicode_ci | Identici |
| Server UUID / pid-file | Verificati | Entrambi distinti dalla baseline |

Processo, listener, query SQL, bind_address e PID file sono stati correlati.
Nessun fallback di connessione. Entrambi i processi risultano attivi; la 3306 non
è stata arrestata o riavviata. Nessun intervento su XAMPP o servizio permanente.

GLOBAL sql_mode identico su entrambi e invariato sulla baseline:
`ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION`.

## Percorsi, configurazione e sicurezza

Area nuova, inizialmente assente:
`migrations/mt4/private/mt5a-mamp-13306/`.
Percorso canonico verificato sotto il workspace, senza reparse point negli
antenati controllati, separato da C:\MAMP\db\mysql. Nessun datadir preesistente
sovrascritto. Porta 13306 verificata libera prima degli avvii.

ACL private con ereditarietà disabilitata sulla radice: proprietario workspace,
account esecutore sandbox, SYSTEM e Administrators; niente Users generico.
ACL ereditate del datadir controllate. Area esclusa da Git, inclusi client.ini,
credenziali, dump, log, config e file InnoDB. Spazio iniziale circa 296,8 GB;
296.380.694.528 byte disponibili al controllo finale riportato.

Configurazione privata `my.ini`, SHA256
`428784F2A3BB69CCC0F5E1FDC1336E73A1E3624356EF78E51FE1EE128CBF77E9`:

- basedir C:/MAMP/bin/mysql; datadir, tmpdir, PID e log sotto la nuova area privata.
- port 13306 e bind-address 127.0.0.1.
- Server utf8mb4 / utf8mb4_unicode_ci, sql_mode sopra.
- General log e slow log disabilitati; buffer pool 64M, redo 16M, packet 64M.
- Nessuna modifica a C:\MAMP\conf\mysql\my.ini: hash prima/dopo coincidente.

Procedura eseguita:

1. `mysqld --defaults-file=<config-privata> --initialize-insecure --skip-networking
   --console`, **una volta** alle 10:57:10, processo di inizializzazione concluso.
2. Bootstrap separato con `--skip-networking --shared-memory` e nome memoria
   condivisa dedicato. SQL verificava porta 0, skip_networking=1 e datadir privato;
   nessun listener TCP. Password root impostata mediante stdin del client MEMORY,
   poi autenticazione con la nuova password verificata prima dell'avvio TCP.
3. Creati database vuoto e definer er_test@localhost con password nuova e ACCOUNT
   LOCK; grant esclusivamente sul database test. Nessuna copia di mysql.*.
4. Shutdown regolare del solo bootstrap via mysqladmin MEMORY. Alla ripresa
   processo assente e log di shutdown verificati.
5. Avvio con `mysqld --defaults-file=<config-privata> --standalone`, finestra nascosta,
   senza flag MEMORY/skip-networking. Root aveva già password verificata.

Credenziali nuove generate crittograficamente e conservate solo nell'area privata.
Root differente da quello della baseline; nessun account anonimo, nessun root
senza password al controllo finale. er_test è un definer bloccato al login, non
un account applicativo già collaudato. Nessun segreto su CLI o nel report;
generazione, SQL password e relativi output restano privati. General log e binlog
risultano disabilitati sull'isolata. Il log segnala un warning SSL per mancanza
di certificato/chiave utilizzabili, senza errori fatali: listener solo loopback,
connessione con password riuscita. Nessuna modifica TLS eseguita.

## Preflight, dump e backup

Preflight 3306 reale: 32 tabelle/53 trigger/98 FK/139 indici, validator 7+5 zero,
98 FK senza orfani, ledger vuoto e revision zero. Acquisiti snapshot completi
di dati e metadati prima, alla ripresa e alla fine: tutti identici sulla baseline.

Sorgente originale usata, invariata:
`migrations/mt4/private/mt4c-new-mamp-20261021/final-mt4c.sql`.
SHA256 **C7376B79D3FCFFE9C8F9911538516DBF0245072B57162D14079FD01DEEF27555**.

Audit del dump: 32 CREATE TABLE, 53 trigger, footer presente, nessun DROP/TRUNCATE,
SQL account, GRANT/REVOKE, SET GLOBAL, target qualificato esterno, LOAD DATA o
file output. Un solo CREATE DATABASE IF NOT EXISTS e un solo USE, entrambi per
electronic_register_test; DEFINER solo root@localhost ed er_test@localhost.
Confrontate le CREATE TABLE con quelle del server baseline e le tuple di tutte
le 32 tabelle tra dump storico e nuovo backup baseline: coincidenti.

Il database isolato era stato creato nuovo e verificato vuoto (anche routine ed
eventi), con charset/collation corretti e i due DEFINER disponibili. Inventario
iniziale salvato privatamente. Import eseguito **una volta alle 11:05:09**, solo
via TCP esplicito 127.0.0.1:13306, senza --force, senza modificare il dump.
Client con --one-database e USE esplicito del database test; audit preventivo dei
target necessario perché il solo filtro client non blocca nomi qualificati.
Nessun restore, DDL o DML inviato alla 3306.

Nuovi backup sotto la radice privata isolata, limitati alla base test:

| Backup | Byte | SHA256 |
| --- | ---: | --- |
| baseline-r2-before.sql, da 3306 | 162.090 | 3ABBA725E6CCB66036317FB672517C5D59CB87686A8D779E17E510D4B6FDCDC3 |
| isolated-mt4c-before-mt5b.sql, da 13306 | 162.090 | 1DC2B5A53DFA268356EDB323EC41C87D54BE2A4D17E2691343606D7C8BADC9CC |

Exit code zero, footer, 32 definizioni tabella, 53 trigger, target unico e DDL
verificati; SHA256 riletto a chiusura. I file hanno hash diversi per metadati di
dump e non sono dichiarati identici byte per byte. **Il backup isolato conserva
lo stato con la differenza SQL_MODE descritta sotto: non autorizza MT5B.**

## Controlli reali sull'isolata

| Controllo | Risultato |
| --- | --- |
| Tabelle / trigger / FK / indici | **32 / 53 / 98 / 139** |
| FK orfane | **0 su tutte le 98**, incluse composite |
| Validator MT4A 003 | **7 regole zero** |
| Validator MT4C 006 | **5 regole zero** |
| Ledger | Vuoto, validazioni coerenti |
| Revision | Nessuna NULL/diversa da zero |
| Holding | MT3A_LEGACY_TEST Provisioning |
| Dati | **32 tabelle identiche alla baseline**, digest di tutte le righe e molteplicità |
| SHOW CREATE TABLE | Tutte identiche alla baseline e al dump |
| Colonne | Tipi, ordinali, nullable/default, extra, charset/collation ed espressioni coincidenti |
| Indici e FK | Colonne/ordine, unicità, tipo indici, parent e regole coincidenti |
| Trigger | Corpi, DEFINER, tabella/evento/timing, ordine e charset/collation coincidenti |
| SQL_MODE di cinque trigger | **DIVERSO — blocco aperto** |

I confronti sono stati eseguiti tra snapshot SQL reali, con dati serializzati in
esadecimale e NULL distinti; non sono dedotti dai conteggi o dai report storici.

### Blocco: SQL_MODE memorizzato nei cinque trigger MT4C

Trigger interessati:

- mt4c_course_event_delete
- mt4c_course_event_insert
- mt4c_course_event_update
- mt4c_enrollment_revision_insert
- mt4c_enrollment_revision_update

Sulla baseline questi trigger includono **NO_AUTO_CREATE_USER** nel proprio
SQL_MODE memorizzato. Sull'isolata quel singolo flag manca; tutti gli altri flag
coincidono. Il GLOBAL sql_mode invece coincide e include il flag su entrambi.

La causa immediata è dimostrata dall'audit: il dump certificato contiene, prima
di queste cinque CREATE TRIGGER, SET sql_mode espliciti **già privi del flag**.
L'import ha rispettato il dump, senza riscriverlo. Non viene attribuita una causa
non verificata all'omissione originaria da parte del client di dump.
Non viene eliminata la differenza dal confronto come se fosse formattazione.

Il requisito «definizioni identiche alla baseline» non è quindi pienamente
soddisfatto. Nessun DROP/CREATE TRIGGER correttivo, reimport o normalizzazione
SQL_MODE è stato eseguito. Dopo il rilevamento sono state eseguite solo letture,
backup e stesura del report. R2 **non è dichiarato pronto**.

Possibile intervento per un checkpoint successivo, da autorizzare specificamente:
ricreare **solo i cinque trigger sull'isolata**, dopo backup verificato, usando
SHOW CREATE/TRIGGER metadata della baseline per conservare corpo/DEFINER/charset/
ordine e SQL_MODE di creazione originale. Richiede piano preciso dei singoli
statement e dei commit impliciti; non comporta modificare la 3306, il dump storico
o ripetere il restore. In alternativa, accettare la differenza richiede una
decisione esplicita motivata: questa esecuzione non la presume irrilevante.

## Compatibilità delle guardie future — sola lettura

| Componente | Riscontro |
| --- | --- |
| MembershipFixturePurge.cs | Versione, porta, host e suffisso datadir soddisfatti; nessuna capability o purge eseguiti |
| Mt5bSecurityStateTests.cs | Identità, DB e GLOBAL mode attesi soddisfatti; nessun test eseguito |
| Run-Mt5bPersistence.ps1 | Listener/binario/identità corretti, ma confronto path corrente non coincide: Replace('\\','/') tratta due barre, non la singola barra Windows |
| Default mysqldump del runner | Percorso MySQL 8.0 assente; non utilizzare opzioni MySQL 8 con il client 5.7 senza revisione |
| Candidati R1 | Hash tutti invariati; root/DB/versione/porta/suffisso corrispondono staticamente al profilo; **nessun 000–005 eseguito** |

Le correzioni di normalizzazione e il driver R1 restano da revisionare nel prossimo
checkpoint: non sono state modificate guardie per far passare artificialmente il
target. SQL originali nella directory del runner ancora assenti; candidati restano
separati. Sul database isolato verificata assenza sia della sidecar MT5B sia di
trigger mt5b_.

## Problemi del helper e perimetro finale

- Prima dell'inizializzazione, il nuovo backup era stato fermato da un'opzione
  connect-timeout non supportata da mysqldump 5.7. Nessun file backup era stato
  creato. Corretto soltanto il helper privato; preflight e backup poi riusciti.
- Prima del restore, il filtro --one-database ignorava query senza USE esplicito.
  Corretto il preambolo del helper; marker restore ancora assente e nessuna DDL
  importata in quel tentativo. L'import effettivo è avvenuto una sola volta.
- La differenza SQL_MODE è invece una differenza reale e rimane irrisolta.

Non ripetute inizializzazione o configurazione credenziali alla ripresa. Nessun
retry di restore falliti, nessuna riparazione automatica. I processi baseline e
isolato sono lasciati attivi; il bootstrap locale era stato chiuso regolarmente.

Evidenze private: initializzazione/configurazione credenziali senza segreti nel
riepilogo, resume-audit, baseline-before/resume/after, source-audit, manifest dei
backup, isolated-before-restore, restore, isolated-final-validation,
final-blocked-verification, security-final e distinzione server UUID/PID file.

Git: branch refactor-database-entities, HEAD invariato
92cb8141b9555a36c00e517cb4cddc50d0dd7485. Aggiunto solo questo report pubblico;
restano le modifiche .gitignore e i candidati di R1 e i precedenti report locali.
Tutto il nuovo materiale di istanza è sotto mt4/private, escluso da Git.
Hash dei sei candidati invariati, nessuna modifica backend/frontend/JWT o SQL
canonico, nessun commit/push. Nessun test .NET o teardown eseguito.

La 3306 è invariata nei dati, nelle definizioni, nel GLOBAL mode e nella config
ai controlli prima/ripresa/dopo; non si afferma un'identità fisica dei suoi file
InnoDB mentre il server è in esecuzione. **Fase fermata alla revisione del blocco.**

**MT-5B-R2 BLOCKED — REVIEW REQUIRED**
