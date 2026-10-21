# XAMPP MariaDB 10.4.32 — piano di recupero Aria

Data: 21 ottobre 2026, verifiche circa 09:48–09:51 Europe/Rome.
Documento operativo da revisionare prima di ogni fase. **Tutti i comandi di
manutenzione riportati sotto sono preparati, NON eseguiti.** Eseguire i blocchi
separatamente, controllando l'esito; non incollarli come unica sequenza automatica.

## 1. Diagnosi e cronologia

Letto `MULTITENANCY_MT5B_XAMPP_CONNECTION_DIAGNOSIS.md`. Riletti log XAMPP,
configurazioni e registro Application Windows (ultimi 12.000 eventi complessivi,
poi filtro MariaDB e lettura EventData XML; non si presume copertura storica totale).

| Data/ora locale | Evidenza | Interpretazione |
| --- | --- | --- |
| 11 giugno 14:14:33 | Eventi 7271–7279: mysql.db crashed, table from another system, Wrong CRC on datapage at 1, Rows lost (Found 0 of 3), safe repair abortita | Corruzione storica db dimostrata; quel tentativo non è riuscito |
| 11 giugno 14:17–14:18 | Fatal error privilege tables su mysql.db, più PID | Precedenti fallimenti di avvio |
| 17 giugno 09:35:56–57 | Zerofilling moved table mysql.db e mysql.global_priv | Operazioni storiche registrate; non provano da sole corruzione attuale |
| 19 ottobre 09:41:13 | Evento 13764, PID 24500: Found 0 of 3 rows when repairing mysql.db | Grave evidenza di possibile perdita di tre righe di privilegi; nessuna certificazione di conservazione integrale |
| 21 ottobre 09:36:12 | PID 6524: Aria recovery done, InnoDB recovery e avvio, socket creato | Motori inizializzati prima del caricamento privilegi; non equivale a server pronto |
| 21 ottobre 09:36:12 | Eventi 14494–14497: proxies_priv crashed, recovering, Couldn't repair, indice corrotto, Can't open and lock privilege tables | Fallimento attuale dimostrato su tabella privilegi |
| 21 ottobre 09:36:12–14 | Eventi 14498–14499: Aborting, Aria checkpoint failed | Aborto avvio; possibile problema secondario durante chiusura/recovery |

Il file `mysql_error.log` termina al socket del 21 ottobre e non contiene gli
errori fatali successivi: il registro Windows è indispensabile. Nessun errore
fatale InnoDB nel segmento corrente esaminato. Il precedente errore 2013 è coerente
con il mancato completamento dell'avvio; non è evidenza di password errata.

Le anomalie db/global_priv/proxies_priv appartengono allo stesso insieme di tabelle
di sistema Aria e richiedono analisi congiunta. **Non è accertata la causa radice**:
non si attribuisce la corruzione attuale alla precedente riparazione, a un crash,
al disco o a un trasferimento di file senza ulteriori prove. Non si dichiara
corrotto l'intero datadir. I warning sulle dimensioni di global_priv riferiti
dall'utente non sono stati ritrovati nelle fonti esaminate; restano da verificare
offline. La lunghezza del file da sola non ne prova la corruzione.

## 2. Ambiente verificato e limiti

- Processo corrente: PID **6524**, percorso `C:\xampp\mysql\bin\mysqld.exe`,
  StartTime 21 ottobre 09:36:12, Responding=True, CPU circa 0,203125 s.
  `mysql.pid` contiene 6524. Non considerare Responding o PID prova di operatività.
- TCP `0.0.0.0:3306 LISTENING` appartiene al PID 6524; nessuna 13306 osservata.
  Nessun altro mysqld/mariadbd rilevato. Non riprovata autenticazione in questa fase.
- Binario server: `mysqld.exe --no-defaults --version` conferma 10.4.32-MariaDB.
  `aria_chk.exe --no-defaults --version`: versione utility 1.2, distribuzione XAMPP.
  Help locale di aria_chk e mysqladmin letto; nessuna tabella aperta con aria_chk.
- `C:\xampp\mysql\bin\my.ini`: datadir C:/xampp/mysql/data, porta 3306,
  log_error mysql_error.log, pid_file mysql.pid; percorsi InnoDB nel datadir,
  buffer pool 16M, redo 5M, sql_mode NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION.
  `C:\xampp\mysql\data\my.ini` esiste e contiene lo stesso datadir.
- `mysql_start.bat` specifica il file bin/my.ini. La command line del processo
  non è disponibile nelle verifiche precedenti per accesso CIM negato: file
  effettivamente utilizzato fortemente corroborato, non provato integralmente.
- `mysql_stop.bat` richiama killprocess.bat con un placeholder di installazione:
  **NON usarlo**. Non è un percorso di shutdown regolare verificato. Non si assume
  neppure che il pulsante Stop del pannello XAMPP eviti una terminazione forzata.
- Strumenti handle/handle64 non disponibili. `openfiles /query` segnala che il
  tracciamento locale non è abilitato: nessuna certificazione completa dei lock.
  Non abilitato il flag e non richiesti privilegi aggiuntivi. Prima del backup
  occorrerà verificare gli handle con Monitoraggio risorse, senza chiuderli.

## 3. Inventario e destinazione

Datadir: **315 file, 103.071.851 byte (circa 98,3 MiB)** al momento del rilievo.
È una misura live, da ricalcolare dopo l'arresto; non certifica contenuti.
Nessun reparse point rilevato nell'albero. Include tutti i database, mysql,
ibdata1 (79.691.776 byte), ib_logfile0/1 (5.242.880 byte ciascuno), aria_log_control,
aria_log.00000001, .frm/.MAI/.MAD, my.ini locale, log e dump diagnostico.

File correnti di interesse (byte):

| Tabella | frm | MAD | MAI |
| --- | ---: | ---: | ---: |
| db | 2.713 | 16.384 | 24.576 |
| global_priv | 1.451 | 16.697 | 23.611 |
| proxies_priv | 2.837 | 16.384 | 24.576 |

Preservare tutte le varianti .old/.BAK senza usarle automaticamente: provenienza,
coerenza temporale e validità non accertate. Il backup di oggi non ricrea le righe
eventualmente già perse il 19 ottobre.

Unico volume rilevato: **C:**, 511.043.432.448 byte totali,
302.531.477.504 byte disponibili (circa 281,8 GiB). Get-PSDrive non esponeva Free;
misura ottenuta con System.IO.DriveInfo. Nessun disco D: presunto.

Destinazione proposta: `Join-Path $env:USERPROFILE 'MariaDB-Recovery-20261021'`.
Il profilo locale esiste su C:, il percorso proposto non esiste; è fuori dal
datadir e dal repository. Non è stato creato. Crearlo soltanto con autorizzazione,
con ACL private verificate **prima** della copia. La ACL del workspace concede
accesso anche a gruppi generici: non ereditarla come garanzia di riservatezza.
La destinazione sullo stesso disco protegge dalle modifiche di recovery, non dal
guasto del disco: seconda copia su supporto separato consigliata prima del repair
originale; non è disponibile un secondo supporto verificato oggi.

Budget minimo prudenziale: 3 GiB liberi per backup, copie indipendenti, manifest,
log e temporanei; ricalcolare se crescono i dati. Non è una garanzia del massimo
temporaneo richiesto da aria_chk. Verificare spazio anche dopo ciascuna copia.

## 4. Arresto controllato — autorizzazione A necessaria

Coordinare la finestra XAMPP con tutti gli utilizzatori. Inibire riavvii automatici
e chiudere i client con procedure concordate, senza modifiche applicative.
Non invocare mysql_stop.bat, taskkill, Stop-Process o kill dal pannello.

Comandi esatti proposti per preflight e shutdown tramite il client XAMPP:

```powershell
$ErrorActionPreference = 'Stop'
$server = Get-Process -Id 6524 -ErrorAction Stop
if ($server.Path -ine 'C:\xampp\mysql\bin\mysqld.exe' -or
    $server.StartTime.ToString('yyyy-MM-ddTHH:mm:ss') -ne '2026-10-21T09:36:12') {
    throw 'Identita cambiata: rifare il preflight, non arrestare'
}
if ((Get-Content -LiteralPath C:\xampp\mysql\data\mysql.pid).Trim() -ne '6524') {
    throw 'PID file non coincide'
}
netstat -ano -p tcp | Select-String ':3306\s'
# Revisione manuale obbligatoria: listener ancora del PID verificato.
# Account autorizzato, password solo nel prompt; nessun segreto sulla CLI.
& C:\xampp\mysql\bin\mysqladmin.exe --no-defaults --protocol=TCP --host=127.0.0.1 --port=3306 --user=root --password --connect-timeout=5 --shutdown-timeout=30 shutdown
if ($LASTEXITCODE -ne 0) { throw 'Shutdown regolare fallito: STOP, nessuna copia cold' }
Wait-Process -Id 6524 -Timeout 30 -ErrorAction Stop
if (Get-Process -Name mysqld,mariadbd -ErrorAction SilentlyContinue) {
    throw 'Processo SQL presente: STOP'
}
netstat -ano -p tcp | Select-String ':3306\s'
# STOP se esiste un LISTENING sulla 3306, anche con altro PID.
```

L'account root è quello già impiegato nel preflight, non vengono ipotizzate nuove
credenziali. Non serve selezionare un database per il comando amministrativo.
Se non dispone di SHUTDOWN o il greeting è assente, il comando fallirà: è probabile
nel presente stato. **Questo è un punto di arresto reale**, non una richiesta di
passare automaticamente a kill. Se il processo esce prima di Wait-Process,
confermare esplicitamente l'assenza anziché trattare l'errore come autorizzazione
a proseguire. Nessun fallback ad altre istanze.

Per ispezione locale avviare, solo nella fase autorizzata, `resmon.exe` e cercare
in CPU → Handle associati il percorso completo C:\xampp\mysql\data, quindi
aria_log_control e i tre nomi delle tabelle. Non usare Close Handle. Assenza di
mysqld più controllo degli altri writer/handle è prerequisito; se il tool non
consente visibilità sufficiente, documentare il limite e fermarsi. Non basta
riuscire a leggere un file. Eventuali escalation di privilegi o interventi sul
processo bloccato richiedono motivazione e nuova autorizzazione separata.

## 5. Backup completo a freddo — autorizzazione B necessaria

Solo dopo il gate precedente, nuova destinazione vuota e spazio confermato.
I seguenti blocchi sono una procedura proposta, non uno script già eseguito.
Per evitare ACL generiche creare una directory protetta per l'utente corrente,
SYSTEM e amministratori; controllare il risultato prima di inserire dati:

```powershell
$recoveryRoot = Join-Path $env:USERPROFILE 'MariaDB-Recovery-20261021'
if (Test-Path -LiteralPath $recoveryRoot) { throw 'Destinazione gia esistente: STOP' }
if ([IO.DriveInfo]::new('C:\').AvailableFreeSpace -lt 3GB) { throw 'Spazio insufficiente' }
New-Item -ItemType Directory -Path $recoveryRoot -ErrorAction Stop | Out-Null
$ownerSid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
& icacls.exe $recoveryRoot /inheritance:r /grant:r "*$($ownerSid):(OI)(CI)F" '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F'
if ($LASTEXITCODE -ne 0) { throw 'ACL non impostata: STOP' }
Get-Acl -LiteralPath $recoveryRoot | Format-List Owner,AreAccessRulesProtected,AccessToString
# Verifica umana obbligatoria: nessun altro accesso, niente gruppi generici.
$coldData = Join-Path $recoveryRoot 'cold-data'
$evidence = Join-Path $recoveryRoot 'evidence'
New-Item -ItemType Directory -Path $evidence -ErrorAction Stop | Out-Null
```

Questi permessi sono intenzionali solo per la nuova area recovery; non cambiare
ACL del datadir. Se l'ACL finale non coincide, non copiare dati. Il backup include
materiale di autenticazione e dati personali: conservare tutto privatamente,
non allegare contenuti o manifest al report pubblico e non aggiungerli a Git.

```powershell
if (Get-Process -Name mysqld,mariadbd -ErrorAction SilentlyContinue) { throw 'Server presente' }
# Riverificare listener e handle, assenza reparse point, nessun writer.
if (Test-Path -LiteralPath $coldData) { throw 'cold-data deve essere nuova' }
& robocopy.exe C:\xampp\mysql\data $coldData /E /COPY:DAT /DCOPY:DAT /R:0 /W:0 /XJ /LOG:"$evidence\cold-copy.log"
$coldCopyExit = $LASTEXITCODE
if ($coldCopyExit -ge 8) { throw "Backup fallito, robocopy=$coldCopyExit" }
Copy-Item -LiteralPath C:\xampp\mysql\bin\my.ini -Destination "$evidence\bin-my.ini" -ErrorAction Stop
Copy-Item -LiteralPath C:\xampp\mysql\data\mysql_error.log -Destination "$evidence\mysql_error.log" -ErrorAction Stop
Copy-Item -LiteralPath C:\xampp\mysql_start.bat -Destination "$evidence\mysql_start.bat" -ErrorAction Stop
Get-WinEvent -FilterHashtable @{LogName='Application'} -MaxEvents 12000 |
    Where-Object {$_.ProviderName -eq 'MariaDB'} |
    ForEach-Object {$_.ToXml()} | Set-Content -LiteralPath "$evidence\mariadb-events.xml.txt" -Encoding UTF8
```

Robocopy 0–7 non basta: leggere log e spiegare bit extra/mismatch, non accettare
file saltati o copie parziali; >=8 è errore. Niente /MIR, niente esclusioni di
.old/.BAK, niente pulizie. /COPY:DAT evita di ereditare ACL pubbliche dalla sorgente;
la destinazione eredita la ACL privata. Salvare separatamente un inventario ACL
della sorgente se necessario per un futuro restore, senza applicarlo al backup.
Configurazione locale data/my.ini e ogni metadato del datadir sono già inclusi.

### Verifica completa del backup

```powershell
function Get-RecoveryManifest([string]$root) {
    $base = (Get-Item -LiteralPath $root -ErrorAction Stop).FullName.TrimEnd('\')
    @(Get-ChildItem -LiteralPath $base -Recurse -File -Force -ErrorAction Stop |
        ForEach-Object {
            [pscustomobject]@{
                RelativePath = $_.FullName.Substring($base.Length + 1)
                Length = $_.Length
                SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256 -ErrorAction Stop).Hash
            }
        } | Sort-Object RelativePath)
}
$sourceOne = Get-RecoveryManifest 'C:\xampp\mysql\data'
$backupOne = Get-RecoveryManifest $coldData
$sourceTwo = Get-RecoveryManifest 'C:\xampp\mysql\data'
if (Compare-Object $sourceOne $sourceTwo -Property RelativePath,Length,SHA256) {
    throw 'Sorgente cambiata durante il backup: STOP'
}
if (Compare-Object $sourceOne $backupOne -Property RelativePath,Length,SHA256) {
    throw 'Backup non identico: STOP'
}
function Get-RecoveryDirectories([string]$root) {
    $base = (Get-Item -LiteralPath $root -ErrorAction Stop).FullName.TrimEnd('\')
    @(Get-ChildItem -LiteralPath $base -Recurse -Directory -Force -ErrorAction Stop |
        ForEach-Object {$_.FullName.Substring($base.Length + 1)} | Sort-Object)
}
if (Compare-Object @(Get-RecoveryDirectories 'C:\xampp\mysql\data') @(Get-RecoveryDirectories $coldData)) {
    throw 'Inventario directory differente: STOP'
}
$sourceOne | Export-Csv -LiteralPath "$evidence\source-manifest.csv" -NoTypeInformation
$backupOne | Export-Csv -LiteralPath "$evidence\backup-manifest.csv" -NoTypeInformation
$backupOne | Measure-Object Length -Sum
foreach ($required in @('mysql\db.frm','mysql\db.MAI','mysql\db.MAD',
    'mysql\global_priv.frm','mysql\global_priv.MAI','mysql\global_priv.MAD',
    'mysql\proxies_priv.frm','mysql\proxies_priv.MAI','mysql\proxies_priv.MAD',
    'aria_log_control','aria_log.00000001','ibdata1','ib_logfile0','ib_logfile1','my.ini')) {
    if (!(Test-Path -LiteralPath (Join-Path $coldData $required))) { throw "Manca $required" }
}
if ((Get-FileHash C:\xampp\mysql\bin\my.ini).Hash -ne
    (Get-FileHash "$evidence\bin-my.ini").Hash) { throw 'Config backup differente' }
Get-Acl -LiteralPath $coldData | Format-List AreAccessRulesProtected,AccessToString
[IO.DriveInfo]::new('C:\').AvailableFreeSpace
```

Confrontare anche elenco directory, incluse vuote, con percorsi relativi sorgente/
destinazione; verificare conteggi, somma byte e nessun errore di enumerazione.
I nomi di redo/log sopra sono quelli attuali: se l'inventario cambia, aggiornare
la lista obbligatoria senza escludere i nuovi file. Preservare hash anche di
config e log diagnostici. Solo dopo tutti questi controlli marcare il backup
**VERIFIED**, annotando exit code, conteggi e spazio residuo. Non è una prova che
i dati originali siano sani: è una copia verificata dello stato danneggiato.

## 6. Seconda copia e check Aria offline — autorizzazione C necessaria

Il backup cold-data non va mai aperto da aria_chk o da mysqld. Preparare `check-data`
nuova copiando **tutto** cold-data, verificando manifest come sopra. Conservare
insieme controllo e log Aria della stessa istanza. Nessun avvio SQL sulla copia.

```powershell
$checkData = Join-Path $recoveryRoot 'check-data'
if (Test-Path -LiteralPath $checkData) { throw 'Copia di lavoro gia presente' }
& robocopy.exe $coldData $checkData /E /COPY:DAT /DCOPY:DAT /R:0 /W:0 /XJ /LOG:"$evidence\check-copy.log"
if ($LASTEXITCODE -ge 8) { throw 'Copia check fallita' }
if (Compare-Object (Get-RecoveryManifest $coldData) (Get-RecoveryManifest $checkData) -Property RelativePath,Length,SHA256) { throw 'Copia check non identica' }
$checkBefore = Get-RecoveryManifest $checkData
Push-Location -LiteralPath $checkData
try {
    foreach ($table in @('proxies_priv','db','global_priv')) {
        $indexPath = Join-Path $checkData "mysql\$table.MAI"
        & C:\xampp\mysql\bin\aria_chk.exe --no-defaults "--datadir=$checkData" "--logdir=$checkData" --require-control-file --description --verbose --read-only --skip-update-state $indexPath *> "$evidence\$table-description.txt"
        $descriptionExit = $LASTEXITCODE
        & C:\xampp\mysql\bin\aria_chk.exe --no-defaults "--datadir=$checkData" "--logdir=$checkData" --require-control-file --check --information --verbose --read-only --skip-update-state $indexPath *> "$evidence\$table-check.txt"
        $checkExit = $LASTEXITCODE
        [pscustomobject]@{Table=$table;DescriptionExit=$descriptionExit;CheckExit=$checkExit} |
            Export-Csv -LiteralPath "$evidence\$table-exits.csv" -NoTypeInformation
    }
} finally { Pop-Location }
$checkAfter = Get-RecoveryManifest $checkData
Compare-Object $checkBefore $checkAfter -Property RelativePath,Length,SHA256
```

Tutte le opzioni risultano nell'help locale: --no-defaults primo argomento;
--read-only e --skip-update-state evitano la marcatura dello stato delle tabelle,
che altrimenti è abilitata per default. Nessun --force (nel check può avviare
recovery), --update-state, --analyze o --zerofill. Il controllo può aprire file
di controllo/log nella sola copia: confrontare l'intero manifest prima/dopo,
registrare ogni modifica inattesa e fermarsi se coinvolge dati/indici. Nessun
flag --ignore-control-file per aggirare un errore di controllo. I log rimangono
privati: potrebbero contenere informazioni sensibili.

Per ciascuna tabella registrare header record count (potenzialmente inaffidabile),
righe lette/valide, lunghezze attese/effettive MAD/MAI, CRC, pagine/offset corrotti,
indici e chiavi duplicate, errori I/O e stato del control file. Se il check non può
leggere i dati, scrivere **conteggio sconosciuto**, non zero. --description non
dimostra il numero reale di record recuperabili. Nessun conteggio attuale è stato
ottenuto in questa fase di pianificazione.

Ulteriori 21 .MAI di sistema presenti: columns_priv, column_stats, event, func,
help_category, help_keyword, help_relation, help_topic, index_stats, plugin, proc,
procs_priv, roles_mapping, servers, tables_priv, table_stats, time_zone,
time_zone_leap_second, time_zone_name, time_zone_transition,
time_zone_transition_type. Estendere **solo i check** sulla copia se emergono
errori comuni; prima del riavvio finale verificare comunque tutte le Aria di
sistema. Errori InnoDB richiedono un percorso distinto; aria_chk non le controlla.

## 7. Simulazione separata — autorizzazione D necessaria

Creare tre copie complete indipendenti e nuove da cold-data verificato:
`trial-index`, `trial-recover`, `trial-safe`. Ripetere robocopy e confronto manifest
del paragrafo precedente per ognuna. Non concatenare tentativi sulla stessa copia,
non usare check-data come baseline e non modificare il backup.

Esempi precisi per la **sola proxies_priv**, da eseguire uno per volta sulla
rispettiva copia, con cwd e logdir nella copia e log di output in evidence:

```powershell
$trialIndex = Join-Path $recoveryRoot 'trial-index'
$trialRecover = Join-Path $recoveryRoot 'trial-recover'
$trialSafe = Join-Path $recoveryRoot 'trial-safe'
# Solo dopo creazione e verifica di ciascuna copia indipendente:
& C:\xampp\mysql\bin\aria_chk.exe --no-defaults "--datadir=$trialIndex" "--logdir=$trialIndex" --require-control-file --recover --quick --backup "$trialIndex\mysql\proxies_priv.MAI"
# Salvare SUBITO $LASTEXITCODE, output completo privato, manifest e check successivo.
& C:\xampp\mysql\bin\aria_chk.exe --no-defaults "--datadir=$trialRecover" "--logdir=$trialRecover" --require-control-file --recover --backup "$trialRecover\mysql\proxies_priv.MAI"
& C:\xampp\mysql\bin\aria_chk.exe --no-defaults "--datadir=$trialSafe" "--logdir=$trialSafe" --require-control-file --safe-recover --backup "$trialSafe\mysql\proxies_priv.MAI"
```

--recover --quick tenta una riparazione senza modificare il file dati: verificare
SHA256 MAD identico e check completo riuscito. Non duplicare --quick: l'help
avverte che due -q possono forzare modifiche dati in caso di duplicati. Non usare
--create-missing-keys se count/index non sono affidabili. Recovery ordinaria può
modificare MAD e MAI; --safe-recover è una modalità alternativa più lenta, non una
garanzia di assenza di perdita. --backup salva il MAD, non sostituisce il cold
backup completo. Niente --force, --extend-check in modalità repair (può recuperare
spazzatura), cambi collation, checksum corretti alla cieca o parametri per saltare
record. Le tre modalità sono supportate dall'help XAMPP 10.4.32.

Applicare la stessa metodologia a db/global_priv **solo se i check ne dimostrano
necessità**, con comandi e autorizzazione aggiornati per ciascuna tabella.
Produrre matrice prima/dopo: conteggi header e scansione, record persi/scartati,
duplicati, CRC/errori lettura, hash MAD/MAI, exit code e nuovo check read-only.
Se passa da N a M con M<N, segnalare esplicitamente N-M record potenzialmente persi;
non autorizzare il target originale automaticamente. Anche count identici possono
nascondere contenuti diversi: exit code 0 e numero righe invariato non certificano
privilegi identici. Per mysql.db, lo stato attuale potrebbe già riflettere la
perdita storica 3→0: un nuovo check verde non la annulla. Per global_priv la perdita
può rimuovere utenti/plugin/autenticazione; per db grant a database; per
proxies_priv grant di delega. Contenuti da confrontare solo privatamente con
backup affidabili precedenti, senza pubblicare credenziali o JSON Priv.

## 8. Eventuale intervento originale — autorizzazione E specifica

Prerequisiti: backup fisico VERIFIED, copia esterna valutata, nessun processo o
handle scrivente, identità/percorso ricontrollati, confronto offline senza perdita
osservata e senza alterazioni ai record non spiegate, elenco esatto tabelle e
metodo concordato. L'utente deve accettare esplicitamente ogni perdita residua
irrisolvibile; in assenza di tale decisione non intervenire.

**Candidato limitato**, solo se il trial-index dimostra che basta ricostruire
l'indice mantenendo MAD identico e tutti i check validi:

```powershell
# NON eseguire adesso. Solo server fermo, backup valido, gate E approvato.
& C:\xampp\mysql\bin\aria_chk.exe --no-defaults --datadir=C:\xampp\mysql\data --logdir=C:\xampp\mysql\data --require-control-file --recover --quick --backup C:\xampp\mysql\data\mysql\proxies_priv.MAI
# Acquisire exit code; check read-only identico a quello offline sul target;
# verificare hash MAD, righe, file modificati e log prima di autorizzare avvio.
```

Se serve recovery ordinaria/safe, questo comando **non è approvabile per analogia**:
presentare il comando esatto scelto e risultati della relativa prova. Non riparare
tutte le tabelle per comodità. Stop immediato per righe perse, errori I/O, control
file/LSN non coerenti, nuovo processo, hash fonte inattesi o disco insufficiente.

In caso di fallimento lasciare server fermo e preservare anche lo stato post-test;
non rilanciare automaticamente né mescolare MAI nuovi e MAD vecchi. Il backup
iniziale conserva la corruzione iniziale e non garantisce un server avviabile.
Un eventuale ritorno allo snapshot completo o recupero di righe da backup storico
richiede **autorizzazione restore separata** e nuova analisi della consistenza
Aria/InnoDB. Non cancellare aria_log*, ibdata1 o redo per forzare avvio; non
sostituire mysql con quella di un'altra installazione XAMPP e non resettare grant.

## 9. Avvio e validazione — autorizzazione F necessaria

Riavvio unico controllato dal pannello XAMPP solo dopo il gate E e check validi;
verificare prima assenza processi/listener residui. Non usare una nuova istanza
SQL o avviare le copie. Nessun loop di restart se compaiono nuovi errori.

Con credenziali esistenti, in sessione privata, acquisire handshake e:

```sql
SELECT VERSION(),@@version_comment,@@hostname,@@port,@@datadir,
       DATABASE(),CURRENT_USER(),@@GLOBAL.sql_mode;
```

Per ElectronicRegister selezionare esplicitamente electronic_register_test.
Confrontare identità con percorso atteso e nuova finestra di log/eventi: nessun
fatal, crash recovery fallita, privilege error o checkpoint failure nuovo.
L'avvio riuscito non certifica conservazione utenti/privilegi.

I check fisici offline comprendono tutte le Aria mysql. Dopo il riavvio, revisione
privata read-only di utenti/plugin, grant diretti/globali/database/tabella/colonna,
routine, ruoli e proxy rispetto a fonte storica affidabile; usare SHOW GRANTS per
gli account identificati senza pubblicarne output, testare accessi autorizzati
esistenti senza cambiare password. Non interrogare/pubblicare colonne di password
o JSON Priv nel report. Se mancano i riferimenti storici, dichiarare preservazione
dei privilegi **non dimostrata**, soprattutto mysql.db.

Inventariare engine, tabelle, trigger, FK e indici dei database esistenti con
information_schema; per ElectronicRegister solo electronic_register_test e sole
letture. Verifica fisica InnoDB e log, più controlli applicativi di lettura e
integrità referenziale pianificati. CHECK TABLE mirati per InnoDB richiedono
revisione del carico e autorizzazione nella fase di validazione: non eseguire un
mysqlcheck globale automatico o repair. Esito di CHECK TABLE non equivale a
validazione semantica di tutti i dati. Per Aria applicative sospette tornare a
backup e diagnosi offline, senza repair indiscriminato. Conservare nuovi backup
logici privati quando il server è stabile. Nessuna migrazione MT5B automatica.

## 10. Autorizzazioni, limiti e Git

| Gate | Operazioni da autorizzare separatamente |
| --- | --- |
| A | Finestra e tentativo shutdown regolare; se fallisce STOP, nuovo piano |
| B | Creazione area privata/ACL, backup cold, copia configurazioni/log e manifest |
| C | Copia offline e controlli aria_chk non riparanti |
| D | Tentativi di repair su copie indipendenti, tabella per tabella |
| E | Metodo preciso sull'originale dopo risultati offline e rischio accettato |
| F | Riavvio controllato e validazioni concordate |
| Separata | Qualsiasi restore, escalation, intervento ulteriore su processo bloccato |

Oggi solo letture, inventari, version/help e scrittura di questo documento.
Nessun arresto, restart, backup, check tabella, repair, SQL, DDL/DML o modifica
di configurazioni/utenti/file del datadir effettuati. Nessun privilegio aggiuntivo
richiesto. Mancano intenzionalmente i risultati offline, ottenibili solo dopo
autorizzazione e shutdown riuscito. Nessuna garanzia preventiva di repair senza
perdita e nessun percorso automatico oltre un gate fallito.

Branch `refactor-database-entities`; inizialmente non tracciati i precedenti due
report XAMPP. Questa attività aggiunge solo questo report; nessun file applicativo
modificato, commit o push. I sei SQL MT5B restano mancanti e non sono oggetto del
recupero. Destinazione proposta non creata, parent e spazio verificati; ACL e
assenza writer sono gate da verificare al momento della futura esecuzione.

**XAMPP ARIA RECOVERY PLAN READY — AWAITING AUTHORIZATION**
