# 20261027.1 — Staff istituzionale (MySQL 5.7.24)

L'ENUM aggiunge Staff **in coda**: i tre ordinali SQL precedenti non cambiano.
Aggiornati inviti (quattro ruoli), snapshot e mutazioni IAM. Nessuna membership viene
ricreata e nessuno storico IAM viene riscritto. User.role legacy non cambia.

Audit mirato `staff.*` su audit_event: classificazione grade e divieto di UPDATE/DELETE.
Inventario finale: 43 tabelle, 99 trigger, 131 FK, 180 indici.

DDL MySQL implica commit: ALTER ENUM e sostituzione trigger NON sono una transazione unica.
Fermare i writer, verificare identità esatta, versione, schema iniziale e trigger, eseguire
backup controllato prima del DDL. Un errore interrompe l'esecuzione: preservare istanza
e backup, nessun retry o rollback automatico. Ripristino solo con procedura controllata.

Il runner `tools/Invoke-InstitutionStaffMigration.ps1` supporta esclusivamente nuove istanze
di prova sotto `.staff-private`, con porta diversa da 3306/13306, datadir esplicito,
evidence directory nuova, preflight, dump SHA256 e postflight. Password via MYSQL_PWD;
non inserirla negli argomenti. Senza `-Apply` esegue soltanto preflight e backup.
Non è un comando autorizzato per database produttivi.

Creazione riproducibile: `tools/New-InstitutionStaffTestInstance.ps1 -MySqlBin <bin> -Port <nuova>`
per bootstrap attuale; aggiungere `-Baseline` per bootstrap del commit di riferimento,
poi usare il runner di migrazione. Nessun drop/reset o servizio Windows viene creato.
I processi sono nascosti, bound a loopback, con PID/evidence e password privata nel datadir padre.

`002_validate.sql` è read-only: tutti i mismatch devono essere zero.
Il contract completo e il manifest SHA256 sono in `migrations/bootstrap`.
Validatore Python: `tools/verify-institution-staff.py`, con opzioni `--mysql-bin`, `--port`,
`--datadir` per controllo read-only di tutte le tabelle e dei metadata trigger.
Senza opzioni verifica soltanto file e contract statico.

I vecchi test contengono guard di teardown root-only fissati a 13306/datadir storico.
Dopo aver verificato il contract canonico, `Bind-StaffDisposableFixtureRetention.ps1`
può vincolare i medesimi predicati ad UNA nuova istanza esatta; il bootstrap e i trigger
di produzione restano invariati. Questo adattamento test-only va registrato nel report.
