# MT-5B: preflight Windows/XAMPP e piano minimo

Verifica del 21 ottobre 2026. Nessuna DDL, DML, fixture, restore, avvio o riavvio
di servizi eseguiti. Nessun database operativo interrogato. Nessun backup ancora
acquisito: la connessione SQL non supera l'handshake.

## Riscontri

- Branch `refactor-database-entities`, HEAD `92cb814`, workspace inizialmente pulito.
- Letti il report persistence e il riepilogo aggiornato MT5B; il checkpoint resta
  PERSISTENCE READY su MySQL 5.7.24, MT5B complessiva parziale, JWT ancora da integrare.
- SDK .NET locale 10.0.401.
- Processo mysqld PID 6524: `C:/xampp/mysql/bin/mysqld.exe`.
  Listener TCP `0.0.0.0:3306` dello stesso PID; nessun listener 13306 osservato.
  Apache XAMPP attivo. PID e listener vanno riverificati prima di agire.
- `my.ini` indica porta 3306 e datadir `C:/xampp/mysql/data`.
- Client locale: MariaDB 10.4.32. **La versione server non è ancora verificata**:
  entrambi i tentativi TCP espliciti a 3306 (127.0.0.1 e localhost), senza file
  defaults, root senza password, sul solo `electronic_register_test`, terminano con
  ERROR 2013 durante lettura del pacchetto iniziale. Nessuna query eseguita.
  L'errore non dimostra un problema di password né l'assenza del database.
- CIM e Get-NetTCPConnection negati nell'ambiente; processo e listener verificati
  con Get-Process e netstat. Non richiesti privilegi aggiuntivi.
- Ispezione dei soli nomi file in `data/electronic_register_test`: 32 `.frm`,
  inclusi institution_membership, institution_membership_role e
  course_administration_event; assente institution_membership_security_state.
  È compatibile con un predecessore MT4C, **non certifica** definizioni, FK,
  trigger, engine, dati o integrità. I file attivi non sono un backup.
- `git ls-files migrations/mt5` elenca solo README, runner e scaffold.
  Mancano tutti i sei SQL 000–005: `.gitignore:26:*.sql` li esclude.
  Il manifest finale versionato ne conserva gli SHA256 in
  `TestResults/mt5b-persistence/final-migration-manifest.json`.
- Runner, MembershipFixturePurge e Mt5bSecurityStateTests vincolano esplicitamente
  target/versione/datadir MAMP 13306. Cambiare soltanto la connection string non
  rende la suite eseguibile su XAMPP. Il runner non va invocato su questo target.

## Confronto disponibile

| Elemento | XAMPP osservato | Checkpoint certificato |
| --- | --- | --- |
| Porta | 3306 | 13306, non trasferibile automaticamente |
| Server | identità SQL non acquisita | MySQL 5.7.24 |
| Tabelle | 32 file .frm, da confermare SQL | 33 |
| Security state | nessun file omonimo | sidecar presente |
| Trigger / FK / indici | non verificabili senza SQL | 64 / 99 / 141 |
| Migrazioni MT5B | sei SQL mancanti nel checkout | 000–005 con hash nel manifest |

Non si assume che le 32 tabelle coincidano con le 32/53/98/139 MT4C certificate.
Non si ricostruiscono i trigger dai soli report o dagli hash.

## Piano minimo, in ordine

1. **Completare il preflight in sola lettura.** Diagnosticare l'handshake dalla
   sessione Windows autorizzata e dal log XAMPP, senza riavviare il server condiviso.
   Confermare via SQL VERSION/version_comment, hostname, port, datadir, DATABASE,
   CURRENT_USER, sql_mode globale/sessione; censire schema, engine, collation,
   definizioni trigger/FK/indici e stato holding. Connessione sempre esplicita al
   solo electronic_register_test, nessun fallback su database operativo.
2. **Recuperare gli originali MT5B** dal precedente workspace o altro artefatto
   affidabile, confrontando tutti i sei SHA256 con il manifest finale. Correggere
   il packaging Git con eccezioni limitate agli SQL canonici, conservando esclusi
   dump e file privati. Nessuna reimplementazione della persistence.
3. **Produrre il diff reale e il piano eseguibile.** Confrontare definizioni con
   MT4C e SQL MT5B recuperati; eseguire soltanto validator letti e confermati
   read-only. Se MT4C coincide e la sidecar manca, occorre solo MT5B; se emergono
   drift o installazioni parziali, fermarsi e descrivere il delta, senza restore
   o riesecuzione automatica di EXPAND.
4. **Backup prima di qualsiasi scrittura, inclusi i test.** Mettere in pausa i
   writer del solo ambiente test; acquisire dump completo della sola base test
   con il mysqldump MariaDB locale, single-transaction, trigger/routine/eventi e
   opzioni compatibili con quel client. Percorso privato escluso da Git; verificare
   exit code, footer, inventario oggetti e SHA256. Non copiare opzioni MySQL 8
   incompatibili. Presentare target, diff, hash backup e comandi precisi prima del
   DDL. Il presente piano non autorizza restore o modifiche al database operativo.
5. **Portabilità controllata e prova MariaDB.** Preparare un profilo test esplicito
   che riconosca il target XAMPP verificato in runner/helper/test e nelle guardie SQL
   di teardown. Conservare i controlli su identità, owner sintetici e holding esclusa;
   non sostituire semplicemente 13306 con 3306 né rimuovere guardie/assertion.
   Provare trigger, ordine, SQLSTATE, overflow, rollback, concorrenza e forme
   INSERT SELECT/DELETE JOIN su ambiente di prova isolato, con piano e backup
   già definiti prima di creare oggetti. Conservare gli originali certificati e
   documentare ogni adattamento MariaDB. Nessuna equivalenza automatica fra motori.
6. **Solo dopo revisione del piano concreto**, se il predecessore è verificato:
   000 preflight → 001 expand → 002 backfill → 003 validate → 004 delete contract
   → 005 validate. DDL a commit implicito: errore significa diagnosi dello stato
   parziale, nessun retry/restore automatico. Conservare generation nonzero;
   backfill solo di installazione, nessun reset. Attesi 33/64/99/141 a parità di
   schema iniziale; verificare anche definizioni precedenti e assenza di orfani.
7. **Ricertificare la persistence su MariaDB**, dopo che teardown e guardie sono
   compatibili: build, prove MT5B mirate e regressione completa, confrontando le
   738 identità storiche (51 unit + 687 integration), nessun salto o indebolimento.
   Override di connessione/strict session/dump solo nei processi figli; GLOBAL e
   configurazioni operative invariati. Verificare teardown e integrità finale.

## Ripresa dal checkpoint JWT

Riprendere dall'entity, repository e generation persistente già implementati.
Solo dopo la ricertificazione locale: emissione generation coerente con membership
e ruoli; claim unico, non negativo, Int64; confronto persistito a ogni richiesta,
senza cache o fallback zero. Rifiutare token senza generation e missing-state;
provare ruoli/stato roundtrip, revoca monotona, isolamento tenant e overflow.
Attivazione coordinata fail-closed su tutti i nodi. Manager IAM, ledger, audit,
Last Admin e API scriventi restano checkpoint successivi; rollout chiuso.

**Esito attuale:** piano preparato; riallineamento non eseguito. Restano da
completare identità/schema SQL e recupero dei sei SQL certificati prima di poter
presentare un delta DDL eseguibile. I 738 Passed precedenti restano evidenza MySQL,
non un risultato ottenuto oggi su MariaDB.
