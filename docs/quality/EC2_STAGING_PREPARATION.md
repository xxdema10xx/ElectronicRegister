# Preparazione Docker e staging EC2

Data: 2026-10-30. **Preparazione locale completata; Docker, TLS del server container e collaudo EC2 non eseguiti.** Nessun commit/push, collegamento EC2, modifica di Nginx/certificati o accesso a database preesistenti. Nessuna nuova valutazione degli audit conclusi.

## Consegna

Il [runbook operativo](../../infra/staging/README.md) contiene comandi Ubuntu per ricognizione, installazione condizionale dei prerequisiti, checkout della SHA pubblicata, preparazione dello stato privato, build, nuovo database, provisioning, preflight, TLS, seed sintetico, avvio, nuova vhost HTTPS, smoke, log e stop/restart conservativo.

- [Compose](../../infra/staging/compose.yaml): namespace casuale, volume persistente dedicato, SQL senza porte pubblicate, API solo `127.0.0.1:18080`, processi API/SQL non-root, health, restart e rotazione log. Un inizializzatore root prepara esclusivamente i permessi del volume nuovo.
- [Controllo host](../../infra/staging/host.py): solo daemon Docker locale, verifica ownership, collisioni di subnet/porta, checkout pulito e identità degli artifact; nessuna funzione SSH o modifica Nginx. Stato e credenziali devono essere fuori dal checkout.
- [Job SQL](../../infra/staging/ops.py) e [immagine operatore](../../infra/staging/Dockerfile.ops): provisioning separato dall'API, attestazione UUID/versione/datadir, principal espliciti e binding dei definer, migrazioni 001–005 ordinate, journal apply-once e preflight. Un passo SQL parziale blocca il replay: non simula idempotenza di DDL non transazionale. L'account API non riceve privilegi DDL.
- [Smoke HTTPS](../../infra/staging/smoke.py): login reale, firma/purpose JWT, selezione membership, tenant negativi, CRUD, Exam/Submission/Grade e replay. Credenziali e report grezzi restano privati.
- [Supporto health](../../tools/StagingSupport/Program.cs): probe locale con redirect disabilitati; health negativa se 503 o redirect. Il medesimo helper genera BCrypt per le sole fixture sintetiche.
- [Workflow staging](../../.github/workflows/staging-package.yml): collaudo Docker/SQL/TLS su runner Ubuntu effimero della SHA esplicitamente pubblicata, senza credenziali EC2. Il workflow portabile include restore locked, build e audit anche del nuovo helper. I comandi `gh` nel runbook controllano headSha ed esito dei run effettivi.

Il manifest canonico corrente comprende ora **750 artifact**, inclusi i nuovi input operativi. Sono cambiati solo cinque artifact preesistenti nel suo inventario: `.dockerignore`, `.gitignore`, workflow verify, Dockerfile e validatore della closure. Aggiunti dodici artifact. Nessun file è stato rimosso dall'inventario. Il manifest storico e gli SQL restano invariati.

## Evidenze locali effettive

| Controllo | Esito / prova |
|---|---|
| Build soluzione Release | PASS, 0 warning / 0 errori: [log](staging-evidence/backend-build.log) |
| Unit | PASS, 590/590, zero failed/skipped: [TRX](staging-evidence/Unit/result.trx) |
| Roslyn | PASS, 660 file, 657 tipi, zero violazioni: [log](staging-evidence/guard.log) |
| Tooling Python | PASS, 25 test, inclusi 7 dello staging: [log](staging-evidence/tooling-final.log) |
| Manifest | PASS, 750 artifact e normalizzazione LF/CRLF: [log](staging-evidence/manifest-final.log) |
| Manifest negativi | PASS, 14 mutanti respinti: [log](staging-evidence/manifest-negatives.log) |
| Preservazione | 713 file applicativi/SQL invariati rispetto allo snapshot iniziale: [confronto](staging-evidence/preservation.json), [script](staging-evidence/check-preservation.cjs) |
| Diff whitespace | PASS, exit 0; warning Git di conversione newline conservati nel [log](staging-evidence/diff-check.log) |
| Nuovo helper | Restore locked e build verificati; [audit online](staging-evidence/support-audit.json) e [gate](staging-evidence/support-audit-gate.log) senza vulnerabilità riportate per questo progetto |
| Health e BCrypt | Eseguiti contro mock HTTP locale: 200 accettato, 503/307 respinti, nessun redirect seguito; hash con salt distinti: [risultati](staging-evidence/support-execution.json). Non è una prova dell'API container |
| Sintassi | YAML Compose e due workflow analizzati; Bash `-n` sui due script SQL superato. Non equivale a `docker compose config/build` |
| Immagine MySQL | Manifest pubblico e digest linux/amd64 verificati via HTTPS: [metadati](staging-evidence/registry-metadata.json). Nessun container eseguito |

L'audit NuGet del nuovo helper usa il relay locale che verifica HTTPS verso gli upstream NuGet: [richieste](staging-evidence/nuget-relay-requests.log). I primi tentativi senza relay contengono NU1900/NU1905 e **non** sono prova valida; i log `support-locked`, `support-build-verified` e l'audit successivo documentano il controllo riuscito. Non è uno scan di immagine, OS o MySQL.

La baseline Integration **1531** appartiene alla regressione precedente conclusa. Non è stata rieseguita in questa preparazione infrastrutturale; nessun codice applicativo o test SQL/HTTP è stato modificato. Non viene presentata come nuovo risultato Docker.

## Gate ancora da eseguire

Docker e Podman non sono disponibili in questo ambiente: build/run, Compose runtime validation, UID effettivi, SQL iniziale, definer/permessi e readiness container restano da verificare. Non è stata avviata alcuna istanza MySQL in questa attività.

Il candidato è MySQL **5.7.24** linux/amd64 con digest fissato. Lo staging impone TLS 1.2 e `require_secure_transport`, driver MySqlConnector 2.6.1 con **VerifyFull**, CA dedicata e SAN `er-stage-db`. Il job `tls` deve dimostrare connessione positiva e rifiuto di CA/hostname errati. L'esistenza dell'immagine e MAMP locale non dimostrano questi requisiti; un errore del gate deve bloccare il collaudo senza downgrade TLS.

Restano necessari: pubblicazione autorizzata dei file e della closure nella stessa SHA; Actions Linux/Windows e regressione SQL sul runner dedicato; build/startup reali; TLS positivo/negativi; disponibilità DNS e certificato HTTPS del nuovo sottodominio; autorizzazione separata per le aggiunte Nginx; smoke pubblico attraverso il proxy host. Il dominio principale e i vecchi frontend non vengono sostituiti da questa preparazione.

Lo staging conserva un volume nuovo e secret locali. Non introduce backup/restore automatici, rinnovi certificati o un upgrade MySQL implicito. Il runbook esplicita rinnovo SQL a 90 giorni e HTTPS, conservazione dei secret, stop/restart senza delete/down/prune e separazione da un futuro rollback dati autorizzato. Non costituisce dichiarazione READY Production.
