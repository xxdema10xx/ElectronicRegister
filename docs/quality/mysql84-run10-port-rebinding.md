# Run #10: porta dinamica dopo docker restart

Analisi del [run 41640993713](https://github.com/xxdema10xx/ElectronicRegister/actions/runs/41640993713),
commit `eeec43be27646fad392fd7b19c7d7b78dd357db0`. Nessun commit, push o deployment.

Il difetto del target TCP obsoleto è riprodotto su MySQL 8.4.13 con l'immagine
fissata dal repository. Docker riassegna la porta host richiesta dinamicamente
al restart dello stesso container. `HostConfig.PortBindings` conserva invece
la richiesta con `HostPort=""`; confrontare soltanto questo campo non rileva
la variazione della porta effettiva in `NetworkSettings.Ports`.

| Prova | Mapping pubblicato prima | Mapping pubblicato dopo | Richiesta prima/dopo |
|---|---|---|---|
| Regressione restart locale | `127.0.0.1:65245 → 3306` | `127.0.0.1:60704 → 3306` | `127.0.0.1`, `HostPort=""` |
| Restart durante upgrade locale | `127.0.0.1:60704 → 3306` | `127.0.0.1:57842 → 3306` | `127.0.0.1`, `HostPort=""` |
| Run GitHub #10 originale | `0.0.0.0:32768` e `[::]:32768` | Non acquisito dal vecchio codice | `HostIp=""`, `HostPort=""` |

Il run originale documenta la porta iniziale nel log del job `125934163072`.
Il suo artefatto upgrade `13232493896` documenta il socket pronto, barriera ON,
UUID e account corretti, e `2003/HY000` su `127.0.0.1:32768`. Non contiene
`NetworkSettings.Ports` dopo il restart e il container è stato rimosso dal
runner. La nuova porta storica non è ricostruibile: il cambio di mapping è
confermato nella riproduzione, mentre la stessa spiegazione per il run storico
resta un'inferenza supportata, non una misurazione retroattiva.

La correzione conserva separatamente richiesta e pubblicazione effettiva.
Il confronto immutabile include container ID, image ID, digest richiesto,
hostname e volume; il socket verifica versione, UUID, hostname, porta interna,
datadir, `offline_mode=1`, `USER()` e `CURRENT_USER()`. Solo dopo queste verifiche
il recovery ricava una porta da Docker sul container attestato. La destinazione
rimane `127.0.0.1`; indirizzi arbitrari, porte protette, pubblicazioni mancanti,
porte IPv4/IPv6 discordanti e divergenze da una richiesta fissa sono rifiutati.
La porta aggiornata viene propagata ai probe API e alla configurazione usata
dalle fasi successive. Il tentativo TCP sul target originale resta nelle prove.

La regressione richiede realmente `3032/HY000` sia dall'API sia dal probe TCP
con account senza `CONNECTION_ADMIN`. Registra anche il risultato API prima
di valutare l'asserzione. `2003` non soddisfa mai l'aspettativa. Dopo i controlli
persistenti e negativi, la rimozione autorizzata della barriera deve essere
persistente e l'API deve eseguire `SELECT 1` con risultato `1`.

Il journal e il Runner DDL restano invariati. Restano obbligatori i privilegi
amministrativi statici e dinamici e `SYSTEM_USER` sul root socket. Sono
conservati i rifiuti di replay implicito, drift, privilegi insufficienti e
identità errata; la regressione aggiunge l'image ID alterato alle prove reali.
Il predecessore storico e il relativo manifest non sono modificati.

La fixture dedicata è `3ccbb050937e59810333fdd89fa946d1573d1b7d0b384efd0411cdde4661d4f2`,
volume `er84-run10-20261103-data`, UUID `07381d54-d792-11f1-9a89-a291036c5940`.
Le prove integrali sono in `.diagnostics/run10-source/` e
`.diagnostics/run10-local/`; la raccolta consegnabile è in
[indice delle evidenze](run10-port-rebinding-evidence/index.json).

La regressione restart è PASS: dato preservato, barriera persistente ON,
API `3032/HY000`, root socket `root@localhost`, account con il solo
`CONNECTION_ADMIN` respinto come migratore, container/volume/immagine/UUID
alterati respinti, barriera globale e persistente OFF dopo autorizzazione,
API nuovamente raggiungibile.

L'esecuzione monolitica locale dell'upgrade resta FAIL: il runner ha esaurito
i 900 secondi di attesa della conferma locale del restart. Il restart e la
conferma sono arrivati successivamente. Non è un errore SQL e non è un PASS.
La ripresa esplicita usa lo stesso journal parziale, le stesse verifiche
del Runner e prove distinte; il report monolitico originale viene conservato.

| Verifica locale finale | Esito |
|---|---|
| Regressione restart, mapping e identità | PASS |
| Vecchia porta TCP | `2003/HY000`, conservato come errore di trasporto |
| API durante barriera ON | `3032/HY000`, verificato realmente |
| Ripresa esplicita dello stesso journal upgrade | PASS; 13 ALTER, 19 trigger e routine `done` |
| Replay implicito e drift dello schema durante recovery | Rifiutati; journal invariato e barriera mantenuta |
| Dati preesistenti, idempotenza e CHECK disabilitato | PASS |
| Accettazione upgrade | PASS; 1.386 casi, 12 fasi, zero fallimenti |
| Fresh, idempotenza e contratto equivalente | PASS; journal separato `done` |
| Accettazione fresh | PASS; 1.386 casi, zero fallimenti |
| Readiness API di entrambi gli schemi | PASS; `CALL er_schema_ready()` restituisce `1` |
| Barriera finale globale/persistente | `0` / `OFF` |
| Metadata protetti di `mysql84-local` | PASS, invariati |
| Fixture run10 finale | Arrestata; volume conservato |
| Esecuzione monolitica locale originale | FAIL, timeout della conferma restart; non riclassificata |

La [ripresa](run10-port-rebinding-evidence/recovery-phases.json) impiega
8,220 s per attestare il canale, 1.602,918 s per completare recovery e controlli
finali, 241,074 s per l'accettazione upgrade e 368,514 s per fresh e accettazione.
Il [report monolitico](run10-port-rebinding-evidence/runtime-monolithic-phases.json)
resta distinto. Sono incluse le sorgenti dello script di ripresa per renderne
verificabili anche i controlli negativi.

Le [letture SQL finali](run10-port-rebinding-evidence/final-identity.json)
confermano versione `8.4.13`, UUID invariato, `root@%` via TCP e
`root@localhost` via socket, con privilegi effettivi registrati. L'API passa
la readiness su `er84_run10_upgrade` e `electronic_register_test`. La
[prova della barriera](run10-port-rebinding-evidence/api-barrier-result.json)
e il [ripristino API](run10-port-rebinding-evidence/api-restored-result.json)
conservano returncode, stdout, stderr, errno e SQLSTATE.

Il [confronto del container protetto](run10-port-rebinding-evidence/protected-metadata-check.json)
comprende ID, nome, image ID e riferimento immagine, stato, avvio/arresto,
restart count, mount, porte richieste e pubblicate. Nessun SQL, restart,
stop o modifica è stato diretto a `mysql84-local`; solo `docker inspect`.
È stato fermato esclusivamente il container run10; nessun volume è eliminato.

Python offline: 106 test PASS. Il primo tentativo mirato nel sandbox fallisce
con WinError 5 durante la pulizia dei temporanei; la riesecuzione fuori sandbox
passa tutti i 20 test mirati. Node: closure di 1.110 file, 12 controlli negativi
manifest, deriva dei sorgenti e bundle fresh/predecessore PASS. Il nuovo file
`tools/test_mysql84_recovery.py` è incluso nella closure candidata e deve essere
incluso insieme agli altri sorgenti modificati nell'eventuale commit futuro.

I sorgenti della patch sono `tools/mysql84_recovery.py`, `tools/mysql84_local.py`,
`tools/mysql84_release.py`, `tools/mysql84_runtime.py`,
`tools/test_mysql84_restart.py` e il nuovo `tools/test_mysql84_recovery.py`.
Tutti sono presenti nel manifest candidato rigenerato
`migrations/mysql84-remediation/manifest.json`. Nessun file è stato aggiunto
all'indice Git e nessun commit è stato creato.
La [copertura dei sorgenti](run10-port-rebinding-evidence/candidate-source-coverage.json)
verifica gli hash di tutti e sei i file e il manifest storico invariato.
SHA-256 del manifest candidato:
`b40153c55ad2f31c6c78b24a24f24e5760a7664e2f0348f41c0e0da7d870f67c`.
`git diff --check` è PASS. I file run8 già non tracciati prima dell'incarico
sono stati lasciati invariati.

Un nuovo run GitHub sul codice modificato non è stato eseguito: richiederebbe
un push, escluso dall'incarico. Il PASS fresh storico non certifica la patch;
upgrade, C# e integrazione del nuovo codice richiedono ancora la verifica CI.
