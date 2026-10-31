**Image drift API — intervento locale, 31 ottobre 2026**

Branch: `refactor-database-entities`. Base e HEAD: `0ffbad41f2ba22ac10a8486c1a64712d6fd6d48e`. Nessun commit/push. I tre file non tracciati preesistenti relativi a MySQL sono rimasti intatti.

**Diagnosi e grado di conferma**

Le evidenze EC2 dimostrano la sostituzione dell'immagine API: ID registrato `sha256:5e921ff749427266d86c5a099cfa558851fa6e181d43194ec5d1bf4d1aea8bfe`, ID successivo `sha256:a32f85a86f69c15fda86921a20a442af11478a65bc1b2e9ee65c3393f8f68900`; il vecchio ID non e piu presente. DB e OPS coincidono. Queste evidenze non identificano da sole l'invocazione responsabile o spiegano la scomparsa del vecchio ID.

Confermato dall'analisi: il controllo host precedente confrontava gli ID soltanto all'ingresso del comando operativo. Lo stesso compose.yaml conteneva build API, build OPS e additional_contexts api_artifact=service:api. Dopo la build, ogni `run ... ops ...` e ogni `up` riceveva quindi anche il grafo di build. Il comando source eseguito dentro build era esposto allo stesso problema prima della registrazione.

Nel sorgente Compose v5.6.1, run costruisce opzioni di build anche senza --build; ensureImagesExists puo invocare build e addBuildDependencies include i servizi referenziati dagli additional_contexts. `--no-deps` non e una garanzia di esclusione delle dipendenze di build. Una differenza nella selezione/riconoscimento delle immagini locali puo dunque riaprire la build API. Questo e il meccanismo sospetto per l'incidente, NON una riproduzione confermata: Docker non e installato in questo ambiente Windows e WSL non e installato. Non si attribuisce la rimozione del vecchio ID a Compose senza ulteriori evidenze.

Comandi potenzialmente coinvolti prima della modifica:

- `run --rm --no-deps ops source`, identity, provision, compatibility, seed, preflight, tls, smoke, class-delete-audit: percorso di risoluzione/build delle immagini.
- `up ... db` e `up ... api`, anche da restart: possono risolvere/scaricare/ricostruire immagini in una configurazione che contiene build; la selezione dei servizi determina quelle interessate. L'up del solo DB non dimostra una ricompilazione API.
- config, ps, exec, logs, stop non richiedono una build. verify usa ps/exec e una richiesta HTTP locale.

Fonti primarie esaminate:

- https://raw.githubusercontent.com/docker/compose/v5.6.1/cmd/compose/run.go
- https://raw.githubusercontent.com/docker/compose/v5.6.1/cmd/compose/up.go
- https://raw.githubusercontent.com/docker/compose/v5.6.1/pkg/compose/build.go

run v5.6.1 espone --build e --pull, ma non --no-build; up espone --no-build e --pull. La verifica locale del binario/help non e stata possibile; il workflow stampa entrambi gli help del binario effettivamente usato.

**Modifica implementata**

compose.yaml diventa esclusivamente runtime, senza build/additional_contexts. Tutti e quattro i servizi hanno pull_policy never. Il nuovo compose.build.yaml contiene solo le due build e mantiene il collegamento OPS/API: Dockerfile.ops continua a copiare /health e /release dall'artefatto API. Entrambi i Dockerfile restano invariati.

Solo host.py build carica l'overlay. Tutti i comandi operativi caricano esclusivamente il file runtime. up aggiunge --no-build --pull never; run aggiunge --pull never. Un'immagine mancante causa un errore, senza recupero automatico tramite build o pull.

Gli ID DB/API/OPS sono verificati prima e dopo ogni invocazione Compose operativa, inclusi ps/exec e i processi che terminano con errore. La verifica post-command avviene in finally: un drift interrompe prima del passo successivo, per esempio prima di DDL, preflight o verifica HTTP successiva. La diagnostica contiene solo servizio e ID SHA256 validati; gli errori di inspect non pubblicano stdout/stderr del daemon. Non e un monitoraggio continuo durante l'esecuzione di un processo Compose.

Anche source usa il Compose runtime e un'istantanea degli ID appena costruiti verificata prima/dopo l'esecuzione. images.json viene scritto solo dopo il successo dell'attestazione source; un record preesistente viene confrontato e non sovrascritto. Non vengono ignorati drift, forzati ID o modificati tag per recuperare uno stato guasto.

Restano attivi: checkout pulito e commit esatto, confronto manifest copiato nell'immagine, closure/hash, UID/GID, ownership Docker, SQL VerifyFull/VERIFY_IDENTITY e TLS 1.2, privilegi SQL e isolation test, firme JWT, porte loopback, DB non pubblicato, secret/volumi privati. E stato aggiornato soltanto il manifest di release corrente per includere l'overlay e i nuovi hash. Manifest storico, SQL storico e firme non sono stati modificati.

**Test eseguiti**

- Restore locked della solution: PASS usando configurazione NuGet e APPDATA temporanei, perche il NuGet.Config utente e inaccessibile. Avvisi NU1900: feed di vulnerabilita non raggiungibile; nessuna attestazione di audit vulnerabilita aggiornato.
- Build Release solution --no-restore: PASS, zero errori, 17 avvisi (NU1900 e nullable nei test esistenti).
- Unit test .NET: 598/598 PASS.
- StagingCalendarFeatureTests: 2/2 PASS. Prima esecuzione bloccata dall'assenza di python nel PATH; ripetuta con Python 3.13 disponibile e percorsi DLL temporanei corretti.
- Unittest Python discover tools/test_*.py: 68/68 PASS, inclusi staging e audit HTTP offline. Il primo controllo closure prima dell'aggiornamento del manifest falliva correttamente; dopo l'aggiornamento passa.
- Nuove regressioni: sequenza db/sql-check/provision/compatibility/preflight/tls/seed/start/verify/smoke/restart/http con ID DB/API/OPS costanti; immagini registrate immutate; overlay escluso dal runtime; drift in ciascuna delle tre immagini prima/dopo Compose e dopo un errore Compose; immagine mancante con diagnostica sanitizzata; source protetto dall'istantanea prima della registrazione.
- release-artifacts.cjs: PASS, 760 artefatti, closure e invarianza LF/CRLF.
- test-release-manifest.cjs: PASS, 14 controlli negativi del verificatore reale.
- test-mysql84.cjs: PASS offline, contratto storico invariato; nessuna dichiarazione di test SQL runtime.
- Parsing YAML workflow/runtime/build e verifica strutturale: PASS.
- git diff --check: PASS.
- Docker build/smoke/TLS reali: NON ESEGUITI, Docker e WSL assenti. EC2, Nginx, SQL storico e stati Docker esistenti non sono stati toccati.

**Verifiche rimaste in GitHub Actions**

Il workflow ora confronta due job: Compose preinstallato sul runner e Compose v5.6.1 della release ufficiale con checksum verificato e versione controllata. Registra Docker Engine, versione Compose e help run/up; controlla anche il modello Compose risolto, privo di build. L'intera sequenza reale mantiene i controlli host prima/dopo ogni chiamata e aggiunge preflight esplicito, verify, restart, verify e confronto byte-per-byte di images.json prima/dopo deployment e audit HTTP.

Non e stato avviato un workflow remoto: le modifiche sono locali e il controllo di checkout pulito impedisce di usarle come release pubblicata. Dopo pubblicazione autorizzata, entrambi i job devono completarsi sulla stessa SHA pubblicata. Verificare anche download/checksum e selezione effettiva del plugin v5.6.1.

Il runner e fissato a ubuntu-24.04, non replica Ubuntu 26.04/Engine 29.9.2 della EC2. Compose coincide nel job v5.6.1; per confermare la causa dell'incidente occorre una riproduzione su un runner effimero con le stesse versioni Engine/OS, usando prima il commit base e poi la correzione, senza dati o stati EC2 esistenti. Lo smoke HTTPS pubblico attraverso Nginx resta una verifica separata; il workflow esegue l'audit HTTP Docker locale. Lo stato EC2 gia affetto da drift deve restare bloccato: non riscrivere images.json. Il percorso ordinario dopo pubblicazione e un nuovo stato isolato, mantenendo intatto quello precedente.
