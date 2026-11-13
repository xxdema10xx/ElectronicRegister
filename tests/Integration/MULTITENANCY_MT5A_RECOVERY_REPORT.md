# MT-5A Recovery e final validation

**MT-5A PARZIALE. Diagnosi completata; validazione finale bloccata dall'assenza di uno
schema MT-4C completo reperibile nell'ambiente locale esaminato.** Nessun codice applicativo
modificato, servizio avviato/fermato, porta cambiata, restore, migrazione o DML eseguiti.
Rollout chiuso e nessun MT-5B.

## Git e SDK

Branch `refactor-database-entities`, HEAD `a002cb66cebecfdcdf892c099cf0518b796b7e1a`.
Workspace iniziale con tutte le modifiche MT5A precedenti, preservate. SDK10.0.401.
[Status iniziale](TestResults/mt5a-recovery/initial-git-status.txt),
[HEAD](TestResults/mt5a-recovery/initial-head.txt).
Nessun reset, checkout, commit, push o pull. Solo documentazione ed evidenze recovery aggiunte.

## Diagnosi delle istanze locali

| Installazione | Identificazione | Stato osservato e database test |
| --- | --- | --- |
| MAMP | C:\MAMP\bin\mysql\bin\mysqld.exe, MySQL5.7.24 | Processo8948, unico mysqld osservato; ascolto TCP0.0.0.0:3306. Datadir effettivo C:\MAMP\db\mysql\; electronic_register_test presente, schema precedente MT4. MAMP.exe processo19800. |
| XAMPP | C:\xampp\mysql\bin\mysqld.exe, 10.4.32-MariaDB (verificato con --version) | Nessun processo MariaDB/XAMPP SQL osservato. my.ini configura porta3306 e datadir C:\xampp\mysql\data. In tale datadir non esiste electronic_register_test. Nessun avvio tentato. |
| MySQL80 | Servizio Windows MySQL80, C:\Program Files\MySQL\MySQL Server 8.0\bin\mysqld.exe | STOPPED, avvio manuale. Configurazione C:\ProgramData\MySQL\MySQL Server 8.0\my.ini: porta3306, datadir Data. electronic_register_test non reperito nel percorso configurato. Nessun avvio. |

Get-Process e netstat identificano eseguibile/PID/porta. Get-CimInstance su processi/servizi
e Get-NetTCPConnection non hanno fornito l'inventario nella sandbox: CIM esplicitamente
Accesso negato. Usati fallback read-only Get-Process, netstat e sc.exe query/qc.
Nessuna ulteriore porta SQL osservata per il processo mysqld, ne listener su3307/3308/8889.
Questo descrive le istanze/processi e i percorsi standard accessibili, non esclude copie
archiviate in altri percorsi, macchine remote o datadir non configurati.

Letti soltanto parametri non segreti dei file my.ini: sezioni, port, basedir, datadir,
socket/bind-address. XAMPP Control Panel non risulta attivo tra i processi osservati;
xampp-control.ini ha EnableModules/MySQL=1, che non prova un servizio SQL attivo.
Non e stata aperta o azionata l'interfaccia grafica. Entrambe le installazioni MAMP/XAMPP
configurano3306: non si puo avviare automaticamente XAMPP su quella porta occupata.

[Processi](TestResults/mt5a-recovery/processes.json),
[listener](TestResults/mt5a-recovery/sql-listeners.txt),
[servizio MySQL80](TestResults/mt5a-recovery/mysql80-service.txt).

## Server raggiunto dalla connection string effettiva

TEST_MYSQL_CONNECTION e stata usata senza mostrarne credenziali; target verificato
loopback/porta3306/database esatto prima dell'accesso. Il client MAMP e stato forzato TCP,
con password solo nell'ambiente temporaneo MYSQL_PWD, ripristinato al termine.
Usare il client XAMPP non seleziona il server XAMPP: e sempre il listener a determinare
l'istanza raggiunta. Nessuna connection string operativa o impostazione persistente cambiata.

| Query | Risposta |
| --- | --- |
| VERSION() | 5.7.24 |
| @@version_comment | MySQL Community Server (GPL) |
| @@hostname | DESKTOP-QUOAG32 |
| @@port | 3306 |
| @@datadir | C:\MAMP\db\mysql\ |
| DATABASE() | electronic_register_test |
| @@global.sql_mode | ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION |
| @@session.sql_mode | identico a GLOBAL nella sessione diagnostica |

La diagnosi non imposta ne GLOBAL ne SESSION: sono sole SELECT/SHOW. ER_TEST_STRICT_SESSION
resta una configurazione per i test futuri, non serve alterare sessioni per leggere metadati.
[Snapshot SQL completo prima](TestResults/mt5a-recovery/server-before.txt),
[dopo](TestResults/mt5a-recovery/server-after.txt),
[script read-only](TestResults/mt5a-recovery/Inspect-TestDatabase.ps1).

## Causa della differenza con MT-4C

Il report MT4C documenta MariaDB10.4.32 XAMPP sul loopback; i TRX storici final-2
hanno computerName **CES01622**, mentre l'ambiente SQL corrente e **DESKTOP-QUOAG32**.
Non e quindi provato che il database MT4C sia scomparso da questa installazione:
le evidenze storiche provengono da una macchina diversa. Un identico127.0.0.1:3306/database
non identifica la stessa istanza su due computer. Qui raggiunge il MySQL di MAMP.

Confermata la causa immediata del blocco: identita server/datadir/schema diversi dalla
baseline certificata. Il cambio di macchina e sostenuto da TRX e hostname; non e invece
ricostruita la storia di eventuali copie/importazioni del DB tra macchine, ne dimostrata
la disponibilita attuale del server storico CES01622. Nessun tentativo di connessione remota.

## Confronto schema effettivo con i canonici MT4

Non e stato usato soltanto il conteggio. Inventariati colonne/tipi/default, indici/ordine,
FK/parent/regole e definizioni trigger, piu SHOW CREATE delle membership e Class.
Confrontati migrations/mt4/001_expand.sql e004_enrollment_audit_expand.sql e il validate006.
Il validate006 non viene eseguito contro tabelle assenti e non vengono lanciati runner.

| Requisito MT4C | Osservazione sul DB MAMP |
| --- | --- |
| course_edition, course_edition_campus, student_enrollment, course_administration_event | tutte assenti nell'inventario information_schema |
| student_enrollment.revision BIGINT default0 | assente insieme alla tabella |
| class.course_edition_id e fk_mt4_class_edition/indice | assenti; SHOW CREATE class conserva il modello MT3A |
| uq_mt4c_membership_actor(institution_id,id,user_id) | assente; presenti PK, UNIQUE User/Institution, UNIQUE tenant/Id e indice tenant MT1 |
| Sei FK ledger RESTRICT e relativi indici | assenti insieme al ledger |
| Otto trigger MT4A e cinque MT4C | nessun trigger mt4_/mt4c_; presenti40 trigger MT3A |
| Holding MT3A_LEGACY_TEST | presente Provisioning |

Totali28 tabelle/40 trigger,15 User, zero membership/ruoli. Le FK/indici MT1/MT3A osservati
non sostituiscono i vincoli MT4C mancanti. Nessun preflight MT4C compatibile disponibile.

## Backup storici trovati, senza ripristino

Due dump privati gia presenti in migrations/mt4/private, ispezionati solo in memoria per
nomi/strutture e SHA256; contenuti personali non stampati o copiati nei report.
[Metadati non sensibili](TestResults/mt5a-recovery/backup-metadata.json).

| File | Definizioni tabelle | Stato osservato |
| --- | ---: | --- |
| 20261019-141428-3269.sql | 28 | nessuna CourseEdition/Enrollment/ledger |
| 20261019-165115-0036.sql | 31 | CourseEdition/Enrollment presenti, ledger/revision/trigger MT4C assenti |

Il secondo ha SHA256
`80797A2C793366040D9B231544FA1AC7CD0770E5706F7AB6EDB806E88BBC022A`,
uguale al backup documentato MT4C **prima** dell'espansione004. E una sorgente storica
precedente a MT4C, non un dump completo dello schema finale32tabelle/53trigger.
Nessun restore certificato o autorizzato viene dedotto dall'uguaglianza dell'hash.

## Operazioni e verifiche effettuate

Solo inventario Git/SDK/processi/listener/servizi, letture di configurazioni non segrete,
versione binari (`mysqld --version`, senza avvio), SELECT/SHOW sul solo DB test,
confronto script/report/TRX e metadati backup. Nessun accesso SQL ad altri database,
DDL/DML/EnsureCreated, preparazione MT3C, disabilitazione vincoli/trigger o cambiamento porte.
Nessun build/test nuovo: come richiesto, lo schema corretto non e stato reperito e il
recupero si ferma alla diagnosi. Non si ripetono624 fallimenti gia spiegati dal preflight.

I due snapshot SQL prima/dopo sono identici byte per byte; hash in
[environment-verification.json](TestResults/mt5a-recovery/environment-verification.json).
Questo confronta identita server, schema, trigger, stati e conteggi catturati, non costituisce
un digest di ogni cella dei dati. L'assenza di scritture recovery deriva anche dalle sole
SELECT/SHOW eseguite; nessuna fixture o password/membership persistente modificata.

## Risultati test preservati e stato validazione

Riletti/verificati TRX e675 identita precedenti con lo script gia esistente, senza
cambiare esiti o rimuovere file. La riesecuzione del verificatore produce lo stesso riepilogo;
non e una nuova esecuzione dei test. Nessuna regressione applicativa nuova dimostrata.

| Esecuzione | Unit Passed | Integration Passed | Failed | Skipped | Totale |
| --- | ---: | ---: | ---: | ---: | ---: |
| MT4C storico (CES01622) | 51 | 624 | 0 | 0 | 675 |
| Baseline MT5A precedente (MAMP) | 51 | 0 | 624 | 0 | 675 |
| Targeted-3 MT5A precedente | — | 36 | 0 | 0 | 36 |
| Finale1 MT5A precedente | 51 | 36 | 624 | 0 | 711 |
| Finale2 MT5A precedente | 51 | 36 | 624 | 0 | 711 |
| Recovery | non eseguiti | non eseguiti | — | — | — |

[Verifica identita/contatori/hash](TestResults/mt5a/verification.json),
[categorie624 errori preflight per run](TestResults/mt5a/failure-categories.json),
[baseline TRX](TestResults/mt5a/baseline/), [36 test](TestResults/mt5a/targeted-3/),
[finale1](TestResults/mt5a/final-1/), [finale2](TestResults/mt5a/final-2/).
Non ci sono nuovi TRX recovery e non esistono due run711/711 verdi.

Sicurezza: il codice/fixture MT5A non viene modificato. Rimangono le prove mirate precedenti
per isolamento A/B/JWT/revoche/ruoli/DTO/read-only/gate/bootstrap/flag. Non si dichiara una nuova
certificazione di tali garanzie su MariaDB, ne di worker MT3E e audit MT4C: serve l'ambiente corretto.
Nessuna nuova route o mutazione membership. I flag rimangono chiusi come nel checkpoint precedente.

## Piano proposto, NON eseguito e soggetto ad autorizzazione

Priorita: recuperare su CES01622 o dall'archivio del checkpoint un export finale MT4C
verificato del solo electronic_register_test. Verificare VERSION/hostname/datadir/schema e
manifest32tabelle/53trigger sul sorgente; produrre backup consistente/hash in archivio privato,
senza dati personali nei report. Non intervenire sul MAMP attuale per ottenere il verde.

Se occorre un'istanza isolata locale, destinazione proposta:
`C:\Users\Lorenzo\ElectronicRegister\.local\mt5a-recovery-mariadb`, porta loopback13306
previa verifica disponibilita, schema sempre electronic_register_test. Configurazione/datadir
dedicati, nessuna modifica a C:\MAMP, C:\xampp o ai servizi Windows. Occorre prima approvare
inizializzazione, account/privilegi e restore del dump finale autentico. Rischi: dati personali
nei backup, compatibilita MariaDB, privilegi/definer, collisione porta e path, test che scrivono
fixture/teardown. Conservare backup del sorgente ed eventuale destinazione esistente; vietato
riutilizzare una destinazione gia popolata o distruggerla automaticamente.

Comandi proposti per le fasi successive, non eseguiti:

```powershell
# Solo dopo approvazione e verifica di configurazione/datadir isolati gia inizializzati:
& C:/xampp/mysql/bin/mysqld.exe --defaults-file=C:/Users/Lorenzo/ElectronicRegister/.local/mt5a-recovery-mariadb/my.ini --console
# Il restore richiede prima un comando specifico revisionato sul dump finale verificato;
# non viene fornito un import alla cieca o un'inizializzazione distruttiva.
# Password tramite canale segreto; niente credenziali nella command line/report.
```

Se l'unico artefatto recuperabile resta il backup31tabelle precedente a MT4C, il piano cambia:
verificarne ripristinabilita in istanza nuova autorizzata, poi valutare separatamente
l'applicazione canonica004/005/006 dopo backup/preflight. Questo richiede approvazione esplicita
e non equivale a ritrovare il database finale storico. Nessuna migrazione del MAMP MySQL5.7.

Dopo un preflight MT4C positivo sulla destinazione approvata, usare configurazione solo per
il processo test, ER_TEST_STRICT_SESSION=1 e dump client/flavor verificati. Sequenza prevista:

```powershell
dotnet build Backend/ElectronicRegisterAPI/ElectronicRegisterAPI.slnx --no-restore
dotnet test Backend/ElectronicRegisterAPI/ElectronicRegisterAPI.slnx --no-build --no-restore --filter 'FullyQualifiedName!~Mt5aMembershipAdminTests' --logger trx --results-directory tests/Integration/TestResults/mt5a-recovery/baseline
dotnet test tests/Integration/Integration.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~Mt5aMembershipAdminTests' --logger trx --results-directory tests/Integration/TestResults/mt5a-recovery/targeted
dotnet test Backend/ElectronicRegisterAPI/ElectronicRegisterAPI.slnx --no-build --no-restore --logger trx --results-directory tests/Integration/TestResults/mt5a-recovery/final-1
dotnet test Backend/ElectronicRegisterAPI/ElectronicRegisterAPI.slnx --no-build --no-restore --logger trx --results-directory tests/Integration/TestResults/mt5a-recovery/final-2
```

Il filtro baseline conserva tutti675 test precedenti, non li sostituisce. Verificare ogni
outcome/identita, baseline675, targeted36, due711/711, zero Failed/Skipped, source freeze,
bootstrap/gate/worker/audit e stato DB/GLOBAL prima/dopo. MYSQLDUMP_EXE/MYSQLDUMP_FLAVOR
solo temporanei se necessari, mai configurazioni operative. Nessun passaggio di questo piano
e stato avviato durante la recovery.

## Decisione finale

**MT-5A resta PARZIALE.** Blocco preciso: sulla macchina corrente e attivo il database
MAMP MySQL5.7 preMT4; l'istanza/schema MariaDB MT4C del checkpoint eseguito su CES01622
non e stata ritrovata localmente. Il binario MariaDB installato e un backup preMT4C non
soddisfano quel prerequisito. Serve recuperare l'ambiente/snapshot corretto o autorizzare
separatamente un ambiente isolato e il relativo piano. Nessuna modifica ai dati per
fabbricare la baseline, nessun MT5B. git diff --check superato.
