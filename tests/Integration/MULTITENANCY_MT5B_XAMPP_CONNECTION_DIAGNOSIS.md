# XAMPP MariaDB — diagnosi connessione MT-5B

Verifica: 21 ottobre 2026, circa 09:39–09:43 Europe/Rome.
Letti il piano XAMPP e il riepilogo aggiornato MT5B. Nessuna analisi storica
MT1–MT5A ripetuta. Nessun intervento su server, configurazione o dati eseguito.

## Causa accertata e limiti

Il server fallisce l'inizializzazione delle tabelle dei privilegi:
**indice corrotto di `mysql.proxies_priv`**. Il registro Application di Windows
contiene la sequenza seguente, tutta attribuita al processo corrente 6524:

| Ora locale | EventRecordID | Testo EventData |
| --- | --- | --- |
| 09:36:12 | 14494 | Table '.\\mysql\\proxies_priv' is marked as crashed and should be repaired |
| 09:36:12 | 14495 | Recovering table: '.\\mysql\\proxies_priv' |
| 09:36:12 | 14496 | Couldn't repair table: mysql.proxies_priv |
| 09:36:12 | 14497 | Fatal error: Can't open and lock privilege tables: Index for table 'proxies_priv' is corrupt; try to repair it |
| 09:36:12 | 14498 | Aborting |
| 09:36:14 | 14499 | mysqld.exe: Aria engine: checkpoint failed |

È dimostrato il fallimento dell'avvio, che spiega l'indisponibilità osservata
durante l'handshake. Non è dimostrata la causa originaria della corruzione né
l'assenza di ulteriori danni. Non è diagnosticato un crash InnoDB o un problema
di password. Il tentativo automatico di recupero registrato precede questa
diagnosi: non è stato invocato dall'agente.

## Processo, rete e log

- `Get-Process`: PID 6524, `C:/xampp/mysql/bin/mysqld.exe`, avviato alle 09:36:12.
  Processo ancora presente al controllo finale, CPU circa 0,203125 s;
  Responding=True non dimostra che un daemon sia pronto a servire richieste.
  Thread osservati in attesa; nessun riavvio durante le verifiche.
- `netstat -ano -p tcp`: `0.0.0.0:3306 LISTENING`, PID 6524; nessun listener 13306.
  La presenza del listener non equivale a completamento dell'avvio.
- Nessun secondo processo mysqld rilevato; Get-Service non restituisce servizi
  con nome/display name MySQL o MariaDB. Nessuna evidenza di conflitto di porta.
  Non è un inventario completo di ogni servizio o software di rete.
- `C:/xampp/mysql/data/mysql.pid` contiene 6524.
- `mysql_error.log`, 95.288 byte, ultima modifica 09:36:12: recovery Aria terminata,
  recovery InnoDB da LSN 535282644, InnoDB avviato a LSN 535282653,
  transaction id 415993, buffer pool caricato, socket creato su `::`.
  Nel segmento dell'avvio corrente non compare un errore fatale InnoDB.
  Il file si ferma prima degli errori di privilegi: **il solo file log era
  insufficiente**; gli errori decisivi sono nel registro eventi Windows.
- Gli eventi sono stati letti via Get-WinEvent sul registro Application e
  decodificati con ToXml/EventData. Il filtro diretto ProviderName fallisce per
  registrazione provider mancante; il filtro sul registro funziona.

## Configurazione e versione

`C:/xampp/mysql/bin/my.ini`, ultima modifica 11 giugno 2026:

| Parametro | Valore nel file |
| --- | --- |
| port | 3306, client e mysqld |
| basedir / datadir | C:/xampp/mysql / C:/xampp/mysql/data |
| socket | C:/xampp/mysql/mysql.sock |
| pid_file / log_error | mysql.pid / mysql_error.log |
| bind-address | direttive commentate |
| skip-networking | commentato; TCP effettivamente in ascolto |
| enable-named-pipe | commentato |
| connect/read/write timeout | nessun override esplicito trovato |
| innodb_lock_wait_timeout | 50, riguarda lock transazionali |
| max_allowed_packet / net_buffer_length | 1M / 8K |
| innodb_buffer_pool_size / innodb_log_file_size | 16M / 5M |
| sql_mode | NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION |
| charset / collation | utf8mb4 / utf8mb4_general_ci |

Nessuna direttiva SSL attiva trovata nel file. `mysql_start.bat` indica
`mysqld --defaults-file=mysql\\bin\\my.ini --standalone`; è stato solamente letto.
Il file e il launcher sono coerenti con processo, pid e log, ma la command line
del processo e le variabili effettive non sono state acquisite: non si certifica
che ogni valore del file sia quello runtime. Nessun file alternativo trovato nei
percorsi controllati C:/my.ini, C:/my.cnf, C:/Windows/my.ini, C:/Windows/my.cnf,
C:/xampp/mysql/my.ini e C:/xampp/mysql/data/my.ini.

`mysqld.exe --no-defaults --version` restituisce **10.4.32-MariaDB Win64 AMD64**.
È un comando di sola stampa versione, non un avvio del server.
Il registro conferma la stessa versione per PID 6524, revision
`c4143f909528e3fab0677a28631d10389354c491`. Identità OS/binario accertata;
**identità SQL non verificata**.

## Prove di connessione e stato schema

Prova client XAMPP senza opzioni implicite, TCP esplicito 127.0.0.1:3306,
root senza password fornita, solo database electronic_register_test,
`--skip-ssl --connect-timeout=4`: ERROR 2013, initial communication packet.
Non sono state stampate o cercate credenziali. La query SELECT predisposta
non è stata eseguita. Il precedente piano registra lo stesso errore senza
skip-ssl e con localhost: non emerge una soluzione tramite opzioni SSL/client.

Prova indipendente con System.Net.Sockets.TcpClient: connessione TCP accettata,
nessun greeting ricevuto entro circa 4.059 ms; nessun dato applicativo inviato.
Questo localizza il sintomo prima dell'autenticazione e della negoziazione SSL;
non giustifica cambio password, aumento timeout o modifica del protocollo.
Non provati named pipe/shared memory: non risultano abilitati e non risolvono
il fallimento fatale dell'inizializzazione dei privilegi.

VERSION(), version_comment, hostname SQL, port SQL, datadir SQL, DATABASE() e
GLOBAL sql_mode **non acquisiti**. Il nome computer del registro è CES01622,
che non viene equiparato a @@hostname.

Il database atteso resta esclusivamente electronic_register_test. Tabelle,
trigger, FK, indici e definizioni non sono verificati via SQL; **MT4C non
accertato, stato MT5B non accertato**. I nomi file osservati nel precedente piano
non vengono usati come certificazione dello schema. Nessun database alternativo
utilizzato e nessuna interrogazione applicativa al database operativo.

## Percorso di recupero proposto — NON eseguito

La fase read-only non offre un modo per rendere pronto questo processo.
Serve autorizzazione esplicita per una finestra di manutenzione dell'intera
istanza XAMPP: la tabella danneggiata è di sistema e il server è condiviso.
Il rischio comprende indisponibilità dei database esistenti e perdita di righe
di privilegi durante una riparazione. Non si propone alcun reset dei privilegi.

1. Prima di interventi, preservare configurazione, log ed eventi. Autorizzare
   arresto controllato dal pannello XAMPP e verificare uscita del PID/listener.
   Se l'arresto non termina, fermarsi: nessun taskkill forzato implicito.
2. A processo completamente fermo, acquisire **backup fisico completo a freddo
   del datadir**, inclusi database operativi, mysql, file InnoDB e Aria, più
   my.ini e log; destinazione privata nuova fuori dal datadir con spazio adeguato,
   inventario e SHA256. Il dump logico non è attualmente disponibile. Una copia
   dei soli proxies_priv o una copia live non sostituisce questo backup.
3. Diagnosticare offline prima una copia di lavoro separata dei file Aria,
   con binario XAMPP esistente. Eventuale repair sulla copia e poi sul target
   richiede autorizzazione specifica; confrontare esiti e righe recuperate.
   Non sovrascrivere con file .old/.BAK di provenienza non verificata.
4. Solo dopo esito e revisione, autorizzare eventuale riparazione della sola
   tabella interessata e riavvio controllato. Se emergono altri errori Aria,
   preservare lo stato e rivalutare; non eliminare aria_log*, ibdata1 o redo log.
5. Dopo avvio sano, ripetere SELECT identità e inventario information_schema
   del solo electronic_register_test; poi backup logico e confronto canonico.
   Nessun avvio JWT o applicazione MT5B in questa fase.

Comandi candidati da revisionare **prima dell'autorizzazione**; non eseguiti:

```powershell
# DOPO arresto autorizzato e confermato: esempio destinazione privata nuova.
# Verificare spazio, ACL e assenza della destinazione prima di copiare.
robocopy C:\xampp\mysql\data D:\ER-Recovery\20261021\cold-data /E /COPY:DAT /DCOPY:DAT /R:0 /W:0 /XJ
Copy-Item -LiteralPath C:\xampp\mysql\bin\my.ini -Destination D:\ER-Recovery\20261021\my.ini
# Verificare esito robocopy, inventario completo e hash; non usare /MIR.

# SOLO su copia di lavoro preparata dal backup, mai sul server attivo:
& C:\xampp\mysql\bin\aria_chk.exe --no-defaults --check --read-only --datadir=D:\ER-Recovery\20261021\work D:\ER-Recovery\20261021\work\mysql\proxies_priv.MAI
# Candidato di riparazione sulla COPIA, da autorizzare dopo il check:
& C:\xampp\mysql\bin\aria_chk.exe --no-defaults --recover --backup --datadir=D:\ER-Recovery\20261021\work D:\ER-Recovery\20261021\work\mysql\proxies_priv.MAI
```

`D:` è un esempio, non un volume verificato. Il comando definitivo, la directory
di lavoro e l'eventuale intervento sul target dovranno essere fissati dopo il
backup e il check. Non si fornisce un comando di restore o riparazione live.
La disponibilità di aria_chk e delle opzioni indicate è stata verificata solo
con `--help`, senza aprire o controllare tabelle.

Sul disco esistono proxies_priv.frm/.MAI/.MAD e varianti .old/.BAK;
MAI/MAD correnti hanno timestamp 09:36:12. Nessun file modificato o copiato.
I file storici non sono stati qualificati come backup valido.

## Git e perimetro finale

Branch refactor-database-entities. I sei SQL MT5B 000–005 sono tuttora assenti
dal checkout e non tracciati: git ls-files elenca soltanto README e due script
PowerShell in migrations/mt5; git check-ignore conferma `.gitignore:26:*.sql`
per tutti i sei percorsi. Manifest hash già presente, nessuna ricostruzione,
recupero o modifica della regola Git eseguiti.

All'inizio risultava non tracciato il precedente piano XAMPP; questa attività
aggiunge solo il presente report. Nessuna modifica all'applicazione, nessun
commit/push, migrazione, restore, repair, restart, DML, modifica password,
tabella di sistema o GLOBAL sql_mode eseguiti. Nessun backup acquisito.
Le azioni di manutenzione sopra descritte restano subordinate all'autorizzazione
dell'utente, come richiesto esplicitamente nella fase 3.

**XAMPP CONNECTION BLOCKED — ACTION REQUIRED**
