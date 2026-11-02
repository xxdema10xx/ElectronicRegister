# Correzione CI MySQL 8.4.13 — verifica ancora BLOCKED

Analisi del run [41504155288](https://github.com/xxdema10xx/ElectronicRegister/actions/runs/41504155288), commit `981c63f7d15e25174fd0fcdbb7077a4032c2d1c1`. Nessun commit, push, deployment o accesso a EC2/staging. La release `er-quality-1` e le sue prove non sono state modificate.

## Runner SQL

Il delimiter precedente era `$$` seguito da 32 cifre: 34 byte. Il client conserva al massimo 15 byte e tronca silenziosamente il parametro. Il resto della riga può rimanere nel buffer SQL e impedire il riconoscimento del successivo comando/marker. Un CREATE può quindi essere già commesso senza conferma al chiamante. Il comportamento è documentabile nel [sorgente esatto mysql-8.4.13, `delimiter[16]` e `com_delimiter`](https://github.com/mysql/mysql-server/blob/mysql-8.4.13/client/mysql.cc). Questa è una diagnosi dal codice; la riproduzione sul container locale è BLOCKED.

La correzione usa un delimiter casuale di 13 byte, assente dal testo SQL. Stdout e stderr vengono drenati contemporaneamente. Un'unica scadenza monotona copre anche scrittura/flush su stdin; EOF, SQL error e timeout rendono inutilizzabile la connessione. La chiusura termina e, se necessario, uccide il processo, attende i reader e chiude le pipe. Non vengono ripetuti DDL o riaperta la manutenzione.

La diagnostica contiene PID, stato/exit code del client, EOF delle pipe, tempo trascorso, numero di righe stdout, hash/verbo della query, stderr recente e una PROCESSLIST senza testo SQL, ottenuta con una connessione osservatrice limitata a cinque secondi. Un client vivo con sessione Sleep e nessuna query attiva si distingue così da una sessione Query con stato/attesa sul server. Le fasi runtime registrano TIMEOUT separatamente dagli errori applicativi e conservano l'output parziale dei sottoprocessi.

`tools/mysql84_protocol.py`, obbligatorio nei job fresh/upgrade, mantiene aperto stdin per riprodurre il vecchio blocco e verifica separatamente che CREATE sia già commesso; non lo ripete. Verifica poi SELECT multilinea, CREATE DATABASE, trigger con BEGIN/END e DML reale, routine, errore SQL e timeout con SELECT SLEEP. I test offline usano processi e pipe reali per successo, stderr pieno, errore, EOF e timeout.

## Configurazione delle fixture

La fixture Testcontainers precedente selezionava `testdb` e una porta casuale, mentre RuntimePolicy richiede `electronic_register_test`, loopback e la porta LocalTest dichiarata. I tre launcher HTTP ora comunicano la porta effettiva. La policy di produzione è invariata. Quattro nuove regressioni senza database verificano porta storica/effimera, rifiuto di 3306 e `testdb`, e impossibilità di autorizzare il ramo 8.4 attraverso una connection string.

La modalità identity-only 8.4 richiede `ER_TEST_MYSQL84=1`, assenza di una connessione esterna e un container creato dal processo con il digest già fissato. Il bundle della nuova release viene installato dal client mysql del container: niente EnsureCreated come sostituto dei trigger/routine. L'identità registrata include UUID, versione esatta, hostname, porta interna, datadir e database; viene ricontrollata prima dell'accesso IAM/onboarding. Il percorso storico conserva le asserzioni MySQL 5.7.24, hostname, porta e datadir originarie. Gli audit 8.4 vengono conservati sino alla distruzione del container, senza ampliare le eccezioni DELETE dei trigger storici.

IAM, onboarding, email, import, piani, calendario e tenant dipendono dall'inizializzazione comune. Non sono state attribuite loro regressioni applicative sulla base dei soli errori di avvio. Le loro esecuzioni con la patch sono tutte BLOCKED in questo ambiente.

Restano da verificare le dipendenze storiche emerse dalla ricerca: Mt5bSecurityStateTests, Mt5bJwtTests, Mt5dMembershipMutationTests e MembershipFixturePurge mantengono vincoli 5.7. In particolare i casi Mt2IdentityTests che richiedono cancellazione fisica membership/user invocano quel purge anche nel corpo del test: conservare i dati nel solo teardown non risolve questi casi. Non sono stati cambiati i risultati attesi o i controlli SQL per farli passare. La compatibilità completa di quei casi rimane aperta e richiede esecuzione/soluzione dedicata; nessuna certificazione dell'intera suite .NET è dichiarata.

## Orchestrazione e tempi

Discovery non richiede Docker: anche se Docker manca, tutti i metodi scoperti rimangono nel report. Ogni famiglia funzionale/identity ha un preflight di versione, connessione, schema (`er_schema_ready`) e avvio del vero host. Un prerequisito fallito viene eseguito una volta e blocca solo i dipendenti; le altre famiglie continuano. Nessuna euristica sul messaggio di un'asserzione trasforma un difetto applicativo in errore infrastrutturale.

Il riuso è consentito solo a una lista esplicita di metodi rivisti, con mapping di ogni caso theory dal TRX corrente. Metodi nuovi, concorrenza, rollback, distruzione e fault injection restano individuali. Risultati incompleti o parziali dopo timeout non attestano PASS. Le regressioni verificano blocco condiviso, prosecuzione indipendente, conteggi, isolamento e batch theory incompleti.

| Ambito | Limite | Motivo |
|---|---:|---|
| Preflight | 240 s | Avvio container, installazione release, host reale |
| Metodo ordinario | 300 s | Integrazione HTTP/SQL completa |
| Concorrenza/recupero/fault | 900 s | Barriere, lock e recupero intenzionali |
| Batch esplicito | 300 s per metodo, massimo 1800 s | Inizializzazione condivisa, casi theory conservati |
| Suite funzionale | 3600 s | Budget totale; metodi non avviati BLOCKED |
| Protocollo reale | 240 s | Include riproduzione del vecchio blocco e timeout intenzionale |
| Installazione/upgrade | 1500 s | DDL, snapshot e recuperi reali, maggiore del vecchio default 1200 s |
| Step integrazione / job | 65 / 75 min | Margine per setup, rapporto e upload |

Il runner diagnostico termina l'albero processi al timeout e conserva stdout/stderr; non usa il timeout del job come primo meccanismo di arresto.

## Esiti locali

| Verifica | Esito |
|---|---|
| Build .NET Release | PASS, 0 errori; 10 warning nullable preesistenti |
| Unit test .NET | PASS, 598/598, zero skipped |
| Nuove regressioni configurazione fixture | PASS, 4/4, zero skipped |
| Regressioni esistenti portabilità del target locale | PASS, 7/7, zero skipped |
| Discovery domini richiesti | PASS, 351 metodi distinti; runtime BLOCKED per tutti |
| Node: release, manifest e controlli negativi, adapter e remediation | PASS offline; nessuna certificazione runtime |
| Regressioni Python nuove/esistenti | BLOCKED: Python non disponibile; download portatile non accessibile dalla rete |
| Actionlint sulla patch | BLOCKED: eseguibile non disponibile |
| Container effimero MySQL 8.4.13 | BLOCKED: Docker non disponibile e WSL nega l'accesso |
| Fresh, upgrade, trigger/routine, recupero DROP/CREATE, sette suite HTTP | BLOCKED: dipendono dal container |

Il JSON affiancato conserva un record BLOCKED per ciascuno dei 351 metodi scoperti. Gli esiti del vecchio run non sono stati riutilizzati come prove della patch. I log/risultati locali sono in `.diagnostics/local/` (non pubblicati). Il manifest della sola nuova release è rigenerato e verificato dopo le modifiche.

**Stato: NON READY.** Le correzioni non sono una prova di superamento runtime. Nessuna regressione applicativa effettiva è stata riprodotta localmente; le incompatibilità storiche ancora aperte sono indicate sopra.
