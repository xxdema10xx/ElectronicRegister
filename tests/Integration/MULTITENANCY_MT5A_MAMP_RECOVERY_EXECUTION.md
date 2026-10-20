# MT-5A — MAMP Recovery, Execution Phase A

**MT-4C RESTORED ON ISOLATED MAMP INSTANCE — READY FOR TEST VALIDATION**

Schema ricostruito e verificato su **MySQL5.7.24 MAMP**, nella sola nuova istanza
127.0.0.1:13306. Non e una certificazione MariaDB10.4. **MT-5A resta PARZIALE**:
nessuna build/suite/test eseguiti in questa fase e nessuna dichiarazione711/711.
Autorizzazione limitata alla fase A rispettata; nessun MT5B, commit, push o modifica applicativa.

## 1. Git iniziale e perimetro

Piano letto integralmente:
[MULTITENANCY_MT5A_MAMP_RECOVERY_PLAN.md](MULTITENANCY_MT5A_MAMP_RECOVERY_PLAN.md).
Branch refactor-database-entities; HEAD a002cb66cebecfdcdf892c099cf0518b796b7e1a.
Workspace iniziale gia modificato da MT5A e report precedenti, preservato senza reset/checkout.
[Status iniziale](TestResults/mt5a-mamp-execution/initial-git-status.txt),
[HEAD](TestResults/mt5a-mamp-execution/initial-head.txt).

Solo nuova directory privata, strumenti diagnostici privati, evidenze non sensibili e questo
report aggiunti. Verificati370 file sorgente C# contro il freeze precedente: zero modifiche.
Verificati i sei SQL canonici contro gli hash del piano: zero modifiche.
Nessun runner con percorsi XAMPP eseguito; nessun EnsureCreated/preparatore MT3C/cutover.

## 2. Istanza MAMP originale e preflight

Server originale verificato **prima** delle operazioni:
MySQL5.7.24, hostname DESKTOP-QUOAG32, porta3306, PID8948,
C:\MAMP\bin\mysql\bin\mysqld.exe, datadir C:\MAMP\db\mysql,
database electronic_register_test.28tabelle/40trigger, holding MT3A_LEGACY_TEST Provisioning.
L'intero snapshot di identita/colonne/FK/indici/trigger/stati/conteggi coincide con quello
conservato nel piano; non risultava gia applicato MT4.

Porta13306 libera, destinazione assente, circa495GB liberi (minimo richiesto2GiB), hash
dump atteso,31tabelle/48trigger e footer verificati. git check-ignore ha confermato
l'esclusione della destinazione privata. Nessun cambio automatico di porta/percorso/strategia.
[Preflight](TestResults/mt5a-mamp-execution/preflight.json),
[snapshot originale](TestResults/mt5a-mamp-execution/original-preflight.txt).

## 3. Backup, SHA256 e protezione dati

Importata esclusivamente una copia byte-identica del dump storico
20261019-165115-0036.sql,150879byte,31tabelle/48trigger/92FK:

`80797A2C793366040D9B231544FA1AC7CD0770E5706F7AB6EDB806E88BBC022A`.

Prima dell'inizializzazione effettuato backup **read-only** del solo database test originale
su3306, con mysqldump MAMP5.7, single-transaction/skip-lock-tables, trigger/routine,
ordine deterministico e senza data del dump.28definizioni/40trigger/footer verificati.
SHA256 prima e dopo incarico identici:

`8508D8F09C79128D206E707ACD5109BA9C8E72AB93B2F06482AAA61D0744C06E`.

Backup aggiuntivi della sola copia isolata:

| Artefatto privato | Tabelle/trigger | SHA256 |
| --- | --- | --- |
| empty-before-restore.sql | 0/0 | 6C7F49EC37CD8D97098F85142F095BE40B484844573A947E0B8DE704113669D2 |
| before-mt4c.sql | 31/48 | 828A2F693F4AC5F85F0155E3BCF5651035636245830C39DB1F0D597B9D421483 |
| final-mt4c.sql | 32/53 | E5CD627A97E45E752155B98FD41BAFF4FE04FD8F076C5A882D2304FA587D12E7 |

Inventari/footer/hash verificati, nessun backup sovrascritto o aggiunto a Git. Il dump finale
e un nuovo backup del risultato ricostruito su MySQL, non il database storico MariaDB recuperato.
Dump con dati personali/hash password, credenziali e log dettagliati rimangono sotto ACL private.
Nei report soltanto metadati, counts e digest; nessuna riga personale/password/token.
[Backup originale](TestResults/mt5a-mamp-execution/original-backup.json),
[backup pre-DDL](TestResults/mt5a-mamp-execution/before-mt4c-backup.json),
[backup finale](TestResults/mt5a-mamp-execution/final-mt4c-backup.json).

## 4. Nuova istanza indipendente

Parent creato ex novo:
`C:\Users\Lorenzo\ElectronicRegister\migrations\mt4\private\mt5a-mamp-13306`.
Datadir nella sua sottodirectory data; config my.ini e client.ini, log/tmp/import/backup
separati. ACL parent senza ereditarieta, sole due regole: utente corrente e SYSTEM,
full control ereditato dai figli. ACL verificate anche sui file credenziali.
[Protezione directory](TestResults/mt5a-mamp-execution/private-directory.json),
[credenziali](TestResults/mt5a-mamp-execution/credential-protection.json).

Unico eseguibile server utilizzato: **C:\MAMP\bin\mysql\bin\mysqld.exe**.
Nessun server XAMPP, installazione o servizio Windows creati/modificati.

## 5. Porta e isolamento verificati

Nuovo PID **24340**, coerente con pid-file e percorso binario. Listener unico
**127.0.0.1:13306**, non0.0.0.0. PID/listener controllati prima del primo accesso SQL
e ripetutamente prima delle operazioni. Named pipe/shared memory/local infile disabilitati,
log-syslog=0 per evitare EventLog; nessuna modifica alla3306.

Identita SQL iniziale/finale confermata attraverso connessioni nuove:

| Proprietà | Valore finale |
| --- | --- |
| VERSION() | 5.7.24 |
| @@hostname | DESKTOP-QUOAG32 |
| @@port | 13306 |
| @@datadir | C:\Users\Lorenzo\ElectronicRegister\migrations\mt4\private\mt5a-mamp-13306\data\ |
| DATABASE() | electronic_register_test |

[Processo/listener](TestResults/mt5a-mamp-execution/isolated-process.json),
[identita SQL finale](TestResults/mt5a-mamp-execution/final-isolated-identity.json).

## 6. Inizializzazione e bootstrap

Datadir controllato vuoto prima di --initialize; inizializzazione riuscita **al primo
tentativo**, senza avviare servizi o usare directory esistenti. Password iniziale generata
dal server nel log riservato; bootstrap protetto ha assegnato una nuova password casuale
crittografica al root della sola copia. Nessuna password predefinita o in argv/output pubblico.
Avvio con Start-Process Hidden e configurazione separata; bootstrap.sql rimosso dopo verifica
dell'identita SQL. Nessun riavvio/reinizializzazione/retry di operazioni mutative.
[Inizializzazione](TestResults/mt5a-mamp-execution/initialization.json).

Credenziali dei client custodite in client.ini privato, non in appsettings o file pubblici.
Per il backup originale password passata soltanto nell'ambiente del processo figlio.
TEST_MYSQL_CONNECTION della sessione chiamante conserva il target originale3306 e nessuna
MYSQL_PWD rimane nell'ambiente. Nessun privilegio conferito a identita HTTP.

## 7. Restore MT-4A

Verificata assenza del database nella nuova istanza; creato electronic_register_test,
controllato a zero tabelle e prodotto backup vuoto verificato prima dell'import.
Predisposto il definer storico **er_test@localhost**, password casuale e **ACCOUNT LOCK**,
con SELECT/INSERT/UPDATE/DELETE/TRIGGER limitati al solo database test. root@localhost della
copia gestisce restore/migrazione. Nessun definer del dump riscritto o eliminato.

Restore eseguito **una sola volta**, SQL originale byte-identico via stdin UTF8/binario,
senza --force. Accettate esclusivamente le impostazioni FK/unique della sessione presenti
nel dump autorizzato; nessun SET GLOBAL o disattivazione trigger aggiuntiva.
Esito immediato:31tabelle/48trigger/92FK, nessun errore di import.
[Esito restore](TestResults/mt5a-mamp-execution/restore.json).

## 8. Verifica dati, FK, colonne e indici MT-4A

Verificati contro le definizioni del dump: nomi/engine delle31tabelle,201colonne con ordine,
tipo/nullabilita/default/auto_increment,131indici con unicita/ordine colonne,92FK con parent,
colonne/ordine e UPDATE/DELETE rule. Per **tutte92FK** eseguite anti-join read-only corrette
per FK composite e nullability: **zero orfani**. Profili/owner restano nei vincoli del dump.

Confrontate anche le48 definizioni trigger, tabella/evento/timing/definer e corpo normalizzato
per whitespace. Ledger/revision/artefattiMT4C assenti prima dell'espansione; holding Provisioning;
nuove sessioni con FOREIGN_KEY_CHECKS=1 e UNIQUE_CHECKS=1.

Dump della copia restaurata confrontato con il dump sorgente per tutte31tabelle: tuple INSERT
ordinate, numero righe e SHA256 identici, zero differenze. Dati/hash personali confrontati
soltanto in memoria/file privati. Nessuna riga sintetica aggiunta per far passare i controlli.
[Schema restaurato](TestResults/mt5a-mamp-execution/restored-schema-validation.json),
[FK](TestResults/mt5a-mamp-execution/restored-fk-integrity.json),
[trigger](TestResults/mt5a-mamp-execution/restored-trigger-integrity.json),
[digest dati](TestResults/mt5a-mamp-execution/restored-data-digests.json).

## 9. Migrazione canonica MT-4C

Soltanto dopo restore e verifiche precedenti:

1. Eseguito003_validate.sql, sette controlli a zero.
2. Ricontrollata assenza di ogni artefattoMT4C; acquisiti digest di tutte le colonne originali
   e backup pre-DDL31tabelle/48trigger verificato, spazio disponibile e identita destinazione.
3. Eseguito004_enrollment_audit_expand.sql **una sola volta**.
4. Eseguito005_enrollment_audit_backfill.sql: invalid_revision=0.
5. Eseguito006_enrollment_audit_validate.sql: cinque controlli a zero.

I file SQL canonici non sono stati modificati. Nessun runner XAMPP utilizzato. Modalita
strict impostata soltanto sulle sessioni della copia; nessuna modifica GLOBAL sql_mode.
Ledger vuoto, nessun evento storico, data/edizione/iscrizione o membership inventati.
[Migrazione](TestResults/mt5a-mamp-execution/migration.json).

## 10. Validatori finali su connessioni nuove

003 e006 ripetuti in sola lettura a schema finale, senza rieseguire DDL:

- MT4A:7/7 controlli, tutte violazioni0.
- MT4C:5/5 controlli, tutte violazioni0.
- Tutte98FK finali: definizioni attese92dal dump +6canoniche, zero orfani.
- Revision BIGINT(20) NOT NULL DEFAULT0; zero revision nonzero.
- Ledger:20colonne nell'ordine e con tipi/nullabilita previsti;0righe.
-131indici precedenti preservati,4nuovi espliciti verificati e4indici di supporto FK
  generati da MySQL verificati: **139indici** totali, nessun indice speculativo.

[Validatore MT4A finale](TestResults/mt5a-mamp-execution/final-mt4a-validator.json),
[MT4C finale](TestResults/mt5a-mamp-execution/final-mt4c-validator.json),
[98 FK](TestResults/mt5a-mamp-execution/final-fk-integrity.json),
[indici/colonne](TestResults/mt5a-mamp-execution/final-index-column-verification.json).

## 11. Trigger e integrita dei dati preesistenti

53trigger presenti:48preesistenti preservati e5MT4C coerenti con canonico per tabella,
evento/timing, corpo eseguibile e definer. UPDATE ledger sempre SIGNAL; DELETE conserva
esattamente la restrizione append-only con l'eccezione tecnica canonica per sole fixture
MT4B disposable e root sul database test. Nessuna estensione della deroga.

MySQL5.7 elimina dall'ACTION_STATEMENT i due commenti standalone del trigger
mt4c_course_event_delete. Confronto iniziale byte/whitespace ha segnalato questa differenza;
verificato che **escludendo soltanto quei commenti**, SQL eseguibile e definer coincidono.
Nessun trigger ricreato o script canonico alterato. Questa normalizzazione diagnostica
non cambia controlli o messaggi SQL. Verifica di definizioni, non prova DML negativa/fixture
del comportamento append-only: quelle appartengono alla futura suite non autorizzata qui.
[Rappresentazione MySQL](TestResults/mt5a-mamp-execution/mysql-trigger-representation.json),
[53trigger](TestResults/mt5a-mamp-execution/final-trigger-integrity.json).

Prima/dopo004: digest per riga basati su valori originali con marker null e HEX(CAST AS BINARY),
colonne originali esplicite, ordinamento deterministico. **Tutte31tabelle e tutte le colonne
preesistenti identiche**, inclusi credenziali globali, date, stati e dati audit. La nuova Revision
e il ledger sono verificati separatamente, non mascherati nel confronto.
[Prima](TestResults/mt5a-mamp-execution/pre-mt4c-data-snapshot.json),
[dopo](TestResults/mt5a-mamp-execution/post-mt4c-data-snapshot.json).

## 12. Compatibilita effettivamente osservata su MySQL5.7

Inizializzazione/avvio con binari MAMP, restore del dump MariaDB MT4A non modificato,
DDL canonicoMT4C e SELECT dei validatori sono riusciti su **MySQL5.7.24**. FK composite,
SIGNAL/JSON_VALID e trigger multipli accettati. Non sono provate qui tutte le semantiche
applicative, concorrenza, worker o rollback dei711test. Nessuna certificazione MariaDB.

GLOBAL sql_mode originale e isolato invariati durante l'incarico:
ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,
ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION.
I modi delle singole sessioni di restore/migrazione non modificano i GLOBAL.

## 13. Stato finale del database originale

3306/PID8948 invariato, datadir MAMP originale,28tabelle/40trigger, holding Provisioning.
Snapshot metadata/schema/stato/conteggi prima/dopo identici. Backup logici completi
deterministici prima/dopo **byte-identici** con SHA2568508D8F0...744C06E sopra riportato.
Questa verifica include dati e definizioni del solo electronic_register_test; altri database
applicativi non sono stati interrogati o modificati da questo incarico.

Nessuna DDL/DML/restore sul server originale; soltanto SELECT/SHOW e backup read-only.
Nessuna modifica di porta, servizio, configurazione originale o password persistente originale.
[Verifica originale/sorgenti](TestResults/mt5a-mamp-execution/original-and-source-verification.json),
[snapshot finale originale](TestResults/mt5a-mamp-execution/original-final-preflight.txt).

## 14. Stato finale del database isolato

Istanza **lasciata attiva** su127.0.0.1:13306/PID24340 per la successiva fase autorizzata;
configurazione e credenziali riservate nella nuova directory privata ignorata da Git.
32tabelle/53trigger/98FK,139indici, holding Provisioning,15User,0membership,
ledger0righe e nessuna revision nonzero. Constraint checks riattivi su connessioni nuove.
Nessun dato artificiale o fixture di test creata. Backup finale verificato disponibile privatamente.
[Verifica finale isolata](TestResults/mt5a-mamp-execution/final-isolated-validation.json).

## 15. Problemi diagnostici riscontrati e risolti senza mutazioni ripetute

- Prima SELECT identita con IFNULL/DATABASE e stringa doppio-quotata: il client Windows
  ha rimosso la quotatura, causando1054. Corretta la sola SELECT usando DATABASE() diretto;
  identita confermata prima di CREATE DATABASE. Nessuna verifica positiva presunta dall'errore.
- Dot-sourcing iniziale del helper bloccato dalla ExecutionPolicy: nessuna query di quel
  blocco eseguita. Usati script privati con powershell -ExecutionPolicy Bypass del solo processo,
  senza cambiare policy globali, servizi o privilegi macchina.
- Generatore della prima anti-join FK trattava una singola colonna come stringa indicizzata,
  producendo riferimento inesistente c.a/errore1054. Corretto il solo script diagnostico privato
  per mantenere array di colonne; ripetute soltanto SELECT, poi92e98FK tutte verificate.
- Differenza di commenti nel metadata del trigger DELETE, analizzata al punto11.

Nessun errore di inizializzazione, restore o migrazione canonica. Nessun retry di DDL/import,
cancellazione o recovery distruttiva; ogni dubbio ha sospeso le mutazioni fino al chiarimento
read-only. Log diagnostici conservati privatamente, senza dati/credenziali nei report.

## 16. Prerequisiti per i711test e arresto della fase

**Non eseguita alcuna suite, baseline, test singolo o build.** Nessun nuovo TRX di fase A.
Richiesta futura separata per validazione711: configurare TEST_MYSQL_CONNECTION soltanto
nel processo test verso13306 con le credenziali private e guard VERSION/port/datadir/database;
l'ambiente chiamante e ancora sulla3306 e non e stato cambiato automaticamente.

Usare ER_TEST_STRICT_SESSION=1. Come gia analizzato nel piano, il backup helper dei test passa
--column-statistics=0: usare il client MySQL8.0.44 gia installato con MYSQLDUMP_EXE e flavor
MySQL temporanei, non fingere che MAMP sia MariaDB per bypassare opzioni. Verificare quel
tooling prima della suite. Nessuna modifica applicativa per compensare differenze del motore.

Serviranno baseline675 identita precedenti,36nuovi test MT5A e due suite consecutive711/711,
zero Failed/Skipped, source freeze/TRX/hash e verifiche workerMT3E/auditMT4C/JWT/gate/bootstrap.
Questi risultati non sono anticipati dal verde SQL di fase A. Rollout invariato/chiuso,
MT5A resta PARZIALE e MT5B non iniziato. git diff --check superato.

**MT-4C RESTORED ON ISOLATED MAMP INSTANCE — READY FOR TEST VALIDATION**
