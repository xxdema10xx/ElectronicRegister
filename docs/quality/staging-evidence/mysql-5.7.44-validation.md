# Staging MySQL 5.7.44 / OpenSSL — release gate BLOCCATO

Data: 2026-10-30. Branch: `refactor-database-entities`.
Base del diff cumulativo: `d580727dedb1d37fc8dce8aee81ccd597e031c45`.
Nessun commit, push, modifica main/Nginx/EC2 o accesso a database esistenti.

## Immagine disponibile e verifiche di provenienza

Fonte: [Docker Official Image mysql](https://hub.docker.com/_/mysql), repository
`registry-1.docker.io/library/mysql`, tag `5.7.44`.
Non viene presentata come immagine con supporto commerciale Oracle.
Le [note ufficiali 5.7.44](https://dev.mysql.com/doc/relnotes/mysql/5.7/en/news-5-7-44.html)
identificano questa versione come ultima release della serie 5.7.

Immagine selezionata:

```text
mysql:5.7.44@sha256:dab0a802b44617303694fb17d166501de279c3031ddeb28c56ecf7fcab5ef0da
```

| Verifica eseguita localmente | Risultato |
| --- | --- |
| Index del tag, SHA256 dei byte ricevuti | `4bc6bc963e6d8443453676cae56536f4b8156d78bae03c0145cbe47c2aad73bb` |
| Manifest linux/amd64, SHA256 e header registry | `dab0a802b44617303694fb17d166501de279c3031ddeb28c56ecf7fcab5ef0da` |
| Config verificato contro il descriptor | `5107333e08a87b836d48ff7528b1e84b9c86781cc9f1748bbc1b8c42a870d933` |
| Config architettura/OS | amd64 / linux |
| `/etc/passwd` dal layer verificato | mysql UID/GID 999:999 |
| Env pacchetto e stringa binario | `MYSQL_VERSION=5.7.44-1.el7`, binario `5.7.44` |
| ELF64 DT_NEEDED del server | `libssl.so.10`, `libcrypto.so.10`; macchina 62, x86-64 |
| Protocolli compilati, stringa nel binario | `TLSv1,TLSv1.1,TLSv1.2` |
| SHA256 del server estratto | `491165213f15b6bcfe103f8faafbdfbb6a7417c6cda327ea37d2dad2d6fc00c8` |

Manifest, config e layer ispezionati sono stati ottenuti con HTTPS dal registry
ufficiale. I byte scaricati sono stati confrontati con i rispettivi SHA256.
La history mostra pacchetti dal repository `repo.mysql.com`, `gpgcheck=1` e
creazione dell'utente mysql 999. Non è stata effettuata una verifica indipendente
della firma dell'immagine o del pacchetto RPM installato: non viene dichiarata.
Entrypoint e configurazione sono stati ispezionati: supportano avvio non-root,
password da file, socket esplicito e log su stderr; la directory secure-file-priv
è già presente nell'immagine. I certificati dedicati restano forniti dallo stato.

[Evidenza strutturata](mysql-5.7.44-registry.json), con descriptor dei layer e
flag runtime esplicitamente falsi. Le prove statiche non equivalgono a esecuzione
di `mysqld --version`, UID effettivo di PID 1 o negoziazione TLS: questi controlli
sono obbligatori nel workflow aggiornato e non risultano ancora eseguiti.

## Modifiche dello staging

- `mysql-image.json` contiene la policy condivisa: versione esatta 5.7.44,
  digest verificato, linux/amd64, UID/GID 999 e TLS 1.2/OpenSSL.
- `host.py` accetta solo il digest revisionato, controlla piattaforma, UID/GID,
  versione esatta e capacità TLS eseguendo il binario durante build. Registra
  `mysql-capabilities.json`; le prove TLS vere restano separate.
- Nuovo formato stato 2 con versione e immagine esplicite. Gli stati precedenti
  vengono rifiutati prima di qualsiasi operazione Docker. Nuova directory,
  UUID, rete e volume per il passaggio di versione; nessun upgrade o riuso dei
  volumi 5.7.24. Un restart è consentito soltanto per lo stesso stato 5.7.44.
- Runtime config `.NET` e preflight generati richiedono esattamente 5.7.44.
  Attestation verifica VERSION, UUID, hostname, datadir, porta,
  require_secure_transport, have_ssl/have_openssl e TLSv1.2. Verifica inoltre
  sessione TLSv1.2 e cipher non vuoto prima del DDL.
- Health SQL richiede versione esatta, SSL e OpenSSL attivi, TLSv1.2 esclusivo,
  secure transport e server finale con rete attiva. Il timeout 180s è invariato.
- `compatibility` dopo `provision`: preflight reale, journal completo,
  hash manifest/bundle/journal, numero di oggetti, metadati routine e DEFINER.
- `start` dimostra anche HTTP 200 dalla porta host loopback, senza seguire
  redirect. Restano le verifiche dei binding esatti e UID non-root di PID 1.
- Workflow Ubuntu: build → db → provision → compatibility → TLS → seed → start.
  Il gate non ammette selezione arbitraria di immagini via input workflow.
  Diagnostica sanitizzata prima dello stop preservata, inclusi marker bootstrap.

Restano obbligatori VerifyFull/VERIFY_IDENTITY, CA esplicita, TLS 1.2, test
negativi su hostname e CA, API 1654, SQL 999, ops 10001, DB senza porte pubbliche,
API solo `127.0.0.1`, ruoli/grant SQL separati, definer ACCOUNT LOCK,
verifica source/manifest e provisioning separato dall'avvio API.

## Compatibilità SQL e audit dei vincoli di versione

Nessuna modifica ai byte di schema, trigger, procedure o migrazioni storiche.
Il generatore ufficiale produce il bundle isolato con bootstrap e 001–005,
DEFINER esplicito, SQL_MODE canonico, grant finali riapplicati dopo 005.
Le primitive usate (JSON, SIGNAL, trigger multipli, ACCOUNT LOCK, mysql.proc,
SQL SECURITY DEFINER e READS SQL DATA) appartengono alla serie MySQL 5.7.
Non è stato identificato un cambiamento SQL necessario nella revisione statica.

Questo non dimostra compatibilità esecutiva: il nuovo gate deve confrontare
SHOW CREATE TABLE/TRIGGER, corpi e SQL_MODE canonici, routine e DEFINER e
chiamare `er_schema_ready()`. Deve verificare 71 tabelle, 180 trigger,
1 procedura e 0 stored function (non ne esistono nel bundle canonico).
Definizioni o metadati diversi bloccano il rilascio; non vengono normalizzate
ulteriori differenze per ottenere il verde.

Ricerca globale con `git grep` di `5.7.24`: i controlli attivi dello staging
sono stati aggiornati; i riferimenti residui nello staging sono contesto storico
e test negativi. Sono rimasti invariati il fallback Local di
`OperationalPipeline.cs`, `infra/release.runtime.example.json`, IntegrationFixture,
fixture/test IAM/MT5/MT6, `local_test_db.py`, `formative_test_db.py`, script locali
di migrazione e retention, PlatformAdmin, manifest e report storici.
I test verificano espressamente la conservazione del default locale 5.7.24.

## Verifiche locali

| Verifica | Risultato |
| --- | --- |
| `node tools/release-artifacts.cjs --write` e verifica ufficiale | 752 artifact, LF/CRLF invarianti |
| `node tools/test-release-manifest.cjs` | 14 controlli negativi passati |
| `python -m unittest discover -s tools -p 'test_*.py'` | 36 passati |
| `python tools/verify-project-artifacts.py` | PASS |
| `py_compile` host/ops/diagnostics | PASS |
| `bash -n` bootstrap e health | PASS |
| actionlint 1.7.12 sul workflow | PASS; shellcheck/pyflakes non disponibili, disabilitati |
| Build solution Release `--no-restore` | PASS, 0 errori; 10 warning nullable in file Integration non modificati |
| Unit backend Release `--no-build --no-restore` | 598 passati, 0 falliti, 0 ignorati |
| Build ReleaseTransportProbe e StagingSupport Release `--no-restore` | PASS, 0 warning/errori |
| CodeOrganization `check .` | PASS, 660 file / 657 tipi, 0 violazioni |
| `git diff --check` | PASS |
| Docker, Compose runtime, SQL/TLS reali, Integration DB | NON ESEGUITI |

I test Python coprono versione esatta, rifiuto 5.7.24 e stati precedenti,
config runtime/preflight, TLS fail-closed, binding dell'evidenza registry,
journal incompleto, migrazione alterata, stored function inattesa, HTTP redirect,
sanitizzazione, ownership e timeout diagnostici. Sono test locali con mock dove
necessario: non costituiscono risultati di provisioning/TLS su server reale.
Python embedded e actionlint sono stati usati temporaneamente nel workspace.
Non sono state alterate installazioni di sistema o usati database esistenti.

## Rischi residui e condizioni EC2

**GitHub Actions non è stato eseguito con queste modifiche.** Docker non è
installato su questa workstation e WSL non è disponibile. Inoltre il workflow
fa checkout di una SHA pubblicata e richiede codice pulito: con il divieto di
commit/push, un dispatch o rerun della SHA precedente eseguirebbe il vecchio codice
e non potrebbe validare questa patch. Non è stato aggirato questo vincolo.

Il release gate resta BLOCCATO finché il proprietario non pubblica la patch con
il proprio processo e ottiene entrambi i run CI/source e staging Docker verdi
sulla medesima nuova SHA. Il run staging deve mostrare tutti i gate sopra,
in particolare provisioning/compatibilità, TLS positivo e negativi CA/hostname,
MySQL healthy senza port binding, API healthy HTTP 200 loopback e UID corretti.
Qualsiasi failure richiede diagnosi dai log sanitizzati, senza replay DDL,
abbassamento TLS o riuso del volume per cambiare versione.

5.7.44 e Oracle Linux 7 sono componenti legacy: digest e TLS non equivalgono a
assenza di vulnerabilità o supporto corrente. L'immagine non è stata sottoposta
qui a vulnerability scan; l'uso resta temporaneo e isolato, non READY Production.
La preservazione dei contratti SHOW CREATE e il comportamento dei trigger sotto
5.7.44 rimangono rischi esecutivi da chiudere con il run, non conclusioni statiche.

Solo dopo questi PASS si può pianificare la fase EC2 separatamente autorizzata:
stessa SHA/digest, host Ubuntu amd64 e Docker >=28, nuovo stato/volume, subnet e
porta libere. Il collaudo pubblico HTTPS/JWT/CORS tramite Nginx rimane un gate
successivo. Questa attività non modifica né autorizza implicitamente EC2/Nginx.
