# Nuovo MAMP Windows — ripartenza MT-5B

## Aggiornamento operativo — database creato manualmente

Verifica successiva del 21 ottobre 2026, solo letture. Questo aggiornamento
supera il precedente esito «database assente» e la precedente esclusione del dump
locale: il confronto mirato con le evidenze MT5A ne conferma ora la riproducibilità.

- Target SQL verificato: **5.7.24 / CES01622 / 3306 / C:\MAMP\db\mysql\ /**,
  processo MAMP PID 9408 correlato al listener.
  DATABASE() = **electronic_register_test**, charset utf8mb4, collation
  utf8mb4_unicode_ci; **0 tabelle/view, 0 routine, 0 eventi**. Nessuna tabella
  preesistente da preservare. Backup dello schema vuoto comunque previsto.
- Account non bloccati: root@localhost, er_test@localhost e er_test@127.0.0.1,
  plugin mysql_native_password. Root ha privilegi globali amministrativi;
  er_test ha privilegi sul solo electronic_register_test, inclusi DML,
  CREATE/ALTER/DROP/INDEX/REFERENCES/TRIGGER/EXECUTE e LOCK TABLES, senza GRANT OPTION.
  Nessuna password o GRANT modificati. Autenticazione root riuscita; password/login
  er_test non provati. I test MT5B con purge DBA richiedono ancora root e le guardie
  13306/datadir isolato: i GRANT er_test non eliminano questo blocco.
- DEFINER nel dump: root@localhost ed er_test@localhost, entrambi presenti.
  Root può importare i DEFINER esistenti; non servono oggi nuovi account o GRANT.
- Dump `migrations/mt4/private/20261019-165115-0036.sql`: SHA256
  `80797A2C793366040D9B231544FA1AC7CD0770E5706F7AB6EDB806E88BBC022A`,
  identico a `mt5a-mamp-execution/preflight.json`. Nessun CREATE DATABASE, USE,
  DROP TABLE o SET GLOBAL rilevato; footer presente. Le evidenze del precedente
  restore MySQL 5.7.24 riportano **31 tabelle/48 trigger/92 FK/131 indici**,
  31 tabelle dati confrontate senza mismatch; dopo MT4C **32/53/98/139**.
  Non è un dump operativo scelto come fixture: è la stessa sorgente test già
  verificata nel recupero precedente. La nuova esecuzione resta da validare.
- Tutti i sei SQL MT4 coincidono con `mt5a-mamp-plan/canonical-sql-hashes.json`.
  Verificati anche gli hash di preflight.json, restore.json, migration.json,
  restored-schema-validation.json e restored-data-verification.json contro
  `mt5a-mamp-execution/artifact-hashes.json`: tutti coincidenti.
- **Percorso supportato dalle prove:** restore del dump invariato → controllo
  MT4A → solo `004_enrollment_audit_expand.sql` → `005` → `006`.
  **Non riapplicare MT4 001/002**, già rappresentati nel dump.
- I sei SQL MT5B 000–005 sono ancora assenti. Recuperarli byte per byte dal
  precedente checkout MAMP e verificarli col manifest MT5B; nessuna ricostruzione.

### Comandi proposti, NON eseguiti

Eseguire dalla radice del repository solo dopo autorizzazione. Prima di ogni
fase riverificare listener→PID→eseguibile e identità SQL; interrompere per
qualunque differenza da versione/porta/datadir/database sopra. Nessun --force.
Password solo al prompt interattivo. Directory privata di output nuova, ACL
controllate e percorso non preesistente; comandi di copia dump non sovrascriventi.

```powershell
$mysql = 'C:\MAMP\bin\mysql\bin\mysql.exe'
$dump = 'migrations/mt4/private/20261019-165115-0036.sql'
if ((Get-FileHash -LiteralPath $dump).Hash -ne '80797A2C793366040D9B231544FA1AC7CD0770E5706F7AB6EDB806E88BBC022A') { throw 'Dump differente' }
$expected = Get-Content tests/Integration/TestResults/mt5a-mamp-plan/canonical-sql-hashes.json -Raw | ConvertFrom-Json
foreach ($item in $expected) {
    if ((Get-FileHash (Join-Path migrations/mt4 $item.File)).Hash -ne $item.Hash) { throw "SQL differente: $($item.File)" }
}
& $mysql --no-defaults --protocol=TCP --host=127.0.0.1 --port=3306 --user=root --password --database=electronic_register_test --batch --execute="SELECT VERSION(),@@hostname,@@port,@@datadir,DATABASE(); SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE();"
if ($LASTEXITCODE) { throw 'Preflight fallito' }
# Verificare identita esatta e conteggio 0, poi backup schema vuoto:
$backupDir = Join-Path (Resolve-Path migrations/mt4/private) 'new-mamp-mt4c-restore'
if (Test-Path -LiteralPath $backupDir) { throw 'Directory backup gia presente' }
New-Item -ItemType Directory -Path $backupDir | Out-Null
# STOP fino a verifica/restrizione ACL privata della nuova directory.
& C:\MAMP\bin\mysql\bin\mysqldump.exe --no-defaults --protocol=TCP --host=127.0.0.1 --port=3306 --user=root --password --single-transaction --routines --events --triggers --set-gtid-purged=OFF --databases electronic_register_test "--result-file=$backupDir\empty-before-restore.sql"
if ($LASTEXITCODE) { throw 'Backup fallito' }
Get-FileHash -LiteralPath "$backupDir\empty-before-restore.sql"
# Verificare footer e definizione schema vuoto prima del restore.

# stdin da file, batch senza --force: errore SQL interrompe il client.
cmd /d /c '"C:\MAMP\bin\mysql\bin\mysql.exe" --no-defaults --protocol=TCP --host=127.0.0.1 --port=3306 --user=root --password --database=electronic_register_test --batch < "migrations\mt4\private\20261019-165115-0036.sql"'
if ($LASTEXITCODE) { throw 'Restore fallito: conservare stato parziale, nessun retry' }
cmd /d /c '"C:\MAMP\bin\mysql\bin\mysql.exe" --no-defaults --protocol=TCP --host=127.0.0.1 --port=3306 --user=root --password --database=electronic_register_test --batch < "migrations\mt4\003_validate.sql"'
if ($LASTEXITCODE) { throw 'Validator MT4A fallito' }
# STOP: esigere 7 violazioni zero; inventario 31/48/92/131, definizioni e dati
# del dump conservati, tutte le FK senza orfani, holding Provisioning.
& C:\MAMP\bin\mysql\bin\mysqldump.exe --no-defaults --protocol=TCP --host=127.0.0.1 --port=3306 --user=root --password --single-transaction --routines --events --triggers --set-gtid-purged=OFF electronic_register_test "--result-file=$backupDir\before-mt4c.sql"
if ($LASTEXITCODE) { throw 'Backup pre-MT4C fallito' }
Get-FileHash -LiteralPath "$backupDir\before-mt4c.sql"
# STOP fino a footer/inventario backup verificati e snapshot dati acquisito.
cmd /d /c '"C:\MAMP\bin\mysql\bin\mysql.exe" --no-defaults --protocol=TCP --host=127.0.0.1 --port=3306 --user=root --password --database=electronic_register_test --batch < "migrations\mt4\004_enrollment_audit_expand.sql"'
if ($LASTEXITCODE) { throw 'EXPAND fallito: nessun retry o rollback DDL automatico' }
cmd /d /c '"C:\MAMP\bin\mysql\bin\mysql.exe" --no-defaults --protocol=TCP --host=127.0.0.1 --port=3306 --user=root --password --database=electronic_register_test --batch < "migrations\mt4\005_enrollment_audit_backfill.sql"'
if ($LASTEXITCODE) { throw 'Controllo revision fallito' }
cmd /d /c '"C:\MAMP\bin\mysql\bin\mysql.exe" --no-defaults --protocol=TCP --host=127.0.0.1 --port=3306 --user=root --password --database=electronic_register_test --batch < "migrations\mt4\006_enrollment_audit_validate.sql"'
if ($LASTEXITCODE) { throw 'Validator MT4C fallito' }
```

Ogni STOP commentato è un gate manuale: non incollare l'intero blocco come script
automatico. I validator restituiscono conteggi: exit code 0 da solo non basta.
Esigere invalid_revision=0 in 005, 5 regole zero in 006 e nuovamente 7 zero in 003.
Inventario finale 32 tabelle/53 trigger/98 FK/139 indici, zero orfani, precedenti
48 trigger invariati, dati delle 31 tabelle conservati salvo le nuove revision=0,
ledger vuoto e nessuna storia artificiale. Confrontare colonne/indici/FK con le
evidenze `mt5a-mamp-execution/final-*`; acquisire dump completo finale e SHA256.
Non disabilitare i controlli globali per far passare la validazione.

**Da autorizzare:** area backup privata e backup, restore una volta sul database
vuoto e MT4C 004 con controlli 003/005/006. Nessun nuovo GRANT richiesto dal restore;
configurazione delle guardie test e MT5B restano fasi separate. Nessuna suite,
JWT, modifica applicativa, connessione XAMPP o operazione SQL mutante eseguita
durante questa verifica. Aggiornato soltanto questo report operativo.

Verifica del 21 ottobre 2026. Solo letture e questo report; nessun CREATE,
restore, migrazione, avvio suite, cambio configurazione, commit o push.
Recupero XAMPP abbandonato: nessuna nuova diagnosi o connessione a XAMPP.

## Ambiente e connessione verificati

| Voce | Riscontro attuale |
| --- | --- |
| Installazione | C:\MAMP |
| Processo server | C:\MAMP\bin\mysql\bin\mysqld.exe, PID 9408, avvio 10:00:35 |
| Listener | 0.0.0.0:3306, PID 9408; nessun listener 13306 osservato |
| Versione SQL e binario | MySQL 5.7.24, Community Server (GPL) |
| Host SQL | CES01622 |
| Datadir SQL | C:\MAMP\db\mysql\ |
| Configurazione sul disco | C:\MAMP\conf\mysql\my.ini, porta/datadir coerenti |
| Connessione | TCP esplicito 127.0.0.1:3306, client MAMP, SELECT riuscite |
| Database atteso | electronic_register_test **assente**, verificato con information_schema.SCHEMATA |
| XAMPP | Nessun mysqld XAMPP rilevato; listener 3306 appartiene a MAMP |

Prima della connessione sono stati verificati percorso del processo e PID del
listener. Non è stato usato un fallback automatico sulla 3306. Credenziali lette
dalla configurazione phpMyAdmin MAMP, passate soltanto all'ambiente del client
figlio, senza stampa o password CLI. Nessuna modifica alle credenziali.
DATABASE() restituisce NULL perché la base richiesta non esiste: nessun database
alternativo selezionato. Nessuna tabella applicativa interrogata.

GLOBAL sql_mode verificato, invariato:
`ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION`.
La versione coincide con quella certificata; porta e datadir sono differenti.
Nessun conflitto corrente richiede un cambio porta. Non è necessario modificare
MAMP o intervenire sul vecchio server per completare questo piano.

## Repository e sorgenti

Branch `refactor-database-entities`, HEAD
`92cb8141b9555a36c00e517cb4cddc50d0dd7485`.
All'inizio soltanto i tre report XAMPP precedenti erano non tracciati.
Letti il persistence report, il riepilogo MT5B aggiornato, README/runner MT5 e
`TestResults/mt5b-persistence/final-migration-manifest.json`.
Nessuna rilettura dei report storici MT1–MT5A.

Tutti i sei file seguenti sono **assenti** da migrations/mt5:

| File | SHA256 certificato atteso |
| --- | --- |
| 000_preflight.sql | 084B704332920EC8ED7A12933E9CBEB434E4270D4BAFF64B20953C72B339F6B0 |
| 001_security_state_expand.sql | 56726FA7C20AE488FDFF1A49610E599C18637A86ACC0761A14F783EB0B622AA9 |
| 002_security_state_backfill.sql | 1F6482453EC2BDDE8F6C3248BC6319B19A05DF1734EE88CC40896E8FEFC8EDA7 |
| 003_security_state_validate.sql | 3E0198F5D54C782A16F9225B546FB8A43BF807C8B95B58D737AF97C952450F72 |
| 004_membership_delete_error_contract.sql | ED482156E11D2026AFED7F1D68E1D2BAAA75BFD80D6F89FF0ABC7B4ED8B7EB3D |
| 005_delete_contract_validate.sql | 5D4B5FBC4FAD090FF1F15EAEBD497E3C8B5FBD7E823E4E618FF86AC985CB1054 |

La regola `.gitignore:26:*.sql` continua a escluderli. Nessun hash locale da
confrontare, nessuna migrazione sostitutiva ricostruita. Gli SQL canonici MT1,
MT3 e i sei MT4 sono presenti, ma da soli non costituiscono la base legacy né
l'intero checkpoint. Non occorre ricominciare l'implementazione.

### Dump locali esaminati senza mostrarne i dati

| File sotto migrations/mt4/private | SHA256 | Inventario indicativo |
| --- | --- | --- |
| 20261019-141428-3269.sql | 3F56B13CEBF28735EE177A90FA0D74D6F2972D6F24C2B87B23E71713AF90E756 | 28 CREATE TABLE, 40 definizioni trigger |
| 20261019-165115-0036.sql | 80797A2C793366040D9B231544FA1AC7CD0770E5706F7AB6EDB806E88BBC022A | 31 CREATE TABLE, 48 definizioni trigger |

Entrambi hanno footer Dump completed, intestazione electronic_register_test e
server sorgente **MariaDB 10.4.32**, nessuna sidecar MT5B. Questi controlli non
certificano consistenza o compatibilità MySQL: non sono i dump MAMP certificati
e **non sono scelti per il restore**. Nessun dump MAMP verificato disponibile
nell'area privata di migrazione inventariata. Non si importa alcun datadir.

## Recupero esatto dal precedente computer MAMP

Nel precedente checkout ElectronicRegister recuperare senza conversione newline:

1. I sei `migrations/mt5/000…005*.sql` originali, anche se ignorati da Git.
2. Il dump privato **pre-EXPAND MT5B** sotto
   `migrations/mt4/private/mt5a-mamp-13306/`, individuandolo per SHA256
   `AE82F10E9EA39499FE9FCC1D8B2201485B384678C1B6F2F8D96AF38D087E0D7E`.
   È il predecessore MT4C: 32 tabelle/53 trigger/98 FK, non un dump operativo.
3. In alternativa, un dump finale MT5B della stessa istanza test isolata, se
   effettivamente esistente e corredato da manifest e verifica; non presumere
   che il report documenti un dump finale. Preferire il pre-EXPAND già certificato
   più i sei SQL originali se manca tale evidenza.
4. Eventuali manifest/definizioni canoniche necessari al confronto, senza
   trasferire credenziali, account mysql.* o configurazioni operative.

Comandi **read-only sul precedente PC**, dalla radice del vecchio checkout:

```powershell
$expected = Get-Content tests/Integration/TestResults/mt5b-persistence/final-migration-manifest.json -Raw | ConvertFrom-Json
foreach ($item in $expected | Where-Object {$_.File -like '*.sql'}) {
    $path = Join-Path migrations/mt5 $item.File
    if (!(Test-Path -LiteralPath $path)) { throw "Manca $($item.File)" }
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $item.SHA256) {
        throw "Hash differente: $($item.File)"
    }
}
Get-ChildItem migrations/mt4/private/mt5a-mamp-13306 -Recurse -File -Filter *.sql |
    Get-FileHash -Algorithm SHA256 |
    Where-Object Hash -eq 'AE82F10E9EA39499FE9FCC1D8B2201485B384678C1B6F2F8D96AF38D087E0D7E'
```

Trasferire i sei SQL byte per byte e il dump identificato mediante canale privato;
dump escluso da Git, directory protetta. Ripetere gli hash sul nuovo PC.
Se il dump ha nome/estensione diversi, cercarlo per hash nell'area privata senza
stampare il contenuto. Se sorgenti o hash non coincidono, STOP: recuperare gli
originali, non rigenerarli dal report. Correggere successivamente il packaging
Git con eccezioni limitate ai sei SQL canonici, lasciando esclusi tutti i dump.

## Profilo specifico per questo computer — proposta, non applicata

```json
{
  "Profile": "new-mamp-windows-mt5b-test",
  "ServerExecutable": "C:/MAMP/bin/mysql/bin/mysqld.exe",
  "Host": "127.0.0.1",
  "Port": 3306,
  "Version": "5.7.24",
  "Hostname": "CES01622",
  "DataDirectory": "C:/MAMP/db/mysql/",
  "Database": "electronic_register_test",
  "AllowFallback": false
}
```

Profilo non contiene credenziali. PID acquisito dinamicamente e correlato al
listener prima di ogni sessione; confrontare poi VERSION/hostname/port/datadir e
DATABASE() in SQL. Nessun override che accetti liberamente qualunque datadir.
È una proposta di contratto per runner/helper, non una nuova configurazione già
supportata dall'applicazione. Non modificare my.ini o GLOBAL sql_mode.

Riferimenti locali da gestire prima della suite:

- `migrations/mt5/Run-Mt5bPersistence.ps1`: listener loopback 13306, target SQL,
  datadir isolato, directory backup e client dump MySQL 8.0. Il runner attuale
  **non è eseguibile su questo target**, neppure in Validate.
- `migrations/mt5/Scaffold-Mt5b.ps1`: connessione imposta a 13306; non serve
  rieseguire lo scaffold per riprendere la persistence già implementata.
- `tests/Integration/MembershipFixturePurge.cs`: verifica connessione, versione,
  porta e datadir prima della capability di teardown.
- `tests/Integration/Mt5bSecurityStateTests.cs`: assertion identità MAMP isolata.
- Trigger di teardown negli SQL MT5B: da ispezionare dopo recupero; possono
  rifiutare il nuovo datadir anche con connessione corretta.

Non sostituire soltanto 13306 con 3306. Il profilo deve preservare allowlist del
database test, verifica processo/SQL, owner sintetici enumerati, esclusione holding
reale e reset capability di sessione. Eventuali adattamenti delle guardie SQL
vanno separati dagli originali certificati con hash e revisione esplicita;
non abbassare assertion per ottenere il verde. Questo datadir è quello ordinario
del nuovo MAMP: la futura destinazione dei test va mantenuta dedicata e verificata.
Se è richiesta una nuova istanza isolata equivalente alla precedente, presentare
prima porta libera, datadir nuovo e comando/config esatti per autorizzazione:
non crearla implicitamente. Nessuna scelta di isolamento già applicata.

## Piano minimo di ricostruzione, dopo recupero e autorizzazione

1. **Gate sorgenti:** sei hash coincidenti e dump MAMP pre-EXPAND coincidente;
   ispezionare struttura, footer, charset/collation, DEFINER, USE/CREATE DATABASE,
   DROP e comandi globali prima di proporne l'import. Bloccare riferimenti ad
   altri database, dati operativi o mysql.*. Non assumere che un dump sia innocuo.
2. **Gate target/backup:** riverificare processo/listener e identità SQL sopra,
   assenza della base e client utilizzatori. Se compare uno schema preesistente,
   inventariarlo e acquisire un dump completo privato prima di qualsiasi modifica;
   se la base resta assente, documentare l'assenza come baseline e preservare
   configurazione/inventario MAMP. Nessun backup test inesistente può essere
   prodotto; non occorre modificare o riversare database operativi.
3. **Piano import definitivo:** esplicitare CREATE DATABASE con charset/collation
   originali letti dal dump, nome electronic_register_test e import del solo dump
   verificato. Presentare l'esatto comando e percorso recuperato prima del DDL.
   Oggi non si inventano charset, nome dump o una stringa CREATE non verificata.
4. **Baseline MT4C:** richiedere 32 tabelle/53 trigger/98 FK/139 indici e confronto
   definizioni, zero orfani, validator canonici senza violazioni. Quindi backup
   completo locale pre-MT5B con client MySQL compatibile, footer/inventario/hash.
5. **MT5B:** dopo revisione delle guardie del nuovo profilo, eseguire nell'ordine
   000 preflight → 001 expand → 002 backfill → 003 validate → 004 delete contract
   → 005 validate. Attesi 33/64/99/141, zero violazioni e definizioni precedenti
   preservate. Nessun retry EXPAND, restore automatico o azzeramento generation.
   DDL a commit implicito: errore implica arresto e diagnosi dello stato parziale.
6. **Test soltanto a DB pronto:** configurare TEST_MYSQL_CONNECTION nel processo
   figlio usando il profilo verificato, strict session e client dump MySQL
   effettivamente disponibile; nessuna password CLI e nessuna modifica GLOBAL.
   Build, mirati persistence e suite completa: identità esattamente uguali alle
   738 certificate, 51 unit + 687 integration, zero Failed/Skipped/NotExecuted,
   teardown completato e integrità finale. Non usare la fixture come installatore
   implicito dello schema. JWT resta fermo al checkpoint precedente.

Comando successivo di controllo locale degli SQL, dopo il trasferimento:

```powershell
# Dalla radice del nuovo checkout; solo hash, nessuna esecuzione SQL.
$manifest = Get-Content tests/Integration/TestResults/mt5b-persistence/final-migration-manifest.json -Raw | ConvertFrom-Json
foreach ($entry in $manifest | Where-Object {$_.File -like '*.sql'}) {
    $file = Join-Path migrations/mt5 $entry.File
    if (!(Test-Path -LiteralPath $file)) { throw "Manca $($entry.File)" }
    if ((Get-FileHash -LiteralPath $file).Hash -ne $entry.SHA256) { throw "Hash errato: $($entry.File)" }
}
```

Nessun comando mutante viene presentato come già pronto per l'esecuzione: il
percorso del dump recuperato, la sua revisione e le guardie SQL mancano ancora.
L'ordine sopra è concreto; i comandi di import/apply saranno fissati su quegli
artefatti, non su un sostituto ipotetico.

## Esito e blocchi aperti

**MAMP CONNECTION VERIFIED — DATABASE SETUP BLOCKED BY MISSING ARTIFACTS.**

Server nuovo funzionante e della versione attesa; database test assente.
Servono dal precedente PC i sei SQL originali e il dump MAMP verificato, poi
revisione delle guardie e autorizzazione del piano SQL definitivo. Nessuna
necessità attuale di cambiare porta, riparare XAMPP o riscrivere la persistence.
Questa attività aggiunge soltanto il presente report ai tre report locali già
non tracciati. Nessun codice applicativo o configurazione modificati.
