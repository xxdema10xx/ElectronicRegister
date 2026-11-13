# MT-5A — Piano di ricostruzione isolata con MAMP

**PIANO NON ESEGUITO. MT-5A resta PARZIALE.** La presente esecuzione ha effettuato soltanto
letture, analisi statica dei dump, interrogazioni SELECT/SHOW, help/version dei binari e
scrittura di questo piano/evidenze. Nessun nuovo datadir inizializzato, processo server
avviato, restore, DDL, DML, build o test. Occorre autorizzazione esplicita prima della sezione
operativa. Codice applicativo e migrazioni canoniche invariati, nessun commit/push/MT5B.

## Ambiente identificato e fattibilita

Branch refactor-database-entities; HEAD a002cb66cebecfdcdf892c099cf0518b796b7e1a; modifiche
MT5A precedenti preservate. Documenti MT5A/recovery/MT4A/MT4C/release gate consultati insieme
ai sei SQL canonici e ai runner in migrations/mt4.

MAMP attivo: MySQL5.7.24, C:\MAMP\bin\mysql\bin\mysqld.exe, PID8948, porta3306,
hostname DESKTOP-QUOAG32, datadir C:\MAMP\db\mysql. Database electronic_register_test:
28tabelle/40trigger, holding MT3A_LEGACY_TEST Provisioning,15User,0membership/ruoli.
XAMPP non e il server attivo e non viene utilizzato nel piano.

Proposta: stesso binario MAMP, istanza separata **127.0.0.1:13306**, datadir
`C:\Users\Lorenzo\ElectronicRegister\migrations\mt4\private\mt5a-mamp-13306\data`,
config/log/tmp/import/backup nello stesso nuovo parent. Il percorso e gia coperto
dall'ignore private/; dati/password/log non devono entrare in Git. Nessun servizio Windows.
Al controllo: porta13306 senza listener, parent non esistente, circa495GB liberi su C:.
Richiedere nuovamente almeno2GiB liberi e porta libera prima di iniziare; non e una stima
produttiva e lo spazio va monitorato durante dump/test.

L'help del binario locale conferma --initialize, basedir/datadir/port/bind-address/log-error,
pid-file e disattivazione named-pipe/shared-memory. La seconda istanza e supportata dal
modello MySQL5.7, ma non e stata inizializzata/provata qui. L'help iniziale ha segnalato un
tentativo fallito di accesso EventLog; con **log-syslog=0** il problema EventLog scompare.
--no-defaults senza datadir esplicito segnala inoltre una directory predefinita inesistente:
non usare quel default. Un exit0 di --help non dimostra un avvio riuscito.
[Help senza syslog](TestResults/mt5a-mamp-plan/mysqld-help-no-syslog.txt),
[ambiente](TestResults/mt5a-mamp-plan/environment.json).

Riferimento primario: [inizializzazione MySQL5.7](https://dev.mysql.com/doc/mysql-installation-excerpt/5.7/en/data-directory-initialization.html).
Le opzioni effettivamente disponibili sono state controllate anche sul binario installato.

## Backup verificati senza importazione

| Dump privato | Byte | Tabelle | Trigger | FK | SHA256 |
| --- | ---: | ---: | ---: | ---: | --- |
| 20261019-141428-3269.sql | 135053 | 28 | 40 | 85 | 3F56B13CEBF28735EE177A90FA0D74D6F2972D6F24C2B87B23E71713AF90E756 |
| 20261019-165115-0036.sql | 150879 | 31 | 48 | 92 | 80797A2C793366040D9B231544FA1AC7CD0770E5706F7AB6EDB806E88BBC022A |

Il secondo hash coincide col backup pre-espansione del report MT4C. Entrambi hanno tutte le
definizioni CREATE TABLE e il marcatore finale Dump completed, senza DROP TABLE, CREATE DATABASE,
USE o SET GLOBAL. Il secondo contiene le tre tabelle MT4A, Class.course_edition_id, sette FK
MT4A e otto trigger aggiuntivi; non contiene ledger, revision o trigger MT4C.
Sono presenti tabelle/vincoli owner MT3A, indici tenant e FK composite dei profili; le48 FK
con prefisso fk_mt3a_ includono l'intero inventario nominativo, non il solo sottoinsieme27
FK composite di ownership citato in precedenti report. Non confondere questi conteggi.

Integrita **statica** verificata: hash, struttura ed epilogo, non restore gia riuscito o
integrita referenziale di ogni riga. Quest'ultima richiede il successivo ripristino isolato
e validatori completi. [DDL/FK](TestResults/mt5a-mamp-plan/backup-ddl-verification.json),
[trigger e opzioni](TestResults/mt5a-mamp-plan/backup-structure.json).

I dump contengono INSERT nelle tabelle user/student/teacher e nel grafo scolastico/esami/audit,
inclusi password_hash e dati potenzialmente personali. Non sono dati anonimi certificati.
Nessuna riga, email, hash password o payload e stato stampato. Custodire originali e copie
con ACL riservate, non allegare dump/log privati ai TRX/report pubblici.

### Compatibilita MySQL5.7.24

Analisi statica favorevole, esecuzione ancora da provare:

- ENGINE InnoDB, collations ascii_general_ci/ascii_bin/utf8mb4_general_ci/utf8mb4_unicode_ci;
  non rilevate Aria, system versioning, collations UCA/0900 o direttive sandbox MariaDB.
- Commenti eseguibili con gate40000/40014/40101/40103/40111/50003/50017, interpretati da5.7;
  nessuna direttiva /*M!. Modi SQL osservati supportati da5.7, inclusi NO_AUTO_VALUE_ON_ZERO
  e la modalita strict dei trigger. Nessuna riscrittura automatica per eliminare errori.
- Canonici MT4: DATETIME(6), SIGNAL, JSON_VALID, REGEXP, chiavi composite e piu trigger
  per evento sono previsti da questa versione. Nessuna dipendenza da CHECK effettivamente
  applicati: i vincoli importanti sono FK e SIGNAL. Non vengono introdotti dati retroattivi.
- Dump con DEFINER e LOCK TABLES: sono stati identificati **root@localhost ed er_test@localhost**.
  Import con root della sola istanza isolata; predisporre er_test@localhost con login bloccato
  e privilegi confinati al database test, altrimenti i trigger possono fallire per definer
  inesistente. Non riscrivere o eliminare i definer del dump.
- Il dump contiene FOREIGN_KEY_CHECKS=0 e UNIQUE_CHECKS=0 **di sessione**, con ripristino
  finale. E parte esplicita della futura autorizzazione al restore su schema vuoto; non e
  autorizzato qui. Nessun SET GLOBAL, disattivazione trigger o deroga durante i test.
  La riabilitazione FK non verifica retroattivamente le righe: validazione owner/FK obbligatoria.

## Strategie confrontate e scelta

| Aspetto | A: dump31tabelle + MT4C | B: copia MAMP28tabelle + MT4A + MT4C |
| --- | --- | --- |
| Sorgente | artefatto storico immutabile con hash noto | nuovo export consistente dell'attuale test MAMP |
| Migrazioni | 003 validate preliminare,004/005/006; nessun001 gia applicato | 001/002/003 poi004/005/006 |
| Dati | conserva snapshot storico, non include modifiche successive del MAMP | conserva lo stato locale attuale, non equivale allo snapshot MT4A |
| Compatibilita | dump prodotto su MariaDB: verifica statica positiva, import da provare | export5.7->5.7 riduce differenze di dump, ma richiede entrambe le espansioni |
| Trigger/FK |48trigger/92FK da verificare dopo import; poi53trigger/98FK |40trigger iniziali e inventario locale da verificare; entrambe le espansioni da controllare |
| Riproducibilita | sorgente gia disponibile e hash documentato; meno DDL | nuova sorgente da congelare/hashare e piu passaggi DDL |

**Scelta A**, per minor numero di DDL e sorgente riproducibile. Non modifica lo stato MAMP
attuale e non fabbrica date/eventi. Conservare comunque un backup del solo DB test originale
prima di iniziare. B rimane alternativa da autorizzare separatamente se A fallisce per
incompatibilita autentica: fermare e conservare la destinazione parziale, non sovrascriverla,
non correggere dump/migrazioni alla cieca e non cambiare automaticamente strategia.

Se i binari MAMP non inizializzano/avviano la seconda istanza, nessuna ricaduta sulla3306:
documentare errore/log privati, poi proporre separatamente un pacchetto MySQL5.7.24 in
directory distinta o un container5.7.24 se disponibile. Richiedono verifica compatibilita,
risorse e autorizzazione proprie; nessuna installazione/servizio automatici.

## Procedura operativa esatta proposta — solo dopo autorizzazione

Eseguire dalla root workspace in PowerShell, una fase alla volta. Gli snippet seguenti
**non sono stati eseguiti**. Non concatenarli in un retry automatico. Ogni errore interrompe
la fase e lascia log/datadir per diagnosi; nessuna pulizia ricorsiva o DOWN.

### 1. Destinazione nuova, ACL, backup prima dell'inizializzazione

```powershell
$ErrorActionPreference='Stop'
$repo='C:/Users/Lorenzo/ElectronicRegister'
Set-Location -LiteralPath $repo
$bin='C:/MAMP/bin/mysql/bin'
$inst="$repo/migrations/mt4/private/mt5a-mamp-13306"
$dump="$repo/migrations/mt4/private/20261019-165115-0036.sql"
$expected='80797A2C793366040D9B231544FA1AC7CD0770E5706F7AB6EDB806E88BBC022A'
if((Get-FileHash -LiteralPath $dump).Hash -ne $expected){throw 'Dump changed'}
if(Test-Path -LiteralPath $inst){throw 'Destination already exists: inspect, never overwrite'}
if(netstat -ano -p tcp | Select-String ':13306\s.*LISTENING'){throw 'Port occupied'}
if(([IO.DriveInfo]::new('C:/')).AvailableFreeSpace -lt 2GB){throw 'Insufficient free space'}
New-Item -ItemType Directory -Path $inst | Out-Null
$sid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
& icacls.exe $inst /inheritance:r /grant:r "*${sid}:(OI)(CI)F" '*S-1-5-18:(OI)(CI)F'
if($LASTEXITCODE){throw 'Private ACL failed'}
foreach($d in @('data','tmp','import','backups','logs')){
    New-Item -ItemType Directory -Path "$inst/$d" | Out-Null
}
Copy-Item -LiteralPath $dump -Destination "$inst/backups/verified-mt4a.sql"
if((Get-FileHash "$inst/backups/verified-mt4a.sql").Hash -ne $expected){throw 'Copy hash mismatch'}
# Snapshot aggiuntivo read-only del solo database test MAMP corrente.
$original=[Data.Common.DbConnectionStringBuilder]::new()
$original.set_ConnectionString($env:TEST_MYSQL_CONNECTION)
if($original['Server'] -ne '127.0.0.1' -or [int]$original['Port'] -ne 3306 -or
   $original['Database'] -cne 'electronic_register_test'){throw 'Unexpected original target'}
$oldPwd=$env:MYSQL_PWD
try {
    $env:MYSQL_PWD=$original['Password']
    & "$bin/mysqldump.exe" --no-defaults --protocol=TCP --host=127.0.0.1 --port=3306 `
      "--user=$($original['User'])" --single-transaction --skip-lock-tables --routines --triggers `
      --skip-add-drop-table --skip-dump-date --order-by-primary --hex-blob `
      "--result-file=$inst/backups/original-mamp-before.sql" electronic_register_test
    if($LASTEXITCODE){throw 'Original backup failed'}
} finally {$env:MYSQL_PWD=$oldPwd}
$sourceBackup=[IO.File]::ReadAllText("$inst/backups/original-mamp-before.sql")
if(-not $sourceBackup.Contains('Dump completed') -or
   ([regex]::Matches($sourceBackup,'(?m)^CREATE TABLE `')).Count -ne 28){throw 'Incomplete original backup'}
Get-FileHash "$inst/backups/original-mamp-before.sql"
```

Prima del dump originale ripetere SELECT identita/metadata con lo script read-only recovery:
deve corrispondere a hostname/version/port/datadir3306 sopra, non basta il nome DB.
L'account della connessione originale resta soltanto sorgente del backup; non viene riusato
per il server nuovo. Verificare tutte28 definizioni e40 trigger del backup originale e footer,
poi hash e spazio nuovamente. Nessun accesso SQL ad altri database applicativi.

### 2. Configurazione e inizializzazione isolate

```powershell
@"
[mysqld]
basedir=C:/MAMP/bin/mysql
datadir=$inst/data
port=13306
bind-address=127.0.0.1
pid-file=$inst/mysqld.pid
log-error=$inst/logs/mysql.err
log-syslog=0
tmpdir=$inst/tmp
secure-file-priv=$inst/import
local-infile=0
named-pipe=0
shared-memory=0
server-id=13306
innodb-buffer-pool-size=128M
character-set-server=utf8mb4
collation-server=utf8mb4_unicode_ci
[client]
protocol=TCP
host=127.0.0.1
port=13306
"@ | Set-Content -LiteralPath "$inst/my.ini" -Encoding ASCII
if(@(Get-ChildItem -LiteralPath "$inst/data" -Force).Count){throw 'Datadir not empty'}
& "$bin/mysqld.exe" "--defaults-file=$inst/my.ini" --initialize
if($LASTEXITCODE){throw 'Initialization failed: inspect private error log, do not retry'}
# Root isolato con nuova password casuale, mai stampata o passata in argv.
$rootPassword=[Guid]::NewGuid().ToString('N')+[Guid]::NewGuid().ToString('N')
"ALTER USER 'root'@'localhost' IDENTIFIED BY '$rootPassword';" |
  Set-Content -LiteralPath "$inst/bootstrap.sql" -Encoding ASCII
@"
[client]
protocol=TCP
host=127.0.0.1
port=13306
user=root
password=$rootPassword
"@ | Set-Content -LiteralPath "$inst/client.ini" -Encoding ASCII
$server=Start-Process -FilePath "$bin/mysqld.exe" -WindowStyle Hidden -PassThru `
  -ArgumentList @("--defaults-file=$inst/my.ini","--init-file=$inst/bootstrap.sql")
```

--defaults-file deve essere il primo argomento server. L'inizializzazione scrive soltanto
nel datadir nuovo e genera una password temporanea nel log privato; il bootstrap protetto
la sostituisce per root@localhost prima dell'uso. Non importare mysql/user di altri server,
non creare servizi. Attendere massimo30s con mysqladmin ping verso13306/client.ini e
verificare il processo avviato, listener127.0.0.1:13306 e PID coerente; se non pronto, fermarsi
e ispezionare i log, senza tentare3306 o elevazioni automatiche. Verificare identita SQL subito
dopo, poi rimuovere solo il file bootstrap.sql con Remove-Item -LiteralPath (nessuna ricorsione).
Non rilanciare l'istanza con init-file dopo il bootstrap.

### 3. Guard obbligatorio e import vuoto

```powershell
$mysql="$bin/mysql.exe"
$argsIso=@("--defaults-file=$inst/client.ini",'--batch','--raw','--skip-column-names')
function Assert-Isolated {
  $line=& $mysql @argsIso '--execute=SELECT @@port,@@datadir,VERSION(),@@hostname;'
  if($LASTEXITCODE){throw 'Identity query failed'}
  $v=$line -split "`t"
  if($v.Count -ne 4 -or $v[0] -ne '13306' -or
     $v[1].Replace('\','/').TrimEnd('/') -ine "$inst/data" -or
     $v[2] -ne '5.7.24' -or $v[3] -ne 'DESKTOP-QUOAG32'){throw 'Wrong destination'}
}
Assert-Isolated
$n=& $mysql @argsIso "--execute=SELECT COUNT(*) FROM information_schema.schemata WHERE schema_name='electronic_register_test';"
if($LASTEXITCODE -or $n -ne '0'){throw 'Destination database already exists'}
& $mysql @argsIso '--execute=CREATE DATABASE electronic_register_test CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;'
if($LASTEXITCODE){throw 'Create database failed'}
$argsDb=$argsIso+@('--database=electronic_register_test')
Assert-Isolated
$n=& $mysql @argsDb '--execute=SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE();'
if($LASTEXITCODE -or $n -ne '0'){throw 'Target not empty'}
& "$bin/mysqldump.exe" "--defaults-file=$inst/client.ini" --single-transaction --routines --triggers `
  --skip-add-drop-table "--result-file=$inst/backups/empty-before-restore.sql" electronic_register_test
if($LASTEXITCODE){throw 'Empty target backup failed'}
```

Verificare footer del backup vuoto/hash e zero tabelle: non puo avere definizioni di tabelle.
Per import e SQL canonici usare stdin binario UTF8, evitando pipeline PowerShell che ricodificano
testi/dati; senza --force. Helper da definire nella stessa sessione:

```powershell
function Invoke-IsolatedFile([string]$path,[string]$name) {
  Assert-Isolated
  $si=[Diagnostics.ProcessStartInfo]::new($mysql)
  $si.UseShellExecute=$false; $si.CreateNoWindow=$true
  $si.RedirectStandardInput=$true; $si.RedirectStandardOutput=$true; $si.RedirectStandardError=$true
  # Percorsi proposti senza spazi; nessuna password negli argomenti.
  $si.Arguments="--defaults-file=$inst/client.ini --database=electronic_register_test --batch --skip-column-names --binary-mode --default-character-set=utf8mb4"
  $p=[Diagnostics.Process]::Start($si)
  $out=$p.StandardOutput.ReadToEndAsync(); $err=$p.StandardError.ReadToEndAsync()
  $prefix=[Text.Encoding]::UTF8.GetBytes("SET SESSION sql_mode=CONCAT_WS(',',@@SESSION.sql_mode,'STRICT_TRANS_TABLES');`n")
  $p.StandardInput.BaseStream.Write($prefix,0,$prefix.Length)
  $bytes=[IO.File]::ReadAllBytes($path)
  $p.StandardInput.BaseStream.Write($bytes,0,$bytes.Length); $p.StandardInput.Close(); $p.WaitForExit()
  [IO.File]::WriteAllText("$inst/logs/$name.out",$out.GetAwaiter().GetResult())
  [IO.File]::WriteAllText("$inst/logs/$name.err",$err.GetAwaiter().GetResult())
  if($p.ExitCode){throw "SQL stage $name failed: inspect private logs; no retry"}
}
# Predisporre il definer storico senza abilitarne il login, solo nella nuova istanza.
$definerPassword=[Guid]::NewGuid().ToString('N')+[Guid]::NewGuid().ToString('N')
@"
CREATE USER 'er_test'@'localhost' IDENTIFIED BY '$definerPassword' ACCOUNT LOCK;
GRANT SELECT, INSERT, UPDATE, DELETE, TRIGGER ON electronic_register_test.* TO 'er_test'@'localhost';
"@ | Set-Content -LiteralPath "$inst/definer.sql" -Encoding ASCII
Invoke-IsolatedFile "$inst/definer.sql" 'definer-provision'
Invoke-IsolatedFile "$inst/backups/verified-mt4a.sql" 'restore-mt4a'
```

L'import esegue le impostazioni FK/unique di sessione presenti nel dump solo sullo schema
vuoto isolato. Non fare SELECT dei dati personali nei log pubblici. Dopo import:31tabelle,
48trigger,92FK, ledger/revision assenti, sette FK MT4A RESTRICT, indici/colonne/owner e definer
corrispondenti alle definizioni estratte, holding Provisioning. Verificare counts/digest di
tutte le tabelle rispetto al dump e assenza di orfani anche per le FK legacy/MT3A: il solo
count e validate003 non bastano. Controllare FOREIGN_KEY_CHECKS e UNIQUE_CHECKS=1 su nuova
connessione. Qualsiasi differenza ferma il piano prima del DDL004.

### 4. Validazione MT4A, backup e sola espansione MT4C

```powershell
Invoke-IsolatedFile "$repo/migrations/mt4/003_validate.sql" 'validate-mt4a'
# Leggere validate-mt4a.out: esattamente7 righe di controllo, ogni violations=0.
Assert-Isolated
& "$bin/mysqldump.exe" "--defaults-file=$inst/client.ini" --single-transaction --routines --triggers `
  --skip-add-drop-table "--result-file=$inst/backups/before-mt4c.sql" electronic_register_test
if($LASTEXITCODE){throw 'Pre-DDL backup failed'}
Get-FileHash "$inst/backups/before-mt4c.sql"
# Prima di proseguire:31 CREATE TABLE,48 trigger, footer, hash e spazio verificati.
Invoke-IsolatedFile "$repo/migrations/mt4/004_enrollment_audit_expand.sql" 'expand-mt4c'
Invoke-IsolatedFile "$repo/migrations/mt4/005_enrollment_audit_backfill.sql" 'backfill-mt4c'
Invoke-IsolatedFile "$repo/migrations/mt4/006_enrollment_audit_validate.sql" 'validate-mt4c'
```

Prima di004: controllare assenza ledger/revision/indice attore/triggerMT4C e integrita
actor/owner, non solo schema count. Dopo005: invalid_revision=0; dopo006: esattamente5
controlli, tutti0. Verificare32tabelle/53trigger/**98FK**, nomi/ordine colonne delle sei nuove
FK tutte UPDATE/DELETE RESTRICT, sette FK MT4A preservate, PK/indici ledger, indice triplo
membership e Revision BIGINT NOT NULL DEFAULT0, cinque definizioni trigger uguali al canonico.
Ledger vuoto, Revision0 sulle relazioni esistenti e nessun evento/edizione/iscrizione inventati.
Confrontare tutte le colonne/dati preesistenti pre/post004, non solo conteggi.

DDL ha commit impliciti: un fallimento puo lasciare stato parziale. Non rieseguire004,
non fare DROP/restore automatico. Conservare la nuova istanza per diagnosi. I runner
Invoke-Mt4*.ps1 esistenti sono stati letti ma **non vanno usati qui**: puntano a client
XAMPP e hanno guard incompleti per datadir/istanza; questo piano usa SQL canonici invariati
con connessione MAMP esplicita e guard supplementari. Nessuna modifica a quei runner.

## Test dopo ricostruzione autorizzata

Esiste un ostacolo tooling indipendente dal server: il backup helper MT3A dei test passa
--column-statistics=0 quando MYSQLDUMP_FLAVOR non e MariaDB. MAMP mysqldump5.7.24 non
supporta quell'opzione (help controllato). **Non impostare flavor=MariaDB per mascherarlo.**
Client alternativo gia installato e verificato: MySQL mysqldump8.0.44, usabile con i flag
--column-statistics=0/--no-tablespaces/--set-gtid-purged=OFF gia passati dall'helper.
Resta solo un client; server e tutti gli import/migrazioni rimangono MAMP5.7.24.
Compatibilita del backup helper va provata prima della suite, senza cambiare assertion.
[Documentazione mysqldump](https://dev.mysql.com/doc/refman/8.0/en/mysqldump.html).

Configurazione esclusivamente nella sessione/processi test dopo guard positivo:

```powershell
Assert-Isolated
$env:TEST_MYSQL_CONNECTION="Server=127.0.0.1;Port=13306;Database=electronic_register_test;User=root;Password=$rootPassword"
$env:ER_TEST_STRICT_SESSION='1'
$env:MYSQLDUMP_EXE='C:/Program Files/MySQL/MySQL Server 8.0/bin/mysqldump.exe'
$env:MYSQLDUMP_FLAVOR='MySQL'
$env:Logging__EventLog__LogLevel__Default='None'
$evidence="$repo/tests/Integration/TestResults/mt5a-mamp-validation"
dotnet build Backend/ElectronicRegisterAPI/ElectronicRegisterAPI.slnx --no-restore
if($LASTEXITCODE){throw 'Build failed'}
dotnet test Backend/ElectronicRegisterAPI/ElectronicRegisterAPI.slnx --no-build --no-restore --filter 'FullyQualifiedName!~Mt5aMembershipAdminTests' --logger trx --results-directory "$evidence/baseline"
if($LASTEXITCODE){throw 'Baseline failed: classify before proceeding'}
dotnet test tests/Integration/Integration.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~Mt5aMembershipAdminTests' --logger trx --results-directory "$evidence/targeted"
if($LASTEXITCODE){throw 'MT5A targeted failed'}
dotnet test Backend/ElectronicRegisterAPI/ElectronicRegisterAPI.slnx --no-build --no-restore --logger trx --results-directory "$evidence/final-1"
if($LASTEXITCODE){throw 'Final1 failed'}
dotnet test Backend/ElectronicRegisterAPI/ElectronicRegisterAPI.slnx --no-build --no-restore --logger trx --results-directory "$evidence/final-2"
if($LASTEXITCODE){throw 'Final2 failed'}
git diff --check
```

Salvare prima i valori delle variabili ambiente e ripristinarli in finally al termine;
non salvarli o stamparli nei report. Nessuna appsettings operativa modificata. Il root
isolato e necessario anche al teardown tecnico MT4C, non conferisce privilegi HTTP.

Richiesti: baseline51unit+624integration=675, stessi nomi TRX storici; targeted36; entrambe
le finali51unit+660integration=711, tutti outcome Passed e zero Failed/Skipped. Congelare
sorgenti/hash prima delle finali, TRX separati con SHA256, nessuna sostituzione test.
Worker MT3E, audit MT4C, concorrenza/rollback, bootstrap Development/Production, JWT/revoche,
legacy gate, A/B/read-only/DTO e flag sono inclusi e vanno verificati negli esiti individuali.
Classificare ogni errore come ambiente/preflight/regressione applicativa/nuovi test prima
di intervenire. Nessun test indebolito per differenze di motore o sql_mode.

Stato finale: sola holding Provisioning, zero fixture temporanee, tutte FK/trigger/indici
preservati, GLOBAL sql_mode originale di ciascuna istanza invariato. Le suite legacy
modificano intenzionalmente dati Esami **solo nella copia isolata**: non promettere digest
di ogni cella isolata invariato dopo i test. Per3306 ripetere snapshot SQL e backup originale
con --skip-dump-date/ordine deterministico, confrontando hash/contenuti senza stamparli;
eventuali scritture concorrenti esterne vanno indagate, mai annullate automaticamente.
Spegnimento futuro soltanto con mysqladmin client.ini/porta13306 e dopo verifica PID/datadir,
mai taskkill generico o stop del MAMP3306. Non cancellare datadir/log/backup automaticamente.

## Limiti, autorizzazione e verdetto

Restano da provare inizializzazione, avvio, restore, tutte le verifiche dati/FK, DDL e suite:
il piano non certifica in anticipo la compatibilita runtime. Nessun accesso CES01622 necessario
per la strategia A. Un risultato711/711 qui sara certificazione **MySQL5.7.24 MAMP**,
non MariaDB10.4. Aggiornare report/release gate a quella distinzione solo dopo esiti reali.

L'autorizzazione richiesta comprende: creazione del solo parent isolato sopra, ACL/private
backup del solo DBtest3306, inizializzazione delle tabelle di sistema del nuovo datadir,
avvio manuale loopback13306 e bootstrap delle sue sole credenziali (incluso er_test definer
con account bloccato e privilegi sul solo test), creazione DBtest vuoto,
restore del dump verificato incluse le sue temporanee impostazioni FK/unique di sessione,
DDL canonico004, validatori e test con fixture/teardown sulla copia. Non comprende modifiche
al server3306, GLOBAL sql_mode, servizi Windows, migrazioni speculative o MT5B.

**Punto di arresto attuale: prima di tutte queste operazioni.** Attendere autorizzazione
esplicita dell'utente. MT-5A resta PARZIALE finche due suite complete711/711 non sono verdi.
