# MT-5B-R3B — revisione candidate-r2: bloccata prima dell'esecuzione

21 ottobre 2026. **Nessuna migrazione eseguita, nessuna DDL o DML.**
La revisione obbligatoria ha individuato un ulteriore difetto nel controllo
owner-aware di 000: R2 non esclude gli indici UNIQUE su prefissi di colonna.
Applicata la condizione di arresto richiesta; preparata candidate-r3 separata,
non applicata. Lo schema MT4C non richiede modifiche.

## Identità e preflight reale

| Verifica | Baseline protetta | Istanza isolata |
| --- | --- | --- |
| Connessione TCP | 127.0.0.1:3306 | 127.0.0.1:13306 |
| PID / listener | 9408 / 0.0.0.0:3306 | 2156 / 127.0.0.1:13306 |
| Eseguibile | C:\MAMP\bin\mysql\bin\mysqld.exe | Stesso binario MAMP |
| VERSION() / hostname | 5.7.24 / CES01622 | 5.7.24 / CES01622 |
| Datadir SQL | C:/MAMP/db/mysql/ | `<repo>/migrations/mt4/private/mt5a-mamp-13306/data/` |
| DATABASE() | electronic_register_test | electronic_register_test |
| Charset / collation | utf8mb4 / utf8mb4_unicode_ci | Identici |
| Tabelle / trigger / FK / indici | 32 / 53 / 98 / 139 | 32 / 53 / 98 / 139 |
| Sidecar / trigger mt5b_* | Assente / 0 | Assente / 0 |

Identità verificate correlando processo, percorso eseguibile, listener e query
SQL. Snapshot iniziali coerenti con la chiusura R3; confrontati dati, colonne,
indici, FK, DDL e tutti i 53 SHOW CREATE TRIGGER con metadati completi.
Nessun fallback e nessuna connessione a XAMPP. Sulla 13306 zero altri client,
zero transazioni INNODB_TRX e zero eventi, prima e dopo le verifiche.
Non sospesi o terminati processi, non modificati account o configurazioni.

Su entrambe le istanze: validator MT4A 7/7 zero, MT4C 5/5 zero e controllo
individuale delle 98 FK, comprese le composite, senza orfani. Ledger vuoto,
revision zero, holding reale Provisioning. Nessuna membership preesistente.

## Revisione R2, delta R1 e SHA256

Consultati soltanto il report R3, README/manifest e sei SQL R2, il delta rispetto
ai sei SQL R1 e le evidenze/helper privati necessari per catturare lo stato SQL.
Nessuna nuova analisi storica MT1–MT5A.

Verificati il manifest R2, il relativo riferimento SHA256 al manifest R1 e tutti
i sei hash. Rispetto a R1 cambia soltanto 000: intestazione di provenienza e
rimozione del confronto nominale con `uq_membership_institution_id`.
Gli altri cinque file sono byte-identici. Nessun SQL R2 contiene ulteriori
riferimenti al vecchio nome indice.

| File R2 | SHA256 |
| --- | --- |
| 000_preflight.sql | 3200D67AA64DBB72B8AECAAB7739DBCC3D59040407642C9744AA04FAE314B780 |
| 001_security_state_expand.sql | F7EA78B7A046FD45A98C4CE7AE50274F495B6A7F20A8DF256BDDEDADAE7383B0 |
| 002_security_state_backfill.sql | 471D37F679C9B5B323D70681CA02FCE12CDBB0EEACAA3EC6FCB35CE2E6E8B35A |
| 003_security_state_validate.sql | E4B86BB9822E0A722002C1DE6C30E8DB9DD483DF22C0F4C19E52F031F14B556C |
| 004_membership_delete_error_contract.sql | 264BA4652F0380D80AA5F14EAC29CF51C80CA72C1992CA71C0F33C43290E04A7 |
| 005_delete_contract_validate.sql | 092539C2C212CAF293C3DCA1AC5471F96AF38985732545F99D8DFAC6643776C7 |

La FK candidata `fk_membership_security_owner` riferisce le colonne
`institution_membership(institution_id,id)`, non il nome di un indice.
Sul server esiste effettivamente `AK_institution_membership_institution_id_id`,
UNIQUE sulle due colonne complete, nell'ordine corretto; SUB_PART è NULL.
Acquisite anche le dipendenze composite già esistenti sulla membership.
Quindi la baseline reale è idonea: il difetto riguarda la capacità del preflight
di rifiutare uno schema diverso, non l'integrità dello schema attuale.

## Difetto dimostrato e arresto

R2 usa NON_UNIQUE=0 e raggruppamento per singolo indice, poi:

```sql
HAVING GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX)='institution_id,id'
```

La lista dei nomi non distingue colonne complete da prefissi: anche un indice
UNIQUE `(institution_id(8),id(8))` restituisce quella lista. Mancando il controllo
SUB_PART, il requisito esplicito di escludere indici parziali non è soddisfatto.
Un indice su prefissi non è un supporto valido per questa FK InnoDB.
Non occorre creare tabelle o tentare DDL per dimostrare il difetto.

Eseguito sulla sola 13306 un SELECT su righe costanti derivate che rappresentano
metadati di indici, senza oggetti temporanei, fixture o modifica dello schema:

| Metadati simulati, indice UNIQUE | Predicato R2 accetta | Predicato corretto accetta |
| --- | ---: | ---: |
| Due colonne complete, ordine corretto | 1 | 1 |
| Due colonne su prefissi | **1** | **0** |
| Tre colonne, ultima aggiuntiva | 0 | 0 |
| Due colonne in ordine invertito | 0 | 0 |

Evidenza privata `predicate-counterexample.json`, con query e risultati reali.
Questa è una verifica dell'espressione di preflight; non è una prova funzionale
MT5B e non equivale all'esecuzione di un file candidato.

Esaminati inoltre PREPARE/EXECUTE/DEALLOCATE, DELIMITER, SIGNAL/MYSQL_ERRNO e
CREATE TRIGGER: nessuna ulteriore dipendenza dal nome indice precedente.
**Non certificata la sintassi a runtime**: l'arresto imposto durante revisione
precede qualsiasi esecuzione dei file, compilazione trigger o gate PREPARE/DO.
Non introdotte scorciatoie su ownership, retention, generation o teardown.

## Registro privato e backup

Nuova directory, distinta da R3 e senza sovrascrivere marker precedenti:
`migrations/mt4/private/mt5a-mamp-13306/r3b-20261021-review/`.
Contiene sorgenti/hash verificati, delta R1/R2, marker review, query di diagnosi,
snapshot iniziali/finali, tutti i metadati trigger e risultati validator.
Il verificatore privato è esclusivamente in lettura: nessun punto di ingresso
per installare SQL, nessuna ripresa del journal R3 e nessun driver mutante avviato.

ACL private verificate: SYSTEM, Administrators, proprietario workspace e account
esecutore sandbox; directory esclusa da Git. Autenticazione privata preesistente,
nessuna password in script, output o report. Client SQL MAMP; niente --force.

Verificati nuovamente tutti i componenti del bundle R2F: dump, metadati completi
e overlay SQL_MODE. Verificato anche il bundle preventivo R3:

| Backup privato già disponibile | SHA256 verificato |
| --- | --- |
| R2F r2f-final-mt4c.sql | CB24A5A028E2EF566A37AFFB9603656F4118F4DEDE71F33D40FF78D87B400EE1 |
| R3 pre-mt5b.sql | 26F40C13F811F7451D382EDE31F385A36EABC28BB3D62459EE4CD4F0C332CAEE |
| R3 pre-mt5b.sql.metadata-overlay.sql | 96C1FAA479A3AA8E9B87F26ABFD3AC3E7A329F82C8C4B192CCFB713C5F02C993 |

R3: acquisizione originaria con exit code 0, footer, 162.090 byte, 32 tabelle e
53 trigger, dati verificati nel checkpoint precedente. Nel presente checkpoint
ricalcolati gli hash e confrontati i metadati completi del bundle con la nuova
cattura SQL reale: identici. Dati SQL correnti identici allo snapshot R3 verificato.
Conservare dump e contesto/overlay insieme: il solo dump non riproduce SQL_MODE
dei cinque trigger MT4C. Nessun overlay o restore eseguito.
Non acquisito un nuovo dump: nessuna DDL raggiunta e backup verificati ancora
corrispondenti allo stato attuale. Nessun backup di database operativi.

## Esito effettivo delle sei fasi

| File candidate-r2 | Esecuzioni in R3B | Esito |
| --- | ---: | --- |
| 000 | 0 | Fermato durante revisione sorgenti |
| 001 | 0 | Nessuna sidecar o trigger core creati |
| 002 | 0 | Nessun backfill |
| 003 | 0 | Validator MT5B non eseguito |
| 004 | 0 | Nessun trigger aggiunto |
| 005 | 0 | Validator MT5B non eseguito |

Nessun errore server o stato DDL parziale; nessun retry, riparazione o replay.
Non dichiarati risultati 7/6/2 zero per questi file, né schema 33/64/99/141.

## Candidate-r3 separata, non applicata

Preparato [pacchetto candidate-r3](../../migrations/mt5/candidate-r3/README.md)
con manifest separato e verificatore statico. Unica correzione funzionale in 000:

```sql
HAVING COUNT(*)=2 AND SUM(SUB_PART IS NOT NULL)=0
  AND GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX)='institution_id,id'
```

Rimangono NON_UNIQUE=0, raggruppamento per indice e ordine esatto; il conteggio
esplicito esclude colonne aggiuntive anche se GROUP_CONCAT fosse troncato.
Gli altri cinque SQL sono byte-identici a R2/R1. SHA256 del nuovo 000:
`FB1CD8F92B369373AC5DC86B0CC8965EAB9DD7B0DCDB65155A8A5869595BCCCF`.
`manifest-r3.json` conserva riferimenti/hash parent R2 e provenienza storica;
R1, R2 e manifest storico non modificati.

Verificatore statico R3 positivo: hash e delta esatto, 11 nomi/forme trigger,
zero collisioni con i 53 canonici, SQL canonici immutati. Non è una certificazione
di esecuzione SQL. Nessun file R3 inviato al server. Serve nuova autorizzazione
per una futura installazione con registro distinto e preflight completo.

## Stato finale e prove ancora necessarie

Snapshot iniziali/finali, SHOW CREATE e tutti i metadati dei trigger risultano
identici su ciascuna istanza; anche i timestamp di creazione sono invariati.
Entrambe mantengono 32 tabelle, 53 trigger, 98 FK, 139 indici, MT4A 7/7 e MT4C
5/5 zero, tutte le 98 FK senza orfani. Dati delle 32 tabelle e definizioni
integralmente preservati, baseline 3306 invariata. GLOBAL sql_mode invariato su
entrambi i server. Fingerprint semantico comune:
`FC6DF04E520A46B4084D9007A825D40DC354CE0C231CB80A0E20133886D92F22`.
Evidenza privata conclusiva `review-final.json`.

**NON ESEGUITE**: generation iniziale/monotona, ruoli INSERT/DELETE/rekey,
status/no-op, rollback, overflow, missing-state, errore 1442, INSERT SELECT,
DELETE JOIN, backfill non vuoto/senza reset, concorrenza, teardown e sue guardie,
holding esclusa, retention operativa e isolamento tenant A/B. Da eseguire soltanto
dopo installazione strutturale autorizzata. Nessuna fixture o teardown avviato.
Nessuna suite .NET, stress test, HTTP, JWT, IAM manager o audit amministrativo.
Nessuna dichiarazione PERSISTENCE READY o 738/738 Passed.

## Stato Git e stop

Branch `refactor-database-entities`, HEAD
`92cb8141b9555a36c00e517cb4cddc50d0dd7485`, invariati.
Modifiche di questo checkpoint: presente report, `candidate-r3/`, sei eccezioni
mirate nel .gitignore e materiale privato ignorato. Modifiche preesistenti
preservate. Verificati git diff --check e git check-ignore: sei SQL candidati
visibili, backup/credenziali/evidenze private esclusi.
Runner pubblico, codice applicativo e SQL MT1/MT3/MT4 invariati.
Nessun commit/push, modifica 3306, operazione XAMPP o nuova fase avviata.

**MT-5B-R3B BLOCKED — REVIEW REQUIRED**
