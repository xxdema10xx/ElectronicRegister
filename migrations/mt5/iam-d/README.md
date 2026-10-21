# MT-5B-D — ledger IAM additivo (MySQL 5.7.24)

Non sostituisce candidate-r3 e non deve ripeterne l'installazione. Solo database
`electronic_register_test` dell'istanza isolata `127.0.0.1:13306`, datadir privato
`migrations/mt4/private/mt5a-mamp-13306/data/`. Mai la baseline 3306.

Ordine: 001, 002, 003, 004. I manifest separati congelano gli hash dei due passaggi
additivi realmente eseguiti. I validator 002/004 sono in sola lettura: rispettivamente
quattro e due conteggi a zero. Non esistono DOWN, DROP, reset o retry automatici.
001 crea esclusivamente il ledger e tre trigger; 003 aggiunge un quarto trigger,
senza sostituire i precedenti, per rifiutare gli array JSON annidati: JSON_CONTAINS
è ricorsivo. Inventario risultante: 34 tabelle, 68 trigger, 101 FK, 144 indici.

Prima di ciascuna DDL: correlare PID/eseguibile/listener e identità SQL di entrambi
i server, confrontare dati e metadati, verificare assenza di writer/transazioni,
hash delle sorgenti, dipendenze FK e trigger; acquisire un backup logico completo
privato con exit code/footer/inventario/hash verificati, metadati SHOW CREATE e
overlay SQL_MODE. Il dump MySQL 5.7 da solo non preserva esattamente quei metadati.
Verificare nuovamente identità e hash immediatamente prima del client. Usare il
client MAMP 5.7 con DELIMITER, senza --force, password fuori argv/log. Registrare
ogni esecuzione una sola volta in area privata; arrestarsi al primo errore DDL.
DDL a commit implicito: nessun rollback globale o restore automatico.

Il writer IAM usa il mutex Institution, rivalida l'attore persistito sotto lock,
blocca membership/ruoli/stati in ordine deterministico, impedisce self e cross-tenant,
controlla Last Admin e ExpectedRevision, applica una sola mutazione e aggiunge
l'evento nella stessa transazione. ExpectedRevision è la security generation
persistita: nessun secondo contatore, nessun incremento in C#. I no-op sono conflitti.
EventId è univoco nel tenant; hash SHA256 dell'intento tipizzato include attore,
target, azione, ruolo, motivo esatto e revision richiesta. Un replay autorizzato
restituisce il risultato storico, non lo stato attuale. Un attore revocato non può
riutilizzarlo. Mancanza dello stato di sicurezza nega anche il replay.

Audit immutabile, FK composite attore/membership/User e target/owner RESTRICT,
unicità tenant/target/revision. Nessuna credenziale/token/anagrafica nel ledger.
Eccezione DELETE soltanto per root sulla 13306 e sul datadir esatto, con owner di
sessione espliciti e codice `MT5BP_<GUID>`; holding esclusa. Nessun writer produttivo
imposta tale capability. Non è una protezione contro un DBA che modifichi lo schema.

Non installa endpoint, flag HTTP scriventi, frontend, onboarding, recupero del primo
admin o audit retroattivo. Il writer tecnico Provisioning conserva il proprio
contratto separato e non scrive eventi umani fittizi.
