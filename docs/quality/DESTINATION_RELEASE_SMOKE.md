# Collaudo della destinazione Docker/TLS

Questi comandi preparano il successivo collaudo autorizzato su rete e database **isolati e nuovi**. Non sono stati eseguiti qui: Docker non è disponibile e MySQL MAMP non offre il protocollo comune necessario. Non costituiscono autorizzazione a deployment EC2 o a migrazioni su un database esistente. Nessun certificato, livello OpenSSL o controllo VerifyFull deve essere indebolito.

## Prerequisiti

- Docker Engine e una immagine Nginx revisionata, indicata per digest in `NGINX_IMAGE`.
- MySQL compatibile 5.7.24 con InnoDB, TLS almeno 1.2 e cifrari/catena compatibili con .NET 10/OpenSSL del container. `have_ssl=YES`, TLSv1.2 effettivamente negoziato, SAN del certificato corrispondente a SQL_HOST. Non basta impostare tls_version se have_ssl diventa DISABLED.
- CA PEM valida, certificato corrente, chiave e catena del server corretti; nessun callback permissivo. Nomi di destinazione e CA devono essere gli stessi usati dal container. Il binario MAMP locale non soddisfa questo gate.
- Database nuovo `electronic_register_release_smoke`, `CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci`. Prima delle scritture l'operatore deve verificare via SQL VERSION(), @@hostname, @@port, @@datadir, DATABASE() contro il proprio inventario autorizzato. Non usare il bootstrap su istanze preesistenti.
- Identità migratore separata con privilegi DDL e, in MySQL 5.7, autorità necessaria ad attribuire DEFINER diversi (SUPER). Non passarne le credenziali al container API.
- Account login API e reviewer già creati con credenziali forti e host ammessi ristretti alla rete di collaudo. La procedura sotto crea invece il definer **ACCOUNT LOCK**, che non può autenticarsi.

L'API riceve CRUD sul solo schema ed EXECUTE sulla sola routine. Il definer riceve CRUD/TRIGGER sullo schema: TRIGGER è necessario alla visibilità di information_schema.TRIGGERS. Il reviewer riceve SELECT/TRIGGER sullo schema, SELECT(User,Host) su mysql.user e SELECT su mysql.proc per confrontare le routine in MySQL 5.7. È un'identità temporanea di verifica, distinta dall'API; revocarla/bloccarla dopo il collaudo. L'API non riceve CREATE, ALTER, DROP, TRIGGER, SUPER o GRANT OPTION.

## Artefatti, binding e preflight

Da un checkout della release verificata, su Linux con Node, Python e SDK .NET 10:

```bash
set -euo pipefail
node tools/release-artifacts.cjs
node tools/test-release-manifest.cjs
dotnet restore Backend/ElectronicRegisterAPI/ElectronicRegisterAPI.slnx --locked-mode
dotnet build Backend/ElectronicRegisterAPI/ElectronicRegisterAPI.slnx --no-restore -c Release
dotnet restore tools/ReleaseTransportProbe/ReleaseTransportProbe.csproj --locked-mode
dotnet publish tools/ReleaseTransportProbe/ReleaseTransportProbe.csproj --no-restore -c Release -p:UseAppHost=false -o "$SMOKE_PRIVATE/transport"
docker build -f infra/Dockerfile -t "$IMAGE" .
docker image inspect "$IMAGE" > "$SMOKE_PRIVATE/image-inspect.json"
docker create --name "$ARTIFACT_CONTAINER" --entrypoint /bin/true "$IMAGE"
mkdir "$SMOKE_PRIVATE/extracted"
docker cp "$ARTIFACT_CONTAINER:/release/." "$SMOKE_PRIVATE/extracted/"
docker cp "$ARTIFACT_CONTAINER:/app/ElectronicRegisterAPI.Infrastructure.dll" "$SMOKE_PRIVATE/"
docker cp "$ARTIFACT_CONTAINER:/app/ElectronicRegisterAPI.deps.json" "$SMOKE_PRIVATE/"
cmp migrations/release/manifest.json "$SMOKE_PRIVATE/extracted/migrations/release/manifest.json"
dotnet run --project docs/quality/remediation-evidence/package-tool -- "$SMOKE_PRIVATE/ElectronicRegisterAPI.Infrastructure.dll"
```

Usare nomi IMAGE/ARTIFACT_CONTAINER nuovi e SMOKE_PRIVATE in una directory privata fuori dal contesto di build. Il tool PE controlla le 14 risorse SQL e i byte rispetto al checkout, più il driver nel deps. Non confondere il solo COPY SQL con una prova delle risorse embedded.

Preparare `binding.json` pubblico:

```json
{
  "artifactRoot": "/absolute/private/extracted",
  "database": "electronic_register_release_smoke",
  "definerUser": "er_release_definer", "definerHost": "%",
  "apiUser": "er_api", "apiHost": "REPLACE_RESTRICTED_HOST",
  "reviewUser": "er_review", "reviewHost": "REPLACE_RESTRICTED_HOST"
}
```

Il `%` del definer non è un login aperto: è ACCOUNT LOCK. Gli host dei due login devono essere sostituiti con la rete/host effettivamente autorizzati. Il tool accetta soltanto principal espliciti senza caratteri SQL arbitrari e verifica hash e sequenza dell'input.

```bash
node tools/prepare-release-sql.cjs "$SMOKE_PRIVATE/binding.json" "$SMOKE_PRIVATE/bound-sql"
# Revisionare binding.json, hash sorgente/derivato e tutti gli SQL generati.
# Eseguire soltanto dopo l'attestazione SQL del database NUOVO descritto sopra.
for script in "$SMOKE_PRIVATE/bound-sql/"*.sql; do
  mysql --defaults-extra-file="$MIGRATOR_MY_CNF" --host="$SQL_HOST" --port="$SQL_PORT" \
    --ssl-mode=VERIFY_IDENTITY --ssl-ca="$SQL_CA" --tls-version=TLSv1.2 < "$script"
done
node tools/release-preflight.cjs "$SMOKE_PRIVATE/preflight.json"
```

`MIGRATOR_MY_CNF` è un file privato, non una password nell'argomento CLI. Il file preflight contiene host, port, database, user reviewer, passwordFile privato, caFile, expectedVersion, expectedDataDir, mysqlClient e **schemaDefiner** (`er_release_definer@%`). Il preflight di release impone TLS 1.2 e VERIFY_IDENTITY; `--isolated-test` è riservato alle istanze locali di regressione e non va usato qui.

Il bundle deriva bootstrap e cinque additive, senza alterare gli originali né il manifest storico. I definer storici root/er_test sono sostituiti nel solo artefatto derivato. I file 00 e 99 separano grant preliminari e EXECUTE finali. **005 esegue DROP/CREATE della routine e MySQL elimina i suoi grant specifici: il passo 99 è obbligatorio.** DDL non transazionali: in caso di errore fermarsi, conservare log e diagnosticare; niente replay automatico o bootstrap su dati esistenti. Un eventuale upgrade richiede una procedura distinta autorizzata, non questo ciclo apply-once.

## TLS del driver reale

Creare prima una rete nuova con CIDR libero e nome nuovo. Il comando deve fallire su collisione, senza rimuovere reti esistenti. Il proxy del collaudo userà l'IP esplicito indicato:

```bash
docker network create --subnet 172.30.250.0/24 --label er.release.smoke="$RUN_ID" "$SMOKE_NETWORK"
```

Creare un `transport-probe.json` privato con i campi seguenti (password vera solo nel file):

```json
{
  "connectionString": "Server=mysql.example.test;Port=3306;Database=electronic_register_release_smoke;User ID=er_api;Password=REPLACE_SECRET;SslMode=VerifyFull;SslCa=/run/secrets/mysql-ca.pem;TlsVersion=TLS 1.2",
  "wrongHost": "mysql-wrong.example.test",
  "wrongCaFile": "/run/secrets/unrelated-ca.pem"
}
```

`unrelated-ca.pem` deve essere una CA PEM valida e indipendente, non un file malformato. Il nome sbagliato deve essere assente dal SAN e raggiungere **lo stesso server**, non produrre un errore DNS. SQL_IP è l'indirizzo attestato del server; non un endpoint alternativo.

```bash
docker run --rm --network "$SMOKE_NETWORK" \
  --add-host "mysql-wrong.example.test:$SQL_IP" \
  --mount "type=bind,src=$SMOKE_PRIVATE/transport,dst=/probe,readonly" \
  --mount "type=bind,src=$SMOKE_PRIVATE/sql-probe-secrets,dst=/run/secrets,readonly" \
  --entrypoint dotnet "$IMAGE" /probe/ReleaseTransportProbe.dll /run/secrets/transport-probe.json \
  > "$SMOKE_PRIVATE/transport-result.json"
```

La probe forza TLS 1.2 e VerifyFull, richiede prima connessione positiva, protocollo effettivo e schema ready, poi rifiuto di CA/hostname. Una mancata connessione positiva interrompe il test; errori DNS/socket/timeout nei negativi non valgono come prova del certificato. La classificazione AuthenticationException è coerente con il [driver 2.6.1](https://github.com/mysql-net/MySqlConnector/blob/2.6.1/src/MySqlConnector/Core/ServerSession.cs). Anche questa probe resta **non eseguita su TLS moderno** nell'ambiente MAMP.

## API Production e Nginx

Usare la rete appena creata per la prova TLS, senza modificare reti operative.

Da `infra/release.runtime.example.json` preparare il config revisionato, con hostname/datadir SQL attestati, AllowedHosts/origini effettivi, proxy `172.30.250.3` e ambiente Production. Il template **non abilita** RolloutApproved: impostarlo true solo nel config revisionato del collaudo isolato. Lasciare il worker disabilitato per la prova read-only; i suoi workflow sono coperti dalla regressione SQL separata.

Il config deve mantenere `urls=http://0.0.0.0:8080`: Program usa altrimenti loopback. Preparare file KeyPerFile `ConnectionStrings__DefaultConnection` (VerifyFull, SslCa, credenziali API) e `Jwt__Key` (almeno 48 byte, casuale), oltre a mysql-ca.pem. Montarli in /run/secrets, leggibili dall'APP_UID dell'immagine e non da altri utenti. Non passare secrets tramite build-arg, immagine o argv. Il runtime config usa ER_RUNTIME_CONFIG; le variabili d'ambiente possono sovrascriverlo, quindi controllare anche l'ambiente finale del container.

```bash
docker run -d --name "$API_CONTAINER" --network "$SMOKE_NETWORK" --network-alias er-api \
  -e ASPNETCORE_ENVIRONMENT=Production -e ER_RUNTIME_CONFIG=/run/config/runtime.json \
  --mount "type=bind,src=$SMOKE_PRIVATE/api-config,dst=/run/config,readonly" \
  --mount "type=bind,src=$SMOKE_PRIVATE/api-secrets,dst=/run/secrets,readonly" "$IMAGE"
test "$(docker exec "$API_CONTAINER" id -u)" != 0
docker exec "$API_CONTAINER" cat /proc/1/status > "$SMOKE_PRIVATE/api-process-status.txt"
docker run -d --name "$PROXY_CONTAINER" --network "$SMOKE_NETWORK" --ip 172.30.250.3 \
  -p 127.0.0.1:8443:443 \
  --mount "type=bind,src=$PWD/infra/nginx.release.conf,dst=/etc/nginx/conf.d/default.conf,readonly" \
  --mount "type=bind,src=$SMOKE_PRIVATE/nginx-secrets,dst=/run/secrets,readonly" "${NGINX_IMAGE:?reviewed digest required}"
for endpoint in live ready; do
  code=$(curl --silent --show-error --cacert "$API_CA" \
    --resolve api.example.test:8443:127.0.0.1 --output "$SMOKE_PRIVATE/$endpoint.json" \
    --write-out '%{http_code}' "https://api.example.test:8443/health/$endpoint")
  test "$code" = 200
done
```

Il Nginx di esempio usa api.example.test: sostituirlo insieme a SAN, curl, AllowedHosts e certificati. I file api-fullchain.pem/api-key.pem sono TLS veri per il nome API. Registrare anche UID di PID 1, immagine/ID, stato, log redatti, protocollo SQL e risorse. Il Dockerfile non contiene HEALTHCHECK: configurare queste due probe nell'orchestratore con liveness separata dalla dipendenza SQL.

Ripetere in container API **separati** con CA SQL errata e alias hostname errato: liveness 200 e readiness 503, senza dettagli sensibili. Provare schema incompleto e EXECUTE revocato soltanto su un ulteriore database sacrificabile. Verificare CORS con OPTIONS per origine permessa e origine estranea; la seconda non deve ricevere Access-Control-Allow-Origin. Non abilitare ASPNETCORE_FORWARDEDHEADERS_ENABLED né fiducia indiscriminata nei proxy: usare soltanto KnownProxies.

Prima della promozione occorrono inoltre i job source GitHub Actions realmente verdi su **Linux e Windows**, audit online non vuoto e il job SQL manuale sul runner dedicato. I comandi locali sono evidenza locale, non esecuzione di Actions. Al termine conservare gli artefatti e fermare/rimuovere soltanto i container e la rete appena creati, verificandone nomi/label; nessun teardown di risorse preesistenti. Nessun comando di deployment EC2 è incluso.
