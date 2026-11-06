# Release su EC2 di staging.consoliprojects.dev

Procedura per sostituire la versione `er-stage-14d2fd709a09` (commit `8e7d9ecd`) con il commit
**`__SHA__`** e popolare i dati demo. Si esegue dalla console AWS (Session Manager), **una fase alla volta**:
dopo ogni fase controllare l'ultima riga, che è sempre `ESITO <fase>: OK` oppure `ESITO <fase>: FAIL`.
Con `FAIL` fermarsi e inviare il log.

Lo script è `infra/staging/release-ec2.sh` (la logica è in `release_ec2.py` e `demo_seed.py`, stessa cartella).
Ogni fase è idempotente: rilanciarla dopo un'interruzione riprende da dove si era fermata.
La vecchia versione viene solo fermata (`docker compose stop`): volume, immagini, vhost e certificato restano.

## 0. Sessione

La shell di Session Manager è `sh`. All'inizio di **ogni** sessione, anche dopo una caduta, incollare:

```sh
REV=__SHA__
L=/srv/electronicregister-staging/launcher-$REV
LOG=/srv/electronicregister-staging/release-$REV/logs
```

Solo la prima volta, scaricare lo script al commit esatto (git verifica il contenuto tramite lo SHA):

```sh
sudo test ! -e "$L" && sudo git clone --quiet --no-checkout https://github.com/xxdema10xx/ElectronicRegister.git "$L"
sudo git -C "$L" -c advice.detachedHead=false checkout --quiet --detach "$REV"
sudo git -C "$L" rev-parse HEAD
```

Deve stampare esattamente lo SHA. `~/ElectronicRegister` e `~/tmp` non vengono toccati.

## 1. check (sola lettura, ~1 minuto)

```sh
sudo bash "$L/infra/staging/release-ec2.sh" check
```

Atteso: righe `OK` per architettura, Docker 29.9.2, Compose 5.6.1, Python 3.14.4, spazio, RAM, vecchia
versione healthy su 127.0.0.1:18080, nginx -t, vhost vecchie attive, HTTPS pubblico, certificato
(SAN `staging.consoliprojects.dev`, scadenza 2027-01-28, chiave corrispondente, uguale a quello servito),
porta prevista `18081`, subnet prevista `172.30.253.0/24`, le 5 CI del commit verdi. Poi `ESITO check: OK`.

Fermarsi se c'è un `FAIL`. Un `WARN` sulla CI non raggiungibile si accetta solo dopo aver verificato a mano
le run su GitHub.

## 2. backup (~1 minuto)

```sh
sudo bash "$L/infra/staging/release-ec2.sh" backup
```

Atteso: `backup completato - /srv/electronicregister-staging/backups/<UTC>-pre-<sha12>` e `ESITO backup: OK`.
Contiene sites-available e sites-enabled (tar con i link), `nginx -T`, docker ps/inspect della vecchia versione
(variabili sensibili omesse), volumi, reti, immagini e le impronte SHA-256 del certificato. Nessuna chiave privata.

## 3. deploy (lungo: 15-25 minuti)

Lanciarlo staccato dalla sessione e seguire il log:

```sh
sudo setsid nohup bash "$L/infra/staging/release-ec2.sh" deploy > /dev/null 2>&1 < /dev/null &
sleep 3; sudo tail -F "$LOG/latest-deploy.log"
```

Uscire da `tail` con Ctrl+C quando compare `ESITO deploy: ...`. Il deploy:

- clona il commit in `/srv/electronicregister-staging/source-$REV` e crea lo stato `state-$REV-<UTC>`;
- esegue host.py `prepare` (namespace, credenziali, volume e rete nuovi, porta `18081`), `config`, `build`, `db`,
  `sql-check`, `provision`, `compatibility`, `release-preflight`, `tls`, `seed`, `preflight`, `start`, `verify`;
- copia il certificato attivo in `/etc/nginx/<nuovo-namespace>/versions/<stesso nome>/` con verifica SHA-256
  e gli stessi permessi, senza toccare `/etc/nginx/er-stage-14d2fd709a09/`.

Atteso alla fine: `vecchia versione ancora healthy`, `HTTPS pubblico invariato durante il deploy`, il riepilogo
memoria con `MemAvailable` di almeno 768 MiB, `ESITO deploy: OK`. Il sito pubblico è ancora servito dalla vecchia versione.

## 4. switch (~1 minuto)

```sh
sudo bash "$L/infra/staging/release-ec2.sh" switch
```

Installa in sites-available le due vhost generate da host.py, disattiva `er-stage-14d2fd709a09-http.conf` e
`-https.conf` in sites-enabled e attiva le nuove; `nginx -t`, reload, controllo HTTPS (la richiesta deve comparire
nell'access log del nuovo namespace). Se `nginx -t` o il controllo HTTPS falliscono **rimette da solo i link vecchi**:
in quel caso compare `link vecchi ripristinati` ed `ESITO switch: FAIL`; il sito resta sulla vecchia versione.
Se tutto va bene ferma la vecchia versione con `docker compose stop`. Atteso: `HTTPS pubblico ancora servito dalla
nuova versione` ed `ESITO switch: OK`. `default`, `nginx.conf` e gli altri file nginx non vengono modificati.

## 5. seed-demo (8-12 minuti, l'API accetta 60 scritture al minuto)

```sh
sudo setsid nohup bash "$L/infra/staging/release-ec2.sh" seed-demo > /dev/null 2>&1 < /dev/null &
sleep 3; sudo tail -F "$LOG/latest-seed-demo.log"
```

Atteso, per ciascuno dei due istituti: istituto e primo admin, 2 admin/1 Staff/3 Teacher, struttura, 12 studenti
importati, calendario, 3 esami; poi `demoSeed: OK` ed `ESITO seed-demo: OK`. Rilanciarlo non duplica nulla.

## 6. verify (~3 minuti)

```sh
sudo bash "$L/infra/staging/release-ec2.sh" verify
```

Esegue `/health/ready` via HTTPS pubblico, `host.py verify`, lo smoke HTTPS pubblico (`host.py smoke`, 54 casi,
eseguibile **una sola volta** per namespace: ai rilanci viene riletto l'esito salvato) e la verifica dei dati demo,
compreso l'isolamento fra i due istituti. Atteso: `demoVerify: OK` ed `ESITO verify: OK`.

## Se la sessione cade

Riaprire la console, incollare il blocco del punto 0 (le tre variabili) e poi:

```sh
pgrep -af release_ec2.py
sudo tail -n 50 "$LOG/latest.log"
```

Se `pgrep` mostra ancora la fase, seguirla con `sudo tail -F "$LOG/latest.log"`. Se non c'è e manca la riga `ESITO`,
la fase è stata interrotta: rilanciare la stessa fase. Due fasi non possono girare insieme (lock).
I log restano tutti in `$LOG/` (`<fase>-<UTC>.log`).

## Rollback

Funziona anche dopo un deploy o uno switch interrotti a metà:

```sh
sudo bash "$L/infra/staging/release-ec2.sh" rollback
```

Riavvia la vecchia versione con `docker compose start` e aspetta che sia healthy, rimette i link
`er-stage-14d2fd709a09-*`, toglie quelli nuovi, `nginx -t`, reload, controlla che l'HTTPS pubblico sia servito
dalla vecchia versione e infine ferma la nuova (dati conservati). La vecchia versione viene avviata **prima** di
cambiare i link, così il sito non resta mai senza backend. Atteso: `ESITO rollback: OK`.
Per tornare alla nuova versione basta rilanciare `switch`.

## Credenziali demo

```sh
sudo cat /srv/electronicregister-staging/release-$REV/demo/credenziali-demo.txt
```

File `0600` in una cartella `0700`, fuori da ogni checkout: istituto, ruolo, nome, email e password finale
(le password temporanee sono già state cambiate). Il JSON completo è in `credentials.json` nella stessa cartella.
Istituti: **Istituto Dimostrativo Aurora (dati finti)** `DEMO_AURORA` e **Istituto Dimostrativo Boreale (dati finti)**
`DEMO_BOREALE`, email `@aurora.example.invalid` e `@boreale.example.invalid`.

Per ciascun istituto: 2 InstitutionAdmin (il secondo creato dal primo via API), 1 Staff, 3 Teacher, 2 classi da
6 Student (import con `EmailMode=None`, poi `POST /api/institution/accounts`), biennio/area/percorso, 3 materie,
6 assegnazioni docente-materia-classe, impostazioni del calendario, 3 esami pubblicati con 5 consegne valutate
su 6 (il sesto studente ha l'esame ancora aperto) e i relativi voti ufficiali.

**Non creato:** lezioni del calendario. Richiedono un piano formativo pubblicato e collegato alla classe, cioè il
flag `FormativePlans`, spento in questo staging. Nessun dato è stato inserito via SQL: solo OwnerProvisioning per
istituti e primo admin.

## Configurazione della nuova versione

Generata da `host.py prepare --enable-import` in `state-$REV-<UTC>/public/runtime.json`:

| Chiave | Valore |
|---|---|
| `ASPNETCORE_ENVIRONMENT` / `Deployment:Mode` | `Staging` |
| `Deployment:RolloutApproved` | `true`, scelta esplicita per questo namespace con dati finti |
| `Readiness:ExpectedVersion` | `8.4.13` (MySQL `mysql:8.4.13@sha256:5cf1e129…`) |
| Connessione API | utente `er_api`, `SslMode=VerifyFull`, TLS 1.2, CA privata dello stato |
| `Transport:KnownProxies` | gateway della nuova rete (`172.30.253.1` se la subnet è `172.30.253.0/24`) e `127.0.0.1` |
| `AllowedHosts` | `staging.consoliprojects.dev` |
| `Transport:AllowedOrigins` | `https://consoliprojects.dev` |
| Flag attivi | `SchoolApi`, `CourseApi`, `ExamApi`, `MembershipAdminApi`, `MembershipWriteApi`, `InstitutionCalendar`, `InstitutionImport` |
| Flag spenti | `Onboarding`, `Governance`, `InstitutionWorker`, `FormativePlans`, `InstitutionEmail`, legacy identity |

`InstitutionImport` è l'unica differenza rispetto al runtime di staging precedente: serve per la procedura
documentata di caricamento delle classi. Senza `--enable-import` host.py lo lascia spento come prima.

## Certificato di staging: scadenza 2027-01-28

Il certificato (Let's Encrypt, CN e SAN `staging.consoliprojects.dev`) scade il **2027-01-28T23:41:01Z** e **non si
rinnova da solo**: `certbot.timer` rinnova solo `consoliprojects.dev`. Rinnovarlo entro metà gennaio 2027
(`check` fallisce sotto i 14 giorni). Con `NS` = nuovo namespace (`sudo cat /srv/electronicregister-staging/release-$REV/deploy.json`):

```sh
NS=er-stage-xxxxxxxxxxxx
sudo certbot certonly --webroot -w /var/lib/er-stage-acme/$NS -d staging.consoliprojects.dev --cert-name staging.consoliprojects.dev
S=/etc/letsencrypt/live/staging.consoliprojects.dev
V=$(sudo cat $S/fullchain.pem | sha256sum | cut -c1-64)
sudo install -d -m 0700 /etc/nginx/$NS/versions/$V
sudo install -m 0644 $S/fullchain.pem /etc/nginx/$NS/versions/$V/fullchain.pem
sudo install -m 0600 $S/privkey.pem /etc/nginx/$NS/versions/$V/privkey.pem
sudo ln -sfn versions/$V /etc/nginx/$NS/active
sudo nginx -t && sudo systemctl reload nginx
sudo bash "$L/infra/staging/release-ec2.sh" check
```

La vhost HTTP del namespace serve già `/.well-known/acme-challenge/` da `/var/lib/er-stage-acme/$NS`; il modo
`--webroot` non modifica la configurazione nginx. La versione precedente resta in `versions/` per tornare indietro
(`ln -sfn versions/<vecchio> active`, `nginx -t`, reload). Da quel momento `certbot.timer` rinnova anche questo
certificato in `/etc/letsencrypt`, ma la copia in `/etc/nginx/$NS/versions` va ripetuta a ogni rinnovo (oppure
aggiungendo un deploy hook dedicato).
