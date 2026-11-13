# MT-5B-R3C — candidate-r3 installata sulla sola 13306

21 ottobre 2026. **Prima installazione completata e validazione strutturale SQL
positiva.** Sei file eseguiti una sola volta; nessun errore, retry, restore o
modifica alla baseline 3306. Nessuna prova funzionale o suite .NET eseguita.

## Identità effettiva e predecessore

| Voce | Baseline protetta | Target autorizzato |
| --- | --- | --- |
| Endpoint TCP | 127.0.0.1:3306 | 127.0.0.1:13306 |
| PID / listener | 9408 / 0.0.0.0:3306 | 2156 / 127.0.0.1:13306 |
| Eseguibile | C:\MAMP\bin\mysql\bin\mysqld.exe | Identico binario MAMP |
| VERSION() / hostname | 5.7.24 / CES01622 | 5.7.24 / CES01622 |
| Datadir SQL | C:/MAMP/db/mysql/ | `<repo>/migrations/mt4/private/mt5a-mamp-13306/data/` |
| DATABASE() | electronic_register_test | electronic_register_test |
| Charset / collation schema | utf8mb4 / utf8mb4_unicode_ci | Identici |
| Inventario prima: tabelle / trigger / FK / indici | 32 / 53 / 98 / 139 | Identico |

Processo, percorso eseguibile e listener correlati all'identità SQL prima del
lavoro e prima di ciascun file, poi alla validazione e al backup finale.
Nessun fallback. Snapshot iniziali completi coincidenti con R2F: dati, colonne,
indici, FK, DDL, trigger e relativo contesto di esecuzione. Su entrambi MT4A
7/7 zero, MT4C 5/5 zero e tutte le 98 FK senza orfani. Sidecar assente e zero
trigger mt5b_ prima della prima DDL; membership preesistenti zero.

Nessun altro client/writer, nessuna transazione INNODB_TRX e nessun evento sulla
13306; controlli ripetuti prima di ciascun file e backup. Nessun processo fermato,
vincolo disabilitato o account modificato. I PID sono rimasti invariati.

## Revisione sorgenti e preflight

Consultati report R3B, README/manifest R3, tutti i sei SQL e le evidenze private
necessarie; nessuna nuova ricostruzione o analisi storica indiscriminata.
Catena manifest R1 → R2 → R3 verificata con SHA256 e confronto byte:

- R1 → R2: solo 000, rimossa l'assunzione sul nome dell'indice owner-aware.
- R2 → R3: solo 000, aggiunti COUNT(*)=2 e rifiuto SUB_PART non NULL.
- 001–005 byte-identici nelle tre revisioni; manifest e revisioni precedenti immutati.

| File candidate-r3 | SHA256 verificato prima dell'esecuzione |
| --- | --- |
| 000_preflight.sql | FB1CD8F92B369373AC5DC86B0CC8965EAB9DD7B0DCDB65155A8A5869595BCCCF |
| 001_security_state_expand.sql | F7EA78B7A046FD45A98C4CE7AE50274F495B6A7F20A8DF256BDDEDADAE7383B0 |
| 002_security_state_backfill.sql | 471D37F679C9B5B323D70681CA02FCE12CDBB0EEACAA3EC6FCB35CE2E6E8B35A |
| 003_security_state_validate.sql | E4B86BB9822E0A722002C1DE6C30E8DB9DD483DF22C0F4C19E52F031F14B556C |
| 004_membership_delete_error_contract.sql | 264BA4652F0380D80AA5F14EAC29CF51C80CA72C1992CA71C0F33C43290E04A7 |
| 005_delete_contract_validate.sql | 092539C2C212CAF293C3DCA1AC5471F96AF38985732545F99D8DFAC6643776C7 |

Verificatore statico R3 positivo; SQL canonici e manifest storico preservati.
Nessun riferimento al vecchio nome indice in 001–005. La FK riferisce colonne,
non nomi indice. Sul server reale STATISTICS mostra l'indice UNIQUE
`AK_institution_membership_institution_id_id`, SEQ_IN_INDEX 1/2, colonne
institution_id/id complete e SUB_PART NULL.

Il predicato completo WHERE/GROUP BY/HAVING è stato valutato con SELECT su
metadati derivati, sul server 5.7.24: accettato il caso valido; rifiutati indice
non UNIQUE, ordine invertito, colonna aggiuntiva, prefissi, tabella diversa,
colonna id mancante e colonne errate. Otto casi conformi, nessuna DDL o fixture
usata per questa verifica. Evidenze `preflight-predicate-cases.json` e
`statistics-real-columns.json` nell'area privata.

Revisionati PREPARE/EXECUTE, DELIMITER, SIGNAL/MYSQL_ERRNO, CREATE TRIGGER,
dipendenze FK, limiti BIGINT e guardie di teardown. I gate PREPARE/DO di
001/002/004 sono stati effettivamente eseguiti senza errori; tutti i CREATE
TRIGGER sono stati accettati dal server MySQL 5.7.24. L'esecuzione dei rami
SIGNAL e delle mutazioni protette resta da provare nel checkpoint funzionale.

## Registro privato e backup

Directory nuova: `migrations/mt4/private/mt5a-mamp-13306/r3c-20261021/`.
ACL private verificate: SYSTEM, Administrators, proprietario workspace e account
esecutore sandbox. Directory ignorata da Git; nessuna credenziale nei sorgenti,
log pubblici o report. Autenticazione da materiale privato preesistente.

Driver privato `apply_r3c.py`, vincolato al percorso canonico candidate-r3 e alla
sola 13306. Client MAMP 5.7.24, stdin con DELIMITER, USE esplicito e filtro
--one-database, nessun --force. Hash verificati prima di ciascun file; marker e
output creati senza sovrascrittura, replay automatico rifiutato. Zero obbligatorio
su tutte le righe di ogni validator. Journal R3/R3B preservati; runner pubblico
non invocato o modificato. DDL a commit implicito, nessun rollback globale promesso.

Ricalcolati hash dei bundle R2F e R3: dump, definizioni/metadati completi e overlay
SQL_MODE integri. Confrontati metadati R3 con il server corrente: identici.
Acquisito anche un nuovo dump preventivo R3C della sola 13306: exit code 0,
footer presente, 32 tabelle/53 trigger, 162.090 byte; DDL confrontate col server
e tutte le tuple INSERT coincidenti con il backup R3 verificato.
SHA256 del nuovo dump:
`95ACC8A857BBF424C80295DE463E78ED258D8DC3A6D211B5CEA2F3C633D9FFD1`;
manifest in `pre-mt5b-manifest.json`.

Il backup completo comprende sempre dump **più** metadati e overlay di contesto:
il dump grezzo 5.7 non riproduce da solo SQL_MODE dei cinque trigger MT4C.
Gli overlay sono esclusivamente archivistici e non sono stati eseguiti.

## Esito reale dei sei SQL

| File | Esecuzioni | Esito osservato |
| --- | ---: | --- |
| 000 | 1 | Esattamente 7 regole zero; 001 avviato soltanto dopo conferma |
| 001 | 1 | Una sidecar e 10 trigger core; inventario 33/63/99/141 |
| 002 | 1 | Backfill completato senza errore; zero righe da inizializzare |
| 003 | 1 | Esattamente 6 regole zero |
| 004 | 1 | Undicesimo trigger additivo; inventario 33/64/99/141 |
| 005 | 1 | Esattamente 2 regole zero, ACTION_ORDER 1/2 confermato |

Tutti gli exit code sono zero, nessun errore di migrazione. Output per fase e
marker conservati privatamente. Nessun DROP, reset, sostituzione di trigger,
riesecuzione EXPAND, retry, restore o modifica di GLOBAL sql_mode.

| Validator | Regole con valore zero |
| --- | --- |
| 000 | target_session, baseline_objects, principal_contract, retained_trigger_safety, membership_integrity, holding_provisioning, installation_boundary |
| 003 | missing_state, orphan_state, invalid_generation, sidecar_contract, core_trigger_contract, membership_dependencies |
| 005 | delete_contract_presence, delete_contract_order |

003 e 005 ripetuti in sola lettura alla validazione finale, ancora tutti zero.
Nessun file mutante ripetuto.

## Struttura e definizioni finali verificate sul server

| Controllo SQL reale sulla 13306 | Risultato |
| --- | --- |
| Tabelle / trigger / FK / indici | **33 / 64 / 99 / 141** |
| Orfani su tutte le FK, incluse composite | **0 su 99** |
| MT4A / MT4C | **7/7 zero / 5/5 zero** |
| MT5B 003 / 005 | **6/6 zero / 2/2 zero** |
| Membership senza stato / stati orfani / generation invalide | 0 / 0 / 0 |
| Dati e DDL delle 32 tabelle precedenti | Identici allo snapshot iniziale |
| Tutti i 53 trigger precedenti | Identici, inclusi corpo, ordine, contesto e timestamp |
| GLOBAL sql_mode | Invariato su entrambe le istanze |

Sidecar InnoDB, utf8mb4_unicode_ci, con due CHAR(36) ASCII ascii_general_ci NOT
NULL; PK `(institution_id,membership_id)`, UNIQUE globale `(membership_id)`;
FK composita `fk_membership_security_owner` verso `(institution_id,id)`, entrambe
le azioni RESTRICT. `security_generation` è BIGINT(20) signed NOT NULL DEFAULT 0,
senza unsigned o espressioni aggiuntive. Vincoli e metadati acquisiti realmente.

Per tutti gli 11 nuovi trigger confrontati corpi completi, timing/evento/tabella
con i candidati, mediante information_schema e SHOW CREATE; confronto che
preserva stringhe SQL e ignora soltanto commenti/spazi non eseguibili.
DEFINER root@localhost, SQL_MODE uguale al contesto verificato, charset utf8mb4,
collation_connection utf8mb4_general_ci, database_collation utf8mb4_unicode_ci.

| Trigger mt5b_* | Timing/evento | Contratto nel corpo installato |
| --- | --- | --- |
| security_insert | BEFORE INSERT sidecar | Solo generation iniziale zero |
| security_update | BEFORE UPDATE sidecar | Identità immutabile, nessuna diminuzione/negatività |
| security_delete | BEFORE DELETE sidecar | Retention, sola eccezione fixture root vincolata |
| membership_security_insert | AFTER INSERT membership | Stato iniziale nella stessa transazione |
| membership_identity_update | BEFORE UPDATE membership | Id/owner/user immutabili |
| membership_security_update | AFTER UPDATE membership | Missing-state fail-closed; incremento solo status effettivo |
| membership_security_delete | BEFORE DELETE membership | Retention; ACTION_ORDER **2** |
| role_identity_update | BEFORE UPDATE ruolo | Rekey vietato, missing-state negato |
| role_security_insert | AFTER INSERT ruolo | Incremento atomico owner-aware |
| role_security_delete | AFTER DELETE ruolo | Incremento atomico owner-aware |
| membership_delete_contract | BEFORE DELETE membership | SIGNAL 23000/1451; ACTION_ORDER **1** |

Gli altri gruppi MT5B hanno ordine 1. I tre incrementi installati usano
`security_generation=security_generation+1`, predicato
`0 <= generation < 9223372036854775807` e ROW_COUNT obbligatorio uguale a 1.
Guardie fixture conservate integralmente: root, versione/porta/database/datadir,
owner espliciti e codice sintetico; holding reale esclusa. Sono proprietà dei
corpi verificati, non dichiarazioni di prove funzionali già superate.

## Baseline, dati e backup finale

3306 confrontata prima/dopo: stesso inventario 32/53/98/139, tutte le definizioni,
metadati e dati identici, 53 SHOW CREATE completi invariati, validator MT4A/MT4C
zero e nessuna FK orfana. Nessuna scrittura sulla 3306.
Fingerprint baseline iniziale/finale:
`FC6DF04E520A46B4084D9007A825D40DC354CE0C231CB80A0E20133886D92F22`.

Fingerprint semantico finale isolata:
`D7E35C227D86A9452C11B00CBAA9D74EE3936C391497C837D8502ABFA41D7085`.
Gli unici oggetti aggiunti sono quelli MT5B autorizzati; nessun dato precedente
modificato, nessun evento IAM o cambiamento User.Role. Membership e sidecar
sono entrambe vuote. **002 su zero membership non certifica il backfill con
dati né la preservazione di generation nonzero durante un backfill reale.**

Acquisito backup finale privato `final-mt5b-candidate-r3.sql`: exit code 0,
footer verificato, 33 tabelle/64 trigger, 179.574 byte, DDL confrontate con il
server e tuple dei dati precedenti identiche al backup preventivo. SHA256:
`6AC8C75C7CA55C21B33532F5D250DF2824321B5EAC8B16BABC7B9EEECF39ED45`.
Conservati manifest, metadati completi dei 64 trigger e overlay di contesto
separato, non eseguito, in `final-mt5b-candidate-r3.sql-bundle.json`.
SHA256 dell'overlay finale:
`ED040833A0EA61DB77DFFED83F3C3A1FBEE7D00DFA80E4B9BA1A4FDAC536CA47`.
Spazio disponibile dopo il dump: 292.627.075.072 byte.

Evidenze conclusive private: `structural-validation.json`, snapshot prima/dopo,
SHOW CREATE completi, validator per fase e finali, `backup-final-integrity.json`.

## Prove ancora mancanti e Git

**Non eseguite**: generation iniziale/monotona, ruoli e status/no-op, rollback,
overflow, missing-state, retention operativa, INSERT SELECT, DELETE JOIN/1442,
backfill con dati/nonzero, concorrenza, teardown e guardie, isolamento A/B.
Compilazione/installazione dei trigger e zero violazioni su tabelle vuote non
dimostrano questi comportamenti. Nessuna fixture creata o eliminata.

Nessuna regressione .NET, stress test, JWT, API, IAM manager, audit amministrativo
o frontend. Nessuna equivalenza agli SQL originali perduti, nessuna dichiarazione
738/738 Passed o MT5B PERSISTENCE READY.

Branch `refactor-database-entities`, HEAD invariato
`92cb8141b9555a36c00e517cb4cddc50d0dd7485`. Questo checkpoint aggiunge solo il
presente report pubblico e materiale privato ignorato. Restano le modifiche
preesistenti a .gitignore, pacchetti candidati e report precedenti; nessuno
alterato per far passare la migrazione. Git diff --check e check-ignore verificati.
Nessuna modifica al runner pubblico, SQL canonici o codice applicativo;
nessun commit/push, nuova istanza o intervento XAMPP. Lavoro fermato qui.

**MT-5B-R3C SCHEMA INSTALLED — AWAITING FUNCTIONAL CERTIFICATION**
