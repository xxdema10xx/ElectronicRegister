# MT-4C — restore sul nuovo MAMP Windows

21 ottobre 2026. Restore autorizzato completato; nessuna fase MT5B o JWT avviata.
Controlli eseguiti sul database SQL reale, non dedotti dalle precedenti evidenze.

## Target e preflight

| Voce | Valore verificato |
| --- | --- |
| Server | MySQL 5.7.24, hostname CES01622 |
| Processo | C:\MAMP\bin\mysql\bin\mysqld.exe, PID 9408 |
| Connessione | TCP esplicito 127.0.0.1:3306, listener correlato al processo |
| Datadir SQL | C:\MAMP\db\mysql\ |
| DATABASE() | electronic_register_test |
| Charset / collation schema | utf8mb4 / utf8mb4_unicode_ci |
| Stato iniziale | 0 tabelle/view, 0 routine, 0 eventi |
| DEFINER disponibili | root@localhost ed er_test@localhost, non bloccati |

Identità processo/listener e SQL verificate prima dell'import, della migrazione
e dei backup, e nuovamente a chiusura. Charset/collation e disponibilità dei
DEFINER ricontrollati. Nessuna connessione selezionata verso altri database:
le letture dei cataloghi per identità, account e integrità sono state effettuate
con electronic_register_test come database corrente.

GLOBAL sql_mode rimasto:
`ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION`.
Nessuna modifica a password, GRANT, servizi, porte o configurazioni MAMP.
Nessun intervento o collegamento a XAMPP.

## Sorgenti verificate

Dump originale invariato:
`migrations/mt4/private/20261019-165115-0036.sql`.
SHA256 **80797A2C793366040D9B231544FA1AC7CD0770E5706F7AB6EDB806E88BBC022A**.
DEFINER originali mantenuti. Nessuna riscrittura o conversione del dump.

Tutti i sei SQL canonici coincidono con
`TestResults/mt5a-mamp-plan/canonical-sql-hashes.json`:

| File sotto migrations/mt4 | SHA256 |
| --- | --- |
| 001_expand.sql | 780905AD6761C542E553A857F8BFCE0AC66338C7F3F0D1E72883D41F0B1A3900 |
| 002_backfill.sql | 02AC1CBFAD27E240DFC9185EBFC64CE86F0752DF07FC693586B2CA71C2036F8F |
| 003_validate.sql | 6F112EBE000AB601CCE4276686CC7DB43B1853D9B420B00320FA4542FBBEFE93 |
| 004_enrollment_audit_expand.sql | 877B1CC060B6886EAF90424B76D588D1D1A80728751365025705C67AE89FCFFE |
| 005_enrollment_audit_backfill.sql | 371E8E1F065C02C8941AB6B8B8105B9634F05A2C0F9C665B030C4B3592655338 |
| 006_enrollment_audit_validate.sql | F0B52A0D09FF780DFD5981EEC6E0436B199FF3EF035B15186BABDA25FA2C2EA2 |

001 e 002 **non eseguiti**. Le precedenti evidenze sono state usate come
riferimento di definizioni; i risultati riportati sotto provengono dalle nuove
query e dai confronti sul contenuto originale del dump.

## Backup privati

Directory nuova:
`migrations/mt4/private/mt4c-new-mamp-20261021/`, verificata esclusa da Git.
ACL con ereditarietà disabilitata: accesso al proprietario del workspace,
account sandbox esecutore, SYSTEM e Administrators; nessun gruppo generico Users.
Verifica ACL eseguita prima di acquisire dati e ripetuta alla fine.
Spazio disponibile: 296.198.369.280 byte all'inizio, 295.899.721.728 alla chiusura.

Client mysqldump MySQL 5.7.24, target e database espliciti, single-transaction,
routine/eventi/trigger inclusi, GTID OFF, senza add-drop-table. Nessun altro
database incluso. Credenziali soltanto nell'ambiente del processo figlio, mai
stampate o inserite nella CLI; nessuna modifica all'ambiente del processo padre.

| Backup | Byte | Tabelle / trigger | SHA256 |
| --- | ---: | --- | --- |
| empty-before-restore.sql | 1.624 | 0 / 0 | C6D1757C8EDE58611D0CCFF6F989BA665CD5B21870A83A66DBE56983DE2E79D1 |
| before-mt4c.sql | 149.540 | 31 / 48 | 08082D2C02C583B317FC94BB3B80A3B2867A9C7141C17C563936BF1265EC796E |
| final-mt4c.sql | 162.087 | 32 / 53 | C7376B79D3FCFFE9C8F9911538516DBF0245072B57162D14079FD01DEEF27555 |

Tutti: exit code client 0, footer Dump completed e inventario definizioni
verificati; SHA256 riletto e riconfermato a chiusura. Anche i record dei backup
pre-MT4C e finale sono stati confrontati con le tuple del dump originale,
considerando separatamente la sola nuova revision=0. Nessuna differenza dati.
Dump, manifest dettagliati e contenuti diagnostici rimangono privati.

## Restore MT-4A

Import originale eseguito **una sola volta**, senza --force o retry, alle 10:18:19.
Prima della migrazione sono risultati positivi:

- **31 tabelle, 48 trigger, 92 FK, 131 indici**.
- Tutte le **92 FK senza orfani**, con controllo SQL di ogni chiave anche composita.
- `003_validate.sql`: **7/7 regole con violazioni zero**.
- Holding `MT3A_LEGACY_TEST` presente in stato **Provisioning**.
- Dati di tutte le **31 tabelle** uguali alle tuple del dump: confronto per valore
  serializzato in esadecimale, mantenendo NULL e molteplicità delle righe.
- Tutte le colonne, nullability/default/extra, gli indici e le regole referenziali
  confrontati con i metadati certificati del predecessore. Colonne delle FK
  confrontate anche con le definizioni originali del dump.
- **48 corpi trigger**, timing/evento/tabella e DEFINER confrontati con il dump.
  Normalizzazione della sola formattazione/commenti standalone; stringhe SQL
  preservate. Nessuna guardia esclusa dal confronto.
- Snapshot SHOW CREATE delle 31 tabelle acquisito sul server prima di MT4C.

Solo dopo questi controlli è stato verificato il backup pre-MT4C.

## Migrazione e risultato finale

`004_enrollment_audit_expand.sql` eseguito **una sola volta** alle 10:21:02.
`005_enrollment_audit_backfill.sql` eseguito **una sola volta** alle 10:21:04:
risultato **invalid_revision=0**, acquisito dall'output effettivo del file.
005 contiene soltanto la SELECT canonica, senza eventi storici artificiali.

| Verifica reale finale | Esito |
| --- | --- |
| Tabelle | **32** |
| Trigger | **53**, 48 precedenti + 5 canonici MT4C |
| FK | **98** |
| Indici | **139** |
| FK orfane | **0** su tutte le 98 chiavi |
| MT4A 003 | **7 regole, tutte zero** |
| MT4C 006 | **5 regole, tutte zero** |
| Dati delle 31 tabelle originali | Coincidenti con il dump |
| Ledger course_administration_event | Vuoto, schema e trigger canonici coerenti |
| Revision | BIGINT NOT NULL DEFAULT 0; nessuna riga NULL o diversa da 0 |
| Holding | Provisioning |
| GLOBAL sql_mode | Invariato |

Metadati finali confrontati con i riferimenti MT4C: 222 colonne, indici e regole
referenziali corrispondenti. Le vecchie SHOW CREATE sono identiche allo snapshot
post-restore, sottraendo soltanto le aggiunte attese: student_enrollment.revision
e uq_mt4c_membership_actor. Tutti i dati originali sono nuovamente confrontati
direttamente con il dump. I 48 trigger precedenti conservano definizioni/DEFINER;
i cinque nuovi corpi coincidono con 004. Backup finale verificato dopo i controlli.

## Problemi del verificatore e scostamenti procedurali

Nessun errore SQL di restore/migrazione e nessuna riparazione dello schema.
Si sono verificati tre arresti del solo verificatore privato:

1. Lettore trigger inizialmente limitato a nomi con backtick: il dump contiene
   anche nomi non quotati. Corretto il lettore e ripetute solo letture SQL.
2. Conteggio trigger del backup con lo stesso limite: il backup pre-MT4C era
   completo (31/48/footer). Verificato il file esistente, senza rigenerarlo;
   004 non era ancora stata eseguita.
3. Confronto SHOW CREATE che non rimuoveva la nuova colonna revision a causa di
   terminazioni CRLF. Il diff mostrava la sola aggiunta autorizzata. Corretto il
   verificatore; nessuna modifica al database per superare il controllo.

Per l'ultimo punto **006 è stata letta due volte**, scostandosi dalla richiesta
di eseguirla una sola volta; entrambe le letture hanno restituito cinque zero.
003 è stata riletta durante le verifiche. Nessuno statement mutante è stato
ritentato: restore, 004 e 005 ciascuno una volta. Le letture aggiuntive non hanno
modificato dati o definizioni. Durante la preparazione ACL è stata negata la
lettura del proprietario del profilo; usato il proprietario verificato del
workspace e verificata la ACL finale prima dei backup, senza privilegi aggiuntivi.

## Evidenze, Git e arresto della fase

Nella directory privata sono conservati runner, marker `.started` non
sovrascrivibili delle tre esecuzioni, manifest SHA256, output validator, controlli
FK, confronti dati/trigger/metadati e snapshot SHOW CREATE. Nessun dato personale,
contenuto delle tabelle o credenziale inserito in questo report.

Branch `refactor-database-entities`, HEAD invariato
`92cb8141b9555a36c00e517cb4cddc50d0dd7485`. Aggiunto solo questo report pubblico;
i quattro report locali precedenti restano non tracciati. Gli strumenti e i dump
nuovi sono nell'area privata ignorata. Nessun codice applicativo, frontend, JWT,
runner pubblico o SQL canonico modificato; nessun commit/push.

Ancora **assenti tutti e sei gli SQL MT5B** in migrations/mt5: 000 preflight,
001 expand, 002 backfill, 003 validate, 004 delete contract e 005 validate ordine.
Nessuno ricostruito o applicato. Nessun EnsureCreated, suite .NET o nuova istanza.
Nessuna scrittura verso database diversi da electronic_register_test.
La successiva fase MT5B richiede recupero degli originali con hash e verifica
delle guardie ambiente di runner, trigger e fixture. **Lavoro fermato qui.**

**MT-4C RESTORED ON NEW MAMP — READY FOR MT-5B ALIGNMENT**
