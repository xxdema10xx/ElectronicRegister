# MT-5B-R3 — prima applicazione controllata: arresto al preflight

21 ottobre 2026. **Installazione bloccata prima della prima scrittura SQL.**
Eseguito una sola volta il SELECT di `candidate-r1/000_preflight.sql` sulla
13306: sei regole zero, `principal_contract=1`. Il driver ha interrotto la fase
come previsto. **001–005 non eseguiti; nessuna DDL, DML, fixture o restore.**
Entrambe le istanze conservano integralmente MT4C.

## Target e prerequisiti effettivamente verificati

| Voce | Baseline, sola lettura | Isolata, unico target autorizzato |
| --- | --- | --- |
| Host SQL | 127.0.0.1 | 127.0.0.1 |
| Porta | 3306 | 13306 |
| PID / listener | 9408 / 0.0.0.0:3306 | 2156 / 127.0.0.1:13306 |
| Eseguibile | C:\MAMP\bin\mysql\bin\mysqld.exe | Stesso binario |
| VERSION() / hostname | 5.7.24 / CES01622 | 5.7.24 / CES01622 |
| Datadir | C:\MAMP\db\mysql\ | `<repo>/migrations/mt4/private/mt5a-mamp-13306/data/` |
| DATABASE() | electronic_register_test | electronic_register_test |
| Charset / collation schema | utf8mb4 / utf8mb4_unicode_ci | Identici |
| Tabelle / trigger / FK / indici | 32 / 53 / 98 / 139 | 32 / 53 / 98 / 139 |
| Membership / sidecar / trigger mt5b_* | 0 / assente / 0 | 0 / assente / 0 |

Verifica correlata di processo, percorso eseguibile, listener e identità SQL;
nessun fallback di porta/database. Confrontati dati e definizioni completi dei
32 oggetti e tutti i 53 trigger mediante information_schema e SHOW CREATE,
inclusi SQL_MODE, ACTION_ORDER, DEFINER, charset/collation. Identità semantica
iniziale con il checkpoint R2F confermata, senza nuove inizializzazioni o restore.

Nessun processo dotnet/testhost rilevato; sulla 13306 nessun altro client in
PROCESSLIST, zero INNODB_TRX e zero eventi. Controlli ripetuti prima del backup,
prima di 000 e alla chiusura. Non necessario sospendere writer; nessun kill,
cambio account o GLOBAL read_only. Le guardie C# di MembershipFixturePurge e
Mt5bSecurityStateTests richiedono 13306 e datadir isolato: la 3306 non le soddisfa.
Nessun teardown eseguito su nessuna istanza.

## Revisione R1 e hash

Consultati report R2F, report di ricostruzione, README/manifest/verificatore e
tutti i sei candidati; sole parti pertinenti dei test security-state e del
teardown. Nessun riesame indiscriminato delle fasi storiche.

`python migrations/mt5/candidate-r1/verify_static.py`: esito positivo.
Verificati hash, manifest storico immutato, SQL canonici predecessori immutati,
11 nomi/forme trigger e assenza di collisioni con i 53 precedenti.

| SQL R1 | SHA256 verificato |
| --- | --- |
| 000_preflight.sql | 98E3B424847C4C1408E4F4DC402ABDC1E318E4707CAA51DA6978D241B01753DF |
| 001_security_state_expand.sql | F7EA78B7A046FD45A98C4CE7AE50274F495B6A7F20A8DF256BDDEDADAE7383B0 |
| 002_security_state_backfill.sql | 471D37F679C9B5B323D70681CA02FCE12CDBB0EEACAA3EC6FCB35CE2E6E8B35A |
| 003_security_state_validate.sql | E4B86BB9822E0A722002C1DE6C30E8DB9DD483DF22C0F4C19E52F031F14B556C |
| 004_membership_delete_error_contract.sql | 264BA4652F0380D80AA5F14EAC29CF51C80CA72C1992CA71C0F33C43290E04A7 |
| 005_delete_contract_validate.sql | 092539C2C212CAF293C3DCA1AC5471F96AF38985732545F99D8DFAC6643776C7 |

Esaminati manualmente BIGINT signed, FK composita RESTRICT, identità immutabili,
incrementi atomici limitati a MAX BIGINT con ROW_COUNT obbligatorio, missing-state,
backfill anti-join, retention e triplice guardia root/ambiente/owner/codice sintetico.
Il codice holding reale è escluso. I trigger ruolo non aggiornano il parent;
004 usa PRECEDES rispetto alla guardia core. Nessuna modifica User.Role, nessun
evento IAM storico, DROP di oggetti esistenti o SET GLOBAL nei candidati.

Questa revisione è statica: **non certifica** compilazione/esecuzione di trigger,
PREPARE/DO, SIGNAL, INSERT SELECT, DELETE JOIN o assenza di errori 1442.
Il controllo positivo non ha individuato preventivamente il nome indice errato;
il confronto con il server reale nel preflight ha fatto emergere il difetto.

## Driver privato e backup preventivo

Area nuova privata, esclusa da Git:
`migrations/mt4/private/mt5a-mamp-13306/r3-20261021/`.
ACL verificate: accesso solo a SYSTEM, Administrators, proprietario workspace
e account esecutore sandbox. Spazio iniziale disponibile 292.605.886.464 byte;
dopo il dump 292.504.907.776 byte. Nessuna credenziale pubblicata o inserita
nel driver/repository: autenticazione da materiale privato preesistente,
password passata soltanto nell'ambiente del processo client figlio.

`apply_r3.py` usa sorgente assoluta canonica candidate-r1, hash controllati
immediatamente prima di ogni fase, endpoint TCP esplicito 13306 e confronto
datadir assoluto/processo/listener. Client MAMP 5.7.24, stdin con DELIMITER,
USE esplicito e --one-database, nessun --force. Il vecchio runner pubblico non
è stato invocato o modificato. Output privati e marker creati con esclusione
di sovrascrittura; il marker di applicazione presente vieta il replay automatico.
Ogni validator deve restituire esattamente il numero atteso di righe, tutte zero.

Ricalcolati con successo i tre hash del bundle R2F: dump finale, metadati completi
e overlay SQL_MODE. Rimane necessario conservarli insieme: il dump grezzo MAMP
omette NO_AUTO_CREATE_USER nei cinque trigger MT4C noti.

Acquisito inoltre un nuovo dump completo del solo database test sulla 13306,
prima di 000: exit code 0, footer presente, 32 CREATE TABLE e 53 trigger,
162.090 byte. DDL confrontate con il server; tutte le istruzioni INSERT/tuple
coincidono con il dump R2F verificato. Snapshot SQL di tutti i dati invariati.

| Componente privato R3 | SHA256 |
| --- | --- |
| pre-mt5b.sql | 26F40C13F811F7451D382EDE31F385A36EABC28BB3D62459EE4CD4F0C332CAEE |
| isolated-triggers-before.json | A7623D91BD56CAA1AFB0BEE5A88218496401CC5E73D04CFD8B58232DA6AB551E |
| pre-mt5b.sql.metadata-overlay.sql | 96C1FAA479A3AA8E9B87F26ABFD3AC3E7A329F82C8C4B192CCFB713C5F02C993 |
| pre-mt5b.sql-bundle.json | 2B39058B45B67A9983330BBCAA075FBF3B4F9D3B68C2C0484C0EED8686D1EA6A |

Il bundle conserva tutte le definizioni complete e il contesto/ordine di creazione;
l'overlay è esclusivamente materiale archivistico per un eventuale recupero
separatamente autorizzato, **mai eseguito**. Hash e corrispondenza dei metadati
con lo stato SQL finale riverificati alla chiusura, con evidenza
`backup-integrity-at-close.json`. Nessun restore automatico e nessun backup
post-MT5B: MT5B non è stato installato.

## Esito reale 000–005 e causa dimostrata

Output integrale di 000:

```text
target_session           0
baseline_objects         0
principal_contract       1
retained_trigger_safety  0
membership_integrity     0
holding_provisioning     0
installation_boundary    0
```

Client exit code 0, stderr vuoto: non è un errore di connessione o di sintassi.
Il driver ha correttamente rifiutato il risultato nonzero del validator.

Diagnosi SQL mirata sui singoli requisiti: PK membership `(id)` e ruoli
`(membership_id,role)` corretti; le tre identità CHAR(36) ASCII NOT NULL corrette;
tutte e tre le FK attese RESTRICT/RESTRICT presenti. L'unica violazione è il
confronto nominale dell'indice owner-aware:

| Richiesto da 000 R1 | Presente nella baseline certificata |
| --- | --- |
| uq_membership_institution_id | AK_institution_membership_institution_id_id |
| UNIQUE (institution_id,id) | UNIQUE (institution_id,id), stesso ordine |

Il nome differisce; l'invariante strutturale esiste. Non è un motivo per rinominare
l'indice o modificare lo schema predecessore. Evidenza privata:
`principal-index-diagnosis.json` e snapshot completi prima/dopo.

| Fase | Esecuzioni / esito |
| --- | --- |
| 000 | Una, solo lettura; 6/7 zero, bloccata |
| 001 EXPAND | Zero; sidecar e dieci trigger non creati |
| 002 BACKFILL | Zero |
| 003 VALIDATE | Zero |
| 004 DELETE CONTRACT | Zero; undicesimo trigger non creato |
| 005 VALIDATE | Zero |

Non esiste stato DDL parziale da recuperare. Nessun retry, reset, workaround o
tentativo di forzare 000. PREPARE/EXECUTE dei file successivi non raggiunti.

## Revisione candidata separata, non applicata

Preparata [candidate-r2](../../migrations/mt5/candidate-r2/README.md), senza
alterare R1 o il manifest storico. Non confondere questa revisione dei file
con il precedente checkpoint infrastrutturale MT-5B-R2.

Unica correzione funzionale in 000: eliminare l'assunzione sul nome dell'indice,
mantenendo NON_UNIQUE=0, raggruppamento per singolo indice ed esatta sequenza
completa `institution_id,id`. Nessuna guardia ambiente o ownership rimossa.
Gli altri cinque SQL sono byte-identici a R1, inclusi gli header di provenienza.
Nuovo SHA256 di 000:
`3200D67AA64DBB72B8AECAAB7739DBCC3D59040407642C9744AA04FAE314B780`.

Manifest separato `manifest-r2.json`: hash nuovi, parent R1 e hash storici.
Verificatore statico separato positivo, con controllo aggiuntivo che il solo
delta autorizzato sia questa correzione e che i cinque file restino identici.
**Nessun SQL R2 inviato al server**, incluso il suo preflight.

Serve nuova autorizzazione per installare la revisione, dopo revisione completa,
nuovo preflight e un nuovo registro di esecuzione. Non riprendere il driver R3
arrestato e non applicare automaticamente i file sostitutivi.

## Integrità finale osservata e prove rinviate

Alla chiusura rieseguiti snapshot completi, tutti i 53 SHOW CREATE e metadati,
validator MT4 e controlli su ciascuna FK, su **entrambe** le istanze:

| Verifica reale | 3306 | 13306 |
| --- | --- | --- |
| Tabelle / trigger / FK / indici | 32 / 53 / 98 / 139 | 32 / 53 / 98 / 139 |
| FK con orfani | 0 su 98 | 0 su 98 |
| Validator MT4A / MT4C | 7/7 zero; 5/5 zero | 7/7 zero; 5/5 zero |
| Dati, DDL e tutti i metadati trigger rispetto all'inizio | Identici | Identici |
| Sidecar / nuovi trigger MT5B | Assente / 0 | Assente / 0 |
| Ledger / revision / holding | Vuoto / zero / Provisioning | Identici |
| GLOBAL sql_mode | Invariato | Invariato |

Fingerprint semantico iniziale e finale comune:
`FC6DF04E520A46B4084D9007A825D40DC354CE0C231CB80A0E20133886D92F22`.
PID e identità SQL invariati. Evidenza conclusiva privata:
`blocked-final-verification.json`, snapshot e metadati `*-blocked-final`.

Prove SQL funzionali **NON ESEGUITE**, perché installazione non iniziata:
generation iniziale, ruolo INSERT/DELETE, cambio status/no-op, rollback incremento,
divieto diminuzione/delete operativo, missing-state e isolamento A/B. Nessuna
fixture creata, neppure temporaneamente. Non dichiarate Passed. Restano inoltre
da dimostrare runtime PREPARE/DO, overflow, 1442, INSERT SELECT/DELETE JOIN,
backfill non vuoto, tutte le guardie teardown, concorrenza e regressione completa.
Nessuna attestazione 33/64/99/141, nessuna dichiarazione 738 test Passed,
PERSISTENCE READY o equivalenza agli originali perduti.

## Git e arresto

Branch `refactor-database-entities`, HEAD invariato
`92cb8141b9555a36c00e517cb4cddc50d0dd7485`.
Questo lavoro aggiunge il report e `candidate-r2/`; estende il .gitignore
esclusivamente ai suoi sei SQL. Verificati git check-ignore e git diff --check:
candidati visibili a Git, materiale privato/backup/credenziali ancora ignorato.
Restano le modifiche locali preesistenti e i report precedenti, senza rimozioni.
R1, SQL canonici e manifest storico preservati; runner e codice applicativo
invariati. Nessun commit/push, test .NET, JWT o intervento XAMPP.

**MT-5B-R3 SQL APPLICATION BLOCKED**
