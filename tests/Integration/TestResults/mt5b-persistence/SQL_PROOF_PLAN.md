# MT5B — prova SQL preliminare e cleanup delimitato

Destinazione unica: MySQL MAMP5.7.24,127.0.0.1:13306,
electronic_register_test, datadir privato mt5a-mamp-13306/data del repository.
Nessuna operazione sulla3306. Backup privato completo verificato gia acquisito;
prima della prova ripetere guard processo/listener/identita/schema/holding.

Prima dell'EXPAND serve una prova reale dei trigger. MySQL non consente trigger su
TEMPORARY TABLE: sono necessari oggetti persistenti isolati dai nomi applicativi.
Creare esclusivamente questi tre nomi, soltanto se tutti assenti:

- mt5b_probe_20261020_membership
- mt5b_probe_20261020_role
- mt5b_probe_20261020_security

Parent/ruoli/sidecar di prova riproducono PK owner-aware,FK RESTRICT e trigger di
generazione. Solo GUID e stati/ruoli sintetici, nessun dato User reale, nessuna
lettura/scrittura dei dati didattici. Nessun servizio/porta/password/GLOBAL modificato.
DDL con commit impliciti: nessuna promessa di rollback del DDL.

Prove DML in transazione: INSERT SELECT, multi-role, DELETE JOIN del parent,
cambi/ripristini di stato e ruoli, no-op, rollback, owner immutabile, stato mancante,
reset/DELETE/recreate negati, overflow, incrementi concorrenti e lock per tenant.
Rollback delle sole transazioni di prova; verifica nuova connessione e zero righe
nei tre oggetti prima dell'eventuale cleanup.

Comandi di cleanup proposti, **NON autorizzati dal divieto DROP attuale**:

```sql
DROP TABLE electronic_register_test.mt5b_probe_20261020_role;
DROP TABLE electronic_register_test.mt5b_probe_20261020_security;
DROP TABLE electronic_register_test.mt5b_probe_20261020_membership;
```

Il DROP TABLE rimuove soltanto anche i trigger associati a quelle tabelle nuove.
Nessun DROP di oggetti preesistenti, nessun IF EXISTS, nessuna wildcard.
Cleanup soltanto dopo prova positiva, manifest di creazione e controlli di identita/
assenza righe/assenza dipendenze esterne; se la prova fallisce conservare lo stato
per diagnosi e interrompere. La migrazione definitiva non comincia prima della
prova positiva e di un nuovo backup verificato dello schema applicativo originale.

Se non viene concessa la deroga al DROP, e possibile conservare i tre oggetti di
prova documentandoli, ma non rimuoverli implicitamente durante la migrazione.
