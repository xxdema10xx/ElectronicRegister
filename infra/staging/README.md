# Staging EC2: API Docker, MySQL privato, Nginx sull'host

## Target: MySQL 8.4 LTS, nuova istanza

Staging usa esclusivamente `mysql:8.4.13@sha256:5cf1e1293ea42e077e6a22114e6773b9aae3bb8f9933a2acd5a9721518ff8f5b`,
Docker Official Image `library/mysql`, linux/amd64. Registry, config e layer passwd/group
sono verificati tramite SHA256: [evidenze](../../docs/quality/staging-evidence/mysql-8.4-registry.json).
UID/GID mysql: 999:999; build e verifica PID 1 ripetono i controlli runtime.
Origine: Oracle Linux 9 slim; release MySQL 8.4.13 del 20 ottobre 2026.

Stato formato **3**, namespace/rete/credenziali/volume nuovi. Gli stati 5.7 e i volumi
non vuoti privi del marker esatto motore/UUID vengono rifiutati. Il marker viene scritto
solo alla fine del bootstrap della nuova istanza. Nessun upgrade in-place o import dati.
Un bootstrap parziale blocca il riavvio automatico del provisioning.

Il binding SQL esplicito `mysqlSeries: 8.4` applica regole versionate in
`tools/mysql84-contract.cjs`: rimozione del solo SQL mode obsoleto
`NO_AUTO_CREATE_USER`, metadata interi senza display width e rigenerazione della
routine 005 dal contratto revisionato. I sorgenti SQL storici e la baseline 5.7.24
restano invariati. Collation utf8mb4_unicode_ci, FK, indici, corpi trigger e vincoli
restano verificati; differenze sconosciute bloccano il preflight.

Migratore separato con CREATE USER e SET_ANY_DEFINER, senza SUPER; reviewer con
SHOW_ROUTINE e visibilit? metadata; API con soli CRUD/EXECUTE. Account login
caching_sha2_password REQUIRE SSL e definer ACCOUNT LOCK. Il binary log viene
disattivato soltanto su questa istanza effimera, priva di replica/PITR, per evitare
SUPER nella creazione dei trigger; nessuna disabilitazione dei controlli FK.
Il protocollo X non ? necessario ed ? disattivato. Le sessioni SQL TCP richiedono
TLS 1.2 e CA/hostname verificati (driver VerifyFull).

Il workflow esegue build, provisioning ordinato, journal, 71 tabelle/180 trigger,
routine/DEFINER/account, preflight, TLS positivo e negativi CA/hostname, rollback,
deadlock, lock timeout, verifica privilegi API, seed, readiness/UID/porte e HTTP
loopback reale con JWT, tenant, Exam/Submission/Grade, calendario e replay.
`host.py http` simula il proxy fidato solo sulla porta loopback: non certifica
HTTPS pubblico. `host.py smoke` mantiene la verifica HTTPS pubblica separata.

La diagnostica sanitizzata include la fase del probe .NET e tipi/codici eccezione,
senza messaggi o connection string. Non considera errori di rete prova del rifiuto
del certificato: i negativi richiedono il preciso errore certificate policy.

**Docker/TLS runtime non certificati localmente:** Docker non ? disponibile.
GitHub Actions deve eseguire queste modifiche sulla stessa SHA pubblicata del manifest
e delle immagini. Nessun commit/push/dispatch, accesso EC2 o modifica Nginx effettuati.
Vedere [report](../../docs/quality/MYSQL_84_MIGRATION_REPORT.md) per esiti e gate residui.

## Architettura e file

Internet → Nginx **esistente sull'host**, nuova vhost `staging.consoliprojects.dev` → `127.0.0.1:18080` → API non-root 1654 → TLS 1.2/VerifyFull/CA esplicita → MySQL 8.4.13 non-root 999 su bridge dedicato, **senza ports**. Il progetto/volume/rete hanno nomi casuali e label di ownership. Nessun Nginx container e nessun Docker socket nei container.

- `compose.yaml`: API, SQL, inizializzatore di permessi del solo volume nuovo, operator job separati.
- `host.py`: prepara stato/certificati/secret, controlla esclusivamente il daemon locale e i propri oggetti; nessuna funzione SSH, Nginx, delete/down/prune.
- `ops.py`: attestazione SQL/TLS prima del DDL, binding usando i tool di release già verificati, journal apply-once, preflight, fixture sintetica esplicita.
- `smoke.py`: richieste HTTPS reali, JWT verificati, ruoli/tenant, lifecycle Exam/Submission/Grade e idempotenza. Non usa token autocostruiti per i casi positivi.
- `Dockerfile.ops`: SDK/tooling in un job non-root 10001, separato dall'immagine API.
- `tools/StagingSupport`: health process locale senza curl e hashing BCrypt delle sole fixture.

Il DB fissato per nome è `electronic_register_staging`; il DB separato `er_stage_control` contiene l'UUID dell'istanza. Gli operator job verificano UUID, versione esatta, hostname, porta, datadir, require_secure_transport e protocollo effettivo prima di toccare lo schema. La porta interna è 3306, non viene pubblicata sull'host.

## 1. Pubblicazione e Actions — nessun commit/push da questa attività

Il proprietario deve prima pubblicare questi file tramite il proprio processo autorizzato. Le configurazioni correnti sono ancora modifiche locali: lo script **rifiuta build/avvio da checkout dirty o da una revisione diversa**. Non aggirare il controllo; preparare un nuovo stato dopo la pubblicazione. Il manifest di release corrente va incluso nella stessa revisione; quello storico non si rigenera.

Su una workstation autorizzata con GitHub CLI già autenticata, impostare repository e ref reali. Questi comandi non fanno commit/push né deployment:

```bash
export REPOSITORY=OWNER/ElectronicRegister
export REF=staging
export REV=$(gh api "repos/$REPOSITORY/commits/$REF" --jq .sha)
test ${#REV} = 40
gh workflow run verify.yml --repo "$REPOSITORY" --ref "$REF"
gh workflow run staging-package.yml --repo "$REPOSITORY" --ref "$REF" -f source_sha="$REV"
gh run list --repo "$REPOSITORY" --commit "$REV" --limit 10
# Impostare i due ID restituiti, non prendere indiscriminatamente l'ultimo run:
gh run watch "$SOURCE_RUN_ID" --repo "$REPOSITORY" --exit-status
gh run watch "$DOCKER_RUN_ID" --repo "$REPOSITORY" --exit-status
gh run view "$SOURCE_RUN_ID" --repo "$REPOSITORY" --json headSha,conclusion,jobs
gh run view "$DOCKER_RUN_ID" --repo "$REPOSITORY" --json headSha,conclusion,jobs
```

Entrambi devono avere **headSha=REV**, conclusion success, source matrix Linux/Windows verde e job SQL dedicato verde. Se il branch avanza fra lettura e dispatch il nuovo workflow fallisce correttamente il confronto SHA: selezionare di nuovo la revisione desiderata. `verify.yml` include il job MySQL manuale sul runner Windows dedicato: se runner/environment non disponibili, il relativo gate resta aperto.

`staging-package.yml` usa push sul branch dedicato (workflow/manifest) oppure workflow_dispatch su runner Ubuntu effimero: build Docker reale, nuovo DB, TLS/definer/preflight/seed e UID/readiness. Non ha credenziali AWS, SSH o deploy. Non prova il dominio/Nginx EC2: quello è il passo smoke separato. Nessuno di questi run è stato avviato o dichiarato verde qui.

## 2. Ricognizione server — solo dopo autorizzazione EC2

Eseguire nella sessione EC2 autorizzata, senza cambiare ancora servizi:

```bash
umask 077
uname -m
cat /etc/os-release
timedatectl status
df -h
sudo nginx -t
sudo nginx -T > "$HOME/nginx-before-er-staging.txt" 2>&1
sudo systemctl is-active nginx
sudo ss -lntp
ip -json route
command -v docker && sudo docker version
command -v docker && sudo docker compose version
```

Richiesti **Ubuntu supportata, x86_64, Docker Engine ≥28, Compose ≥2.24**, Git, Python 3 e OpenSSL. Il manifest pubblico verificato per MySQL 8.4.13 è solo linux/amd64: niente emulazione Graviton o upgrade MySQL automatici. [Digest controllato](../../docs/quality/staging-evidence/mysql-8.4-registry.json) fissato nel preparatore; l'esistenza del manifest **non dimostra TLS**.

Docker ≥28 è richiesto anche per la corretta segregazione della pubblicazione loopback. Il controllo è coerente con le [limitazioni delle porte pubblicate](https://docs.docker.com/engine/network/port-publishing/). Mantenere le regole AWS esistenti e non aprire 18080/3306: non sono porte Internet. Verificare che il daemon non abbia configurazioni di routing diretto inattese. Il tool non modifica security group, firewall o routing operativo.

Se Docker è già installato, usarlo se soddisfa i requisiti: **non reinstallare/rimuovere pacchetti o servizi esistenti**. Se manca, e soltanto dopo autorizzazione all'installazione, usare il [repository ufficiale Ubuntu](https://docs.docker.com/engine/install/ubuntu/):

```bash
# Prima: se esistono pacchetti docker.io/containerd/runc o altri workload, fermarsi
# e pianificare separatamente. NON eseguire apt remove o upgrade automatici.
dpkg -l docker.io containerd runc docker-ce docker-compose-plugin 2>/dev/null || true
sudo apt-get update
sudo apt-get install -y ca-certificates curl git python3 openssl
sudo install -m 0755 -d /etc/apt/keyrings
test ! -e /etc/apt/keyrings/docker.asc
sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
sudo chmod a+r /etc/apt/keyrings/docker.asc
. /etc/os-release
test "$ID" = ubuntu
test ! -e /etc/apt/sources.list.d/docker.sources
cat > /tmp/er-docker.sources <<EOF
Types: deb
URIs: https://download.docker.com/linux/ubuntu
Suites: ${UBUNTU_CODENAME:-$VERSION_CODENAME}
Components: stable
Architectures: $(dpkg --print-architecture)
Signed-By: /etc/apt/keyrings/docker.asc
EOF
sudo install -m 0644 /tmp/er-docker.sources /etc/apt/sources.list.d/docker.sources
sudo apt-get update
sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
sudo docker version
sudo docker compose version
```

Questa installazione non installa/reconfigura Nginx, non aggiunge utenti al gruppo docker e non elimina servizi. Non eseguirla se ci sono conflitti già presenti. Non usare Docker context remoti o DOCKER_HOST ssh/tcp: il preparatore consente soltanto `/var/run/docker.sock` locale.

## 3. Nuovo checkout e stato privato

Usare una directory sorgente nuova per la REV già verificata da Actions. Nessun reset/clean di repository esistenti:

```bash
export REPO_URL=https://github.com/OWNER/ElectronicRegister.git
export REV=REPLACE_WITH_VERIFIED_40_HEX_SHA
export SOURCE=/srv/electronicregister-staging/source-$REV
export STATE=/srv/electronicregister-staging/state-$REV-$(date -u +%Y%m%dT%H%M%SZ)
export DOMAIN=staging.consoliprojects.dev
test ! -e "$SOURCE"
test ! -e "$STATE"
sudo install -d -m 0755 /srv/electronicregister-staging
sudo git clone "$REPO_URL" "$SOURCE"
cd "$SOURCE"
sudo git checkout --detach "$REV"
test "$(sudo git rev-parse HEAD)" = "$REV"
test -z "$(sudo git status --porcelain)"
sudo python3 infra/staging/host.py --state "$STATE" prepare --domain "$DOMAIN" --port 18080 --subnet 172.30.252.0/24
sudo python3 infra/staging/host.py --state "$STATE" config
```

Lo stato deve restare **fuori dal checkout**. È root-only e contiene CA privata SQL, chiave server, credenziali casuali, chiave JWT, journal e fixture. La CA SQL è dedicata e non viene installata nel trust store globale né usata da Nginx. Le CA private non entrano nei container; solo il certificato CA pubblico viene montato ai client. Server key e credenziali sono bind secret readonly con proprietari/permessi reali 999/1654/10001: Compose file secrets non garantiscono rimappatura uid/gid ([documentazione](https://docs.docker.com/compose/how-tos/use-secrets/)). Non affidarsi a un `uid:` dichiarativo per sistemare i permessi.

Il JSON runtime generato usa **Staging** con le stesse protezioni TLS/CA/AllowedHosts della policy Production. RolloutApproved vale per questo nuovo namespace sintetico; worker, onboarding e governance restano disabilitati. Non è una configurazione Production da riutilizzare sui dati reali. L'API accetta solo il gateway del bridge come proxy host e il proprio loopback per health check. Non aggiungere wildcard o tutta la subnet a KnownProxies.

Il CIDR scelto viene confrontato con reti Docker e rotte host/VPC prima dell'avvio. Su collisione scegliere un nuovo stato con subnet libera; non rimuovere reti esistenti. La porta deve essere libera: non fermare altri servizi per liberarla. Per una seconda istanza scegliere anche porta e dominio differenti.

## 4. Build, nuovo DB e provisioning separato

```bash
cd "$SOURCE"
sudo python3 infra/staging/host.py --state "$STATE" build
sudo python3 infra/staging/host.py --state "$STATE" db
sudo python3 infra/staging/host.py --state "$STATE" provision
sudo python3 infra/staging/host.py --state "$STATE" compatibility
sudo python3 infra/staging/host.py --state "$STATE" preflight
sudo python3 infra/staging/host.py --state "$STATE" tls
sudo python3 infra/staging/host.py --state "$STATE" seed
sudo python3 infra/staging/host.py --state "$STATE" start
sudo python3 infra/staging/host.py --state "$STATE" verify
```

Build verifica UID e versione del binario MySQL, etichetta SHA dell'API e coerenza fra source manifest e bundle copiato dall'immagine. Le immagini effettive vengono congelate per ID in images.json. Tag/digest o checkout differenti vengono rifiutati. Il volume-init può cambiare il proprietario della sola directory vuota appena creata; su dati presenti controlla soltanto l'owner. Non fa chown ricorsivi o wipe.

La prima inizializzazione **del database** crea solo namespace vuoto, record di ownership e login migratore. L'API non applica DDL. `provision` è un comando one-shot esplicito: TLS 1.2 effettivo, VerifyIdentity, CA, UUID, VERSION=8.4.13, hostname, datadir e require_secure_transport devono corrispondere prima del bootstrap. Poi crea i login API/reviewer REQUIRE SSL, deriva i definer espliciti ACCOUNT LOCK e applica bootstrap+001–005+grant finali.

Journal e lock su file condiviso impediscono due provisioning contemporanei. Un passo completato con lo stesso hash non viene rieseguito; un passo `started` o con hash diverso **blocca**, perché il bundle DDL non ? una singola transazione. Non cancellare il journal per forzare il verde. Ripetere `provision` completo equivale a verifica/preflight, non a replay delle SQL. 005 elimina i grant della routine e il passo 99 li riapplica.

L'API ha CRUD sul solo schema ed EXECUTE sulla routine, senza DDL/SUPER/GRANT. Il migratore è privilegiato ma disponibile solo al job operatore sul nuovo DB. Il reviewer ha la visibilità metadata necessaria a MySQL 5.7; nessuna di queste credenziali viene montata nell'API. Lo stato SQL normale non contiene marker di bypass o session variable di fixture.

`tls` esegue il driver reale MySqlConnector 2.6.1: prima collegamento VerifyFull positivo, protocollo TLSv1.2 e schema ready; poi alias sullo stesso container assente dal SAN e CA indipendente errata. DNS/timeout/rifiuto socket non sono prove del certificato. Se l'immagine non offre un protocollo comune, **fermarsi**: niente TLS debole, SslMode=None, VerifyCA al posto di VerifyFull o callback permissivi. MAMP non è una prova per questo ambiente.

`seed` è intenzionalmente separato, solo sullo schema di staging nuovo/vuoto: due istituzioni, quattro account sintetici e associazioni esplicite admin/teacher/student, catalogo minimo. È idempotente tramite journal e non invia email. Credenziali in `$STATE/ops/seed.json`, da leggere solo in sessione privata; non copiare il file nel repository o nei log. Non usare questo seeder per creare istituzioni reali.

## 5. Nginx host: nuova vhost e certificato separato

Solo dopo autorizzazione a questa fase, creare DNS A (e AAAA solo se correttamente raggiungibile) di DOMAIN verso EC2. Verificare che la nuova vhost non sia già gestita:

```bash
PROJECT=$(sudo python3 -c 'import json,sys;print(json.load(open(sys.argv[1]))["project"])' "$STATE/state.json")
sudo nginx -T > "$HOME/nginx-before-er-stage-enable.txt" 2>&1
# Verificare manualmente include sites-enabled e assenza di server_name DOMAIN.
# Se il layout usa conf.d, usare quella directory già inclusa; non modificare nginx.conf.
test ! -e "/etc/nginx/sites-available/$PROJECT-http.conf"
test ! -e "/etc/nginx/sites-available/$PROJECT-https.conf"
sudo install -d -m 0755 "/var/lib/er-stage-acme/$PROJECT"
sudo install -m 0644 "$STATE/nginx-http.conf" "/etc/nginx/sites-available/$PROJECT-http.conf"
sudo ln -s "/etc/nginx/sites-available/$PROJECT-http.conf" "/etc/nginx/sites-enabled/$PROJECT-http.conf"
sudo nginx -t
sudo systemctl reload nginx
```

È una vhost HTTP nuova: espone solo la challenge ACME e redirige il resto. Nessun default_server, modifica del dominio apex, file o certificato esistente. Ottenere un certificato **nuovo** per DOMAIN. Esempio Certbot in container effimero, senza plugin Nginx e senza montare il `/etc/letsencrypt` dell'host:

```bash
sudo install -d -m 0700 "$STATE/acme/config" "$STATE/acme/work" "$STATE/acme/logs"
sudo docker pull certbot/certbot:latest
CERTBOT_IMAGE=$(sudo docker image inspect certbot/certbot:latest --format '{{index .RepoDigests 0}}')
sudo docker run --rm \
  --mount "type=bind,src=$STATE/acme/config,dst=/etc/letsencrypt" \
  --mount "type=bind,src=$STATE/acme/work,dst=/var/lib/letsencrypt" \
  --mount "type=bind,src=$STATE/acme/logs,dst=/var/log/letsencrypt" \
  --mount "type=bind,src=/var/lib/er-stage-acme/$PROJECT,dst=/var/www/acme" \
  "$CERTBOT_IMAGE" certonly --non-interactive --agree-tos --email "$ACME_EMAIL" \
  --webroot --webroot-path /var/www/acme --cert-name "$PROJECT" -d "$DOMAIN"
sudo openssl x509 -in "$STATE/acme/config/live/$PROJECT/fullchain.pem" -noout -checkhost "$DOMAIN"
sudo openssl x509 -in "$STATE/acme/config/live/$PROJECT/fullchain.pem" -noout -checkend 604800
test ! -e "/etc/nginx/$PROJECT"
sudo install -d -m 0700 "/etc/nginx/$PROJECT"
sudo install -m 0644 "$STATE/acme/config/live/$PROJECT/fullchain.pem" "/etc/nginx/$PROJECT/fullchain.pem"
sudo install -m 0600 "$STATE/acme/config/live/$PROJECT/privkey.pem" "/etc/nginx/$PROJECT/privkey.pem"
sudo install -d -m 0750 -o www-data -g adm "/var/log/er-stage/$PROJECT"
sudo install -m 0644 "$STATE/nginx-https.conf" "/etc/nginx/sites-available/$PROJECT-https.conf"
sudo ln -s "/etc/nginx/sites-available/$PROJECT-https.conf" "/etc/nginx/sites-enabled/$PROJECT-https.conf"
test ! -e "/etc/logrotate.d/$PROJECT"
sudo install -m 0644 "$STATE/nginx-logrotate.conf" "/etc/logrotate.d/$PROJECT"
sudo nginx -t
sudo systemctl reload nginx
```

Specificare ACME_EMAIL e registrare il digest Certbot usato. La copia riguarda soltanto i nuovi certificati di staging; i vecchi non vengono letti né sostituiti. Il rinnovo di questo certificato isolato, il rinnovo SQL (server cert valido 90 giorni) e l'eventuale automazione richiedono una procedura dedicata: non installare cron che rinnovi/copi i certificati attuali. Se i check Nginx falliscono, non fare reload; conservare e diagnosticare la sola aggiunta. Non usare `certbot --nginx`, che può riscrivere le configurazioni esistenti.

## 6. Smoke reali e raccolta prove

```bash
curl --fail --show-error "https://$DOMAIN/health/live"
curl --fail --show-error "https://$DOMAIN/health/ready"
sudo python3 infra/staging/host.py --state "$STATE" smoke
sudo python3 infra/staging/host.py --state "$STATE" verify
sudo python3 infra/staging/host.py --state "$STATE" logs
sudo cat "$STATE/images.json" "$STATE/ops/database-identity.json" \
  "$STATE/ops/preflight-result.json" "$STATE/ops/transport-result.json"
sudo ls "$STATE/ops/"http-smoke-*.json
sudo tail -n 100 "/var/log/er-stage/$PROJECT/access.log"
```

Smoke verifica HTTPS pubblico senza disabilitare CA/hostname, assenza di redirect inattesi, HSTS, sovrascrittura header proxy, login reale, firme JWT HS256/issuer/audience/purpose/exp, Selection→InstitutionAccess, firma alterata 401, separazione dei purpose, membership estranea 403, classi/esami/submission estranei 404, vecchie route ritirate, CRUD classe, publish/start/autosave/submit/replay e voto ufficiale, filtri tenant e CORS. Scrive solo dati sintetici in questo staging; ogni run conserva un report nuovo. Non stampa password/token/body sensibili. Non copre Microsoft login o nuovi frontend non ancora configurati.

Se HTTPS readiness redirige o manca HSTS, verificare l'IP sorgente del proxy rispetto al gateway atteso: non risolvere aggiungendo reti intere o disabilitando HTTPS. `verify` controlla health Docker, binding esatto loopback, MySQL senza host ports e UID reale di PID 1 (1654/999). La health API interna usa il loopback fidato per evitare di confondere la redirezione HTTPS con readiness positiva. Health unhealthy **non implica** riavvio automatico: unless-stopped riavvia processi terminati, non fa rollout o riparazioni.

Log Docker limitati a 5×10MB per container; log Nginx nuovi separati dal vecchio glob e ruotati 7 giorni con copytruncate senza segnali/restart al servizio. Il log accessi non include query string, header o body. Conservare lo stato privato e i log operatore come materiale sensibile; non caricare journal/seed/secret grezzi negli artifact pubblici.

## 7. Arresto e ripristino non distruttivi

```bash
cd "$SOURCE"
sudo python3 infra/staging/host.py --state "$STATE" stop
# Volume, secret, journal e reti restano; Nginx non viene toccato.
# Solo la vhost staging risponderà 502 finché l'API è ferma.
sudo python3 infra/staging/host.py --state "$STATE" restart
sudo python3 infra/staging/host.py --state "$STATE" verify
sudo python3 infra/staging/host.py --state "$STATE" smoke
```

Restart usa gli stessi artifact congelati, riprende SQL e API, ripete preflight, non ripete bootstrap né seed e non cambia Nginx. Non sono presenti comandi down -v, rm, prune, reset o clean. Backup/restore dati, downgrade di schema o rollback verso un'altra revisione non sono un restart: fermare lo staging e predisporre un clone/nuovo volume autorizzato, senza sovrascrivere il volume corrente. Conservare anche chiavi/credenziali dello stato per accedere al volume ripristinato. Nessuna procedura automatica elimina o sovrascrive dati per "ripartire pulito".

## Criterio di esito

`host.py --state "$STATE" diagnostics` pubblica la sezione JSON `provisioning`:
fase (`precheck`, `bundle-generation`, `account-creation`, `migrations`, `preflight`),
passo SQL, exit code del comando fallito, errno MySQL, SQLSTATE e riga quando disponibili.
Un campo `null` indica che il provider non lo ha restituito. Per il preflight,
`preflight-query-N` identifica la chiamata SQL in ordine nel verificatore.
`startedSteps` elenca solo i nomi canonici dei passi ancora `started` nel journal.
Le eccezioni JavaScript del preflight emettono `ER_PREFLIGHT_FAILURE` e il collector
pubblica `failure.check` con `id`, `category` e `object`. Gli oggetti sono filtrati
contro i contratti versionati; messaggi, stack, SQL e valori DEFINER non sono pubblicati.
I mismatch DDL/body includono solo `difference` (`ddl` o `body`) e gli SHA256
`expectedHash`/`actualHash` dopo le normalizzazioni previste dal contratto.
Gli identificatori distinguono configurazione, identità DB, caricamento contratti,
DDL delle tabelle base/additive, SQL mode e charset/collation dei trigger base,
DDL dei trigger base/additivi, metadata dei trigger additivi, inventario trigger,
metadata/body della routine, DEFINER di ciascuna routine/trigger, esistenza
dell'account proprietario e risultato del probe metadata `er_schema_ready`.
Un probe metadata negativo resta fail-closed; il suo risultato booleano non
permette di attribuire il fallimento a una singola riga information_schema.
La normalizzazione MySQL 8.4 del charset esplicito di colonna è motivata da
[`store_create_info`, sorgente mysql-8.4.13](https://github.com/mysql/mysql-server/blob/mysql-8.4.13/sql/sql_show.cc):
`column_has_explicit_collation` forza la stampa di `CHARACTER SET`. La regola
copre esclusivamente il prefisso di tipo delle colonne con la collation
versionata `utf8mb4_unicode_ci`, senza modificare default/commenti o accettare
altre collation. Questa verifica statica e i test simulati non certificano
il superamento del preflight Docker, né identificano con certezza il primo
mismatch della run 41129473750: la diagnostica precedente non lo conservava.
Il collector proietta questi campi da `operation-failure.json` e `journal.json`;
non legge `errors.log`, SQL, messaggi del provider o traceback. Lo stato diagnostico
del comando precedente viene rimosso dopo aver acquisito il lock operatore.
Un passo parziale resta bloccato anche dopo il riavvio: conservare le prove private
per la diagnosi manuale, senza cancellare journal o bundle e senza ripetere DDL.

La preparazione locale non è un PASS Docker/TLS/EC2. Prima di dichiarare lo staging collaudato servono run Actions della SHA pubblicata, build e startup reali, driver TLS positivo/negativi, preflight/UID/binding corretti e smoke HTTPS completi attraverso l'Nginx host. Questo runbook non assegna READY Production né riapre gli audit conclusi.

### Audit completo MySQL 8.4.13

`provision` esegue `release-preflight.cjs --audit` dopo tutti i passi SQL del
journal e prima del preflight finale. Stessi contratti e confronti del preflight,
con raccolta delle differenze indipendenti; qualsiasi differenza blocca il gate.
Un journal parziale continua a impedire la riesecuzione delle migrazioni.

Copertura: inventario esatto di 71 tabelle, 180 trigger e una procedura (nessuna
funzione); SHOW CREATE delle 71 tabelle e dei 174 trigger storici; corpi dei sei
trigger additivi; evento, timing, tabella target, SQL mode, charset/collation e
DEFINER; corpo, sicurezza, accesso SQL e metadati di creazione della routine;
esistenza del proprietario; 26 colonne, 18 parti di indice, sette FK e 13 colonne
FK additive; clausole strutturali storiche tramite DDL e prerequisiti readiness.
I contatori e timestamp volatili non fanno parte del contratto.

L'audit richiede lo staging Docker temporaneo, il suo identificatore nel database,
MySQL 8.4.13, er_review non-root e TLS VERIFY_IDENTITY. Esegue solo SELECT/SHOW:
valuta il SELECT readiness versionato senza chiamare una routine potenzialmente
alterata. Il report `ops/schema-audit.json` e le righe pubblicate contengono solo
category, id, object, difference, expectedHash e actualHash. Nessun valore SQL o
messaggio del provider viene incluso. Un guasto di identita/configurazione blocca
subito; gli errori di lettura successivi sono raccolti senza interrompere gli
altri controlli indipendenti.

La run 41134588468 (7973f844) ha mostrato l'hash
5039D672316C43F43964EDA8185A6D74785CFB8E7B922FE84DAB617F0928AF4D per
calendar_assignment_binding_bu. Il test offline riproduce esattamente questo
SHA256 e verifica i 21 header storici FOR EACH ROW seguiti da newline e BEGIN:
solo l'atteso 8.4 usa uno spazio. Corpo e artefatti 5.7 restano immutati.
Il test simulato comprende differenze simultanee e i negativi per corpo, evento,
timing, target, SQL mode, collation e DEFINER. La prova reale e il gate Docker
GitHub Actions: i test offline non attestano un PASS del server provisionato.

### Immutabilita delle immagini dopo la build

`compose.yaml` e esclusivamente runtime: nessuna direttiva build o additional_contexts, pull_policy never per tutti i servizi. Solo `host.py build` carica anche `compose.build.yaml`, che mantiene il collegamento OPS/API per copiare gli stessi artefatti. I comandi operativi non caricano mai l'overlay. `up` usa --no-build --pull never; `run` usa --pull never (Compose v5.6.1 non espone --no-build per run).

Gli ID vengono controllati prima e dopo ogni invocazione Compose operativa, anche quando il processo fallisce; una sostituzione blocca il comando prima del passo successivo. images.json resta invariato. Un'immagine mancante non viene scaricata o ricostruita. Lo stato con drift gia rilevato resta bloccato: non riscrivere images.json e non ritaggare per aggirarlo; dopo pubblicazione della correzione usare un nuovo stato isolato attraverso il processo ordinario. Il vecchio stato e volume restano intatti.

Il workflow confronta Compose del runner e v5.6.1 scaricata dalla release ufficiale con controllo checksum, registra Engine/flag, verifica anche preflight esplicito, restart e verify. Ubuntu/Engine del runner non sono una replica esatta di Ubuntu 26.04/Engine 29.9.2: questa differenza resta visibile nei log e richiede un runner effimero equivalente per una riproduzione esatta.
