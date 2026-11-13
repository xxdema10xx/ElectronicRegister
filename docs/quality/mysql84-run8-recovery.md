# Run #8 — recovery MySQL 8.4.13

Verifica locale del 3 novembre 2026, branch `refactor-database-entities`, base
`f5b2acb08186a22e32bf13dc6e728f1640507af7`. Nessun commit, push o deployment.

**Recovery locale verificato con ripresa esplicita; certificazione complessiva
non completata.** Upgrade reale e fresh sono completati, ma l'esecuzione
monolitica locale rimane FAIL per il timeout Docker documentato. Nessun nuovo
run GitHub è stato eseguito e l'errore TCP amministrativo storico è irrecuperabile.

## Diagnosi confermata e limite storico

Sono stati scaricati e letti gli artefatti fresh, upgrade, diagnostic e static
del [run 41547078989](https://github.com/xxdema10xx/ElectronicRegister/actions/runs/41547078989),
oltre al log completo del job upgrade `125625167883`. L'artefatto upgrade è
`13189531637`, digest pubblicato
`7cb4c3484dd5f9a5ce524923b93944666958e830a6da99dd77913105dac5cfb0`.
Le copie complete estratte sono in `.diagnostics/run8-source/`.

Il server storico termina lo shutdown alle 23:43:18 e torna ready alle
23:43:20. Il journal resta con il primo ALTER in `intent`, coerentemente con
l'interruzione dopo il commit. Il vecchio ciclo richiede invece un login TCP
amministrativo riuscito e scarta `returncode`, `stdout` e `stderr` falliti.
La causa confermata del messaggio ingannevole è questa sovrapposizione fra
disponibilità del server e successo del login: il timeout non prova che
MySQL non sia ripartito.

**L'errore SQL del login amministrativo originale non è stato registrato.**
Non è ricostruibile dagli artefatti; neppure l'account effettivamente
selezionato da quel tentativo è disponibile. Non si attribuisce quindi
retroattivamente `3032/HY000` al TCP amministrativo del run #8. Nella
riproduzione locale `root@%` continua a collegarsi dopo restart, anche con
TLS disabilitato e `--get-server-public-key`, mentre l'API riceve realmente
`ERROR 3032 (HY000): The server is currently in offline mode`.
Il difetto storico esatto del login root rimane da identificare nel prossimo
run con la diagnostica aggiunta.

La [documentazione MySQL sui privilegi](https://dev.mysql.com/doc/refman/8.0/en/privileges-provided.html)
descrive il ruolo di `CONNECTION_ADMIN` nella manutenzione. I privilegi
riportati nelle prove sono quelli letti dal server reale, non inferiti dal
nome `root`.

## Correzione limitata al recovery

- `mysql84_recovery.py` distingue connessione TCP alla porta pubblicata,
  login TCP originale e query socket nel container attestato; conserva
  returncode, stdout, stderr, errno e SQLSTATE. Non registra argv,
  password o connection string.
- L'accesso socket usa esplicitamente `root@localhost` nel container
  effimero, con client dell'immagine, socket fisso e configurazione client
  indipendente da quella del runner. Il confronto comprende container,
  image ID/digest, hostname, binding porte e volume dati. UUID, versione,
  datadir, identità effettiva e barriera sono verificati tramite SQL.
- Il canale è ammesso soltanto nella certificazione locale attestata o
  nel service CI dedicato. Override Docker sono rifiutati. Ogni operazione
  del client recovery rinnova i controlli di container e identità SQL.
- Restano obbligatori PROCESS, SHOW DATABASES, CONNECTION_ADMIN e
  SYSTEM_VARIABLES_ADMIN; il root socket deve avere anche SYSTEM_USER.
  `ALL PRIVILEGES` statico non sostituisce la verifica dei privilegi dinamici.
- `offline_mode` resta ON e persistente durante il recupero. L'API continua
  a usare TCP anche quando il migratore passa al socket.
- Il journal, il confronto dello schema completo, i fingerprint e il
  riconoscimento degli ALTER/CREATE già commessi rimangono attivi. Nessun
  DDL incerto viene ritentato dal probe. La normale procedura autorizzata
  rimuove la barriera soltanto dopo la verifica finale.
- Il workflow aggiunge regressioni reali nel solo job upgrade e carica
  le nuove prove. Fresh rimane distinto; integrazione continua a dipendere
  da release e diagnostic; final-gate non cambia i criteri di esito.

## Risultati

| Controllo | Esito |
|---|---|
| Restart reale: UUID, dati, barriera persistente | PASS |
| API rifiutata 3032/HY000; socket autorizzato | PASS |
| TCP rifiutato e server disponibile distinti nello stesso probe | PASS |
| Account con solo CONNECTION_ADMIN rifiutato come migratore | PASS |
| Identità UUID/container/volume diverse rifiutate | PASS |
| Upgrade reale con recovery esplicito, dati e idempotenza | PASS; 13 ALTER, 19 trigger, routine e journal done |
| Accettazione dopo recovery | PASS; 1.386 casi, 12 fasi, 0 fallimenti |
| Fresh install completa e accettazione | PASS; 1.386 casi, 0 fallimenti; journal distinto done |
| Readiness API dei due schemi; barriera persistente OFF | PASS |
| Esecuzione monolitica locale | FAIL; timeout read-only Docker, conservato separatamente |
| Python offline | PASS, 104 test |
| Unit C# | PASS, 598; 0 fail, 0 skipped |
| Node candidato, 12 controlli negativi manifest, adapter/hash | PASS |
| Manifest candidato | PASS, 1.109 file di closure |
| Predecessore immutabile | PASS, checksum closure e 11 file storici confrontati |
| git diff --check | PASS |
| GitHub Actions con le nuove modifiche | BLOCKED: nessun push/run autorizzato |

La ripresa è registrata in
[recovery-runtime-phases.json](run8-recovery-evidence/recovery-runtime-phases.json):
canale/stato persistente PASS, recovery DDL PASS (1.495,812 s), accettazione
PASS (235,469 s), fresh PASS (355,703 s). Il
[report monolitico](run8-recovery-evidence/runtime-monolithic-phases.json)
resta FAIL con le fasi dipendenti BLOCKED. Le
[identità e readiness finali](run8-recovery-evidence/final-identity.json)
confermano `root@%` via TCP, `root@localhost` via socket, privilegi effettivi,
UUID invariato e `offline_mode` globale/persistente OFF.

## Ambiente ed evidenze

Docker Desktop era fermo; è stato avviato con approvazione esplicita.
L'inventario iniziale dopo l'avvio mostra `mysql84-local` automaticamente
in esecuzione. Nessun comando SQL, restart, stop, configurazione o mount è
stato diretto a quel container. Le attestazioni lo leggono soltanto con
`docker inspect`. Non si può dichiarare invariato lo StartedAt precedente
all'avvio di Docker Desktop; non era osservabile con il daemon fermo.
Fra le snapshot raccolte durante la verifica e a fine lavoro, ID, StartedAt,
RestartCount, immagine, hostname, mount e porte del container protetto sono
invariati: [confronto metadata](run8-recovery-evidence/protected-metadata-check.json).

Le due fixture create per questa verifica hanno volumi nuovi indipendenti.
La prima (`2a79ec5343e4…`, porta 13385) è arrestata e conservata con journal
parziale e manutenzione persistente: era una prova diagnostica superata
dalle regressioni definitive, **non un upgrade PASS**. La fixture finale
è `e3b219c9eb6236579bbfdde1dd17ff8d43faa8a66a8ccdb5aa6c91a8e2fc0e93`,
porta 13386, UUID `ccb5af5b-d772-11f1-a92e-62d36692ed32`, volume
`er84-run8-final-20261103-data`, conservata in esecuzione per ispezione.
Una sola fixture di certificazione attiva.
Nessun volume o container preesistente eliminato; nessun prune o accesso
a EC2, staging o produzione.

Le prove complete locali sono in `.diagnostics/run8-local/`; la raccolta
consegnabile è nell'[indice JSON](run8-recovery-evidence/index.json), con
hash e collegamenti agli artefatti in `run8-recovery-evidence/`.
I tentativi preliminari includono
un errore della fixture API (SELECT mancante, corretto), il rifiuto delle
directory temporanee MXC nella suite Python (rieseguita con approvazione)
e una suite offline avviata prima di aggiornare il manifest. Nessuno di
questi tentativi è conteggiato come PASS; valgono gli esiti finali indicati.

La prima esecuzione completa sul container finale ha inoltre rilevato un
errore introdotto nel ritorno a TCP: la nuova connessione non aveva ancora
eseguito la selezione del database. Il successivo fingerprint SELECT ha
restituito `ERROR 1046 (3D000): No database selected`, con journal già `done`
e barriera rimossa dalla procedura verificata. Il tentativo rimane FAIL e
le sue fasi sono conservate in `final-attempt-1046-phases.json`. La correzione
chiama nuovamente `identity(db, config)`, incluso USE del database attestato,
prima di proseguire. L'intero percorso è ripetuto su un nuovo schema
`er84_run8_verified`, senza riutilizzare quel tentativo come PASS.

Questa ripetizione ha incontrato un timeout locale distinto, di 10 secondi,
del processo `docker exec` usato dall'osservatore prima di uno SHOW CREATE
TABLE. Non è un errno MySQL e non è un `3032`: il client non ha completato
la query di sola lettura. Il journal strutturale è rimasto parziale e la
manutenzione ON. Una successiva lettura reale ha confermato versione/UUID,
barriera persistente e assenza di sessioni client residue. L'esecuzione
monolitica resta FAIL (`runtime-monolithic-phases.json`). Non sono stati
aumentati timeout o ripetuti DDL dal probe. La ripresa esplicita usa lo stesso
Runner/recover, journal e attestazione, e produce un report separato in
`.diagnostics/run8-local/recovery-verified/`; il relativo script è incluso
nelle prove consegnate. La riuscita separata della ripresa non converte
il precedente FAIL in PASS.

## Certificazione GitHub ancora necessaria

Occorre un nuovo run sul codice modificato: actionlint del workflow,
regressione restart nel service container Ubuntu, nuova diagnostica
TCP/identità/privilegi, recovery upgrade completo, fresh, prova C# incorporata
MySqlConnector/HTTP, matrice d'integrazione dipendente e final-gate.
La verifica locale non certifica hostname/account/rete del runner GitHub.
Il nuovo run deve anche chiarire il login TCP originariamente opaco; un
successo via socket non dimostra da solo quale fosse il suo errore storico.
