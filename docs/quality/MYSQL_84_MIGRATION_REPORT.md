# ElectronicRegister — migrazione staging MySQL 8.4 LTS

Data: 30 ottobre 2026. Branch: `refactor-database-entities`.
Checkout iniziale: `572ca15b3e6544ff0ebe67c77988b91a0d5b36c7`.

**Esito: implementazione predisposta, controlli locali verdi; certificazione Docker,
SQL/TLS/HTTP runtime ancora aperta su GitHub Actions. EC2 non autorizzata.**
Nessun commit, push, collegamento EC2, modifica Nginx o accesso a database/volumi
preesistenti. I due file patch non tracciati già presenti sono stati preservati.

## Immagine verificata

```text
mysql:8.4.13@sha256:5cf1e1293ea42e077e6a22114e6773b9aae3bb8f9933a2acd5a9721518ff8f5b
platform: linux/amd64
UID/GID mysql: 999:999
```

La patch corrente verificata è 8.4.13, release del 20 ottobre 2026:
[release notes Oracle](https://dev.mysql.com/doc/relnotes/mysql/8.4/en/news-8-4-13.html).
Sono stati interrogati direttamente `registry-1.docker.io/library/mysql`, tag
`8.4` e `8.4.13`: entrambi contenevano lo stesso manifest amd64. SHA256 verificati
sui byte di manifest/config/layer letto. Il digest fissato è il manifest amd64,
non il digest multiarch e non un tag mobile.

[Evidenza registry](staging-evidence/mysql-8.4-registry.json) conserva digest,
piattaforma, annotazioni, revisione sorgente ufficiale, config e history pertinenti.
Dal layer di creazione dell'utente sono stati letti realmente `etc/passwd` e
`etc/group`: `mysql:x:999:999::/var/lib/mysql:/bin/bash`, `mysql:x:999:`.
L'origine è il [Dockerfile ufficiale revisionato](https://github.com/docker-library/mysql/blob/c92fbc392511086e6c1500f28a21421413f0bb51/8.4/Dockerfile.oracle),
base Oracle Linux 9 slim. Datadir e socket dell'immagine sono stati confrontati
con il Dockerfile: `/var/lib/mysql`, `/var/run/mysqld/mysqld.sock`.

Queste sono evidenze statiche, non esecuzione del processo. Il job Docker ripete
`id -u/-g mysql`, `mysqld --version`, capacità TLS del binario e controlla UID reale
di PID 1. `containersExecuted` e `runtimeTlsVerified` nell'evidenza sono **false**.

## Inventario e adattamenti

Il bootstrap storico descrive 66 tabelle, 174 trigger, 193 FK e 293 indici.
Con 001–005: 71 tabelle, 180 trigger, 200 FK, una procedura, nessuna stored function.

| Area | Riscontro | Correzione / verifica residua |
|---|---|---|
| `mysql.proc` | Incompatibilità certa: rimosso in MySQL 8; bootstrap e grant reviewer lo usavano | Percorso 8.4 usa `information_schema.ROUTINES` e `SHOW_ROUTINE`; variante 5.7 separata conservata |
| `have_ssl`, `have_openssl` | Variabili rimosse in 8.4 | Health/attestation usano `version_ssl_library`, policy TLS e sessione cifrata reale |
| `NO_AUTO_CREATE_USER` | SQL mode rimosso, presente nel bootstrap e nel contratto 005 | Rimosso solo dal bundle derivato 8.4 e dalle corrispondenti asserzioni; altri mode preservati |
| `row_number` | Keyword riservata in 8.x: dichiarazione/PK bootstrap e INSERT applicativo senza quoting | Quoting nel derivato 8.4 e nella singola query `InstitutionImportRepository`; `NEW.row_number` qualificato resta invariato |
| Tipi interi metadata | Display width 5.7 nei contratti | Contratto 8.4 senza width; `tinyint(1)` preservato |
| Timestamp metadata | MySQL 8 espone `EXTRA=DEFAULT_GENERATED` per default espressione | Contratto 8.4 delle tre colonne timestamp aggiornato; default e precisione preservati |
| SHOW CREATE | 8.x può stampare la collation ASCII predefinita esplicita | Normalizzazione limitata a `CHARACTER SET ascii COLLATE ascii_general_ci`; collation differenti continuano a fallire |
| DEFINER / SUPER | Provisioner 5.7 aveva SUPER | Solo `CREATE USER`, `SET_ANY_DEFINER`, `SHOW_ROUTINE`, privilegi DDL/DML enumerati sul solo schema e SELECT su cinque colonne mysql.user |
| Autenticazione | Default 5.7 differente | Login creati esplicitamente `caching_sha2_password REQUIRE SSL`; definer caching_sha2_password ACCOUNT LOCK; nessuna riabilitazione native password |
| Binary log | Default MySQL 8 attivo, rispetto al default 5.7 disattivo; influenza CREATE TRIGGER senza SUPER | `--skip-log-bin` esplicito sulla sola istanza effimera senza replica/PITR; nessun `log_bin_trust_function_creators=1` |
| FK non standard | 8.4 restringe riferimenti non univoci | Controllo statico: tutte le 200 FK hanno chiave referenziata primaria/univoca esatta. Restrizione attestata attiva; controlli FK mai disabilitati |
| Charset/collation | Default MySQL 8 diverso da 5.7 | Schema/sessioni espliciti utf8mb4_unicode_ci; ASCII identificatori e ascii_bin esistenti preservati |
| Trigger e isolamento tenant | Retention include guardie storiche 5.7.24/root/datadir | Guardie e corpi preservati, nessun bypass adattato allo staging. DEFINER comune esplicito, SQL mode/charset/collation verificati |
| EF Database First | Mapping esistente, EF Core 9/Pomelo 9, .NET 10; `ServerVersion.AutoDetect` già presente | Nessun re-scaffold o aggiornamento pacchetti. Build/unit verdi; query EF reali da eseguire nel job HTTP |
| MySqlConnector | 2.6.1; query parametrizzate e transazioni esistenti | Driver/pacchetti invariati. Nuovo probe reale per RR, rollback, dirty read, deadlock e lock timeout; runtime da certificare |
| Health/readiness | Versione 5.7.44 codificata nello staging | Versione esatta 8.4.13 in runtime/preflight/health; marker release e ownership ancora obbligatori |

La rimozione delle variabili TLS è documentata nelle
[modifiche ufficiali 8.4](https://dev.mysql.com/doc/refman/8.4/en/mysql-nutshell.html).
Il campo `DEFAULT_GENERATED` è documentato nel
[contratto INFORMATION_SCHEMA.COLUMNS](https://dev.mysql.com/doc/refman/8.4/en/information-schema-columns-table.html).
La dipendenza di CREATE TRIGGER dal binary log è descritta nel
[manuale stored program logging](https://dev.mysql.com/doc/refman/8.4/en/stored-programs-logging.html).

Le differenze metadata sopra sono adattamenti statici revisionati: **non si
dichiara ancora dimostrata l'equivalenza di tutti i SHOW CREATE reali 8.4.13**.
Ulteriori differenze non contemplate faranno fallire il preflight, senza imparare
automaticamente attese dal database o saltare asserzioni.

## Tracciabilità SQL e sicurezza

`tools/mysql84-contract.cjs` contiene regole versionate; il binding richiede
`mysqlSeries: "8.4"`. `prepare-release-sql.cjs` verifica gli hash dei sei input
canonici prima di produrre bootstrap + 001–005. Registra separatamente hash del
sorgente storico e hash dell'artifact derivato. La 005 viene rigenerata dal
contratto revisionato per includere mode/tipi/EXTRA 8.4, senza perdere conteggi,
FK, indici, corpi trigger o proprietà SQL SECURITY DEFINER / READS SQL DATA.

Gli script storici in `migrations/bootstrap` e `migrations/identity-account`,
i relativi contratti, manifest storici e test Integration 5.7.24 non sono stati
riscritti. La variante senza `mysqlSeries` conserva il comportamento 5.7, incluso
il relativo provisioning metadata. La correzione C# di quoting è compatibile anche 5.7.

Il definer è separato e bloccato al login. L'API riceve esclusivamente
SELECT/INSERT/UPDATE/DELETE sullo schema ed EXECUTE sulla routine. Il reviewer
riceve SELECT/TRIGGER sullo schema, EXECUTE sulla routine, SELECT(User,Host) su
mysql.user e SHOW_ROUTINE globale per leggere le definizioni; nessun DDL/DML
amministrativo. Migratore e reviewer non sono montati nel container API.
Il migratore non ha SUPER, FILE, SYSTEM_USER o privilegi amministrativi generici.

Il journal registra `started` prima di ogni passo e `done` solo al successo.
Passo parziale o hash diverso bloccano l'esecuzione. Il workflow ripete il
provisioning completato per verificare lo skip senza replay DDL. MySQL 8 offre
atomic DDL per operazione, non una transazione per l'intero bundle: il journal
rimane necessario.

Stato formato 3 con versione/digest esatti; nuovi UUID, progetto, subnet, rete,
volume, schema, password e PKI. Stato 5.7 rifiutato prima dei comandi Docker.
Il volume non vuoto è accettato solo per riavviare la stessa istanza con marker
`8.4.13:<UUID>`, scritto al termine del bootstrap. Vecchio datadir o bootstrap
parziale senza marker vengono rifiutati, senza upgrade o riparazione automatica.

MySQL resta non-root 999, API 1654, ops 10001. Solo l'inizializzatore del volume
nuovo è root temporaneamente, senza rete. Nessuna porta SQL pubblicata; protocollo
X disattivato. API pubblicata solo su loopback, filesystem readonly, capability
rimosse e `no-new-privileges` preservati.

## TLS: diagnosi e limiti

TLS 1.2, REQUIRE SSL / require_secure_transport e driver `SslMode=VerifyFull`
restano obbligatori. CA e hostname sono verificati anche dal client SQL
`VERIFY_IDENTITY`. Le CA generate dichiarano ora esplicitamente CA:TRUE e key
usage per la firma, senza dipendere dalla configurazione OpenSSL dell'host.

È stato confermato in sola lettura il fallimento del run
[41116350436](https://github.com/xxdema10xx/ElectronicRegister/actions/runs/41116350436)
sulla SHA iniziale. Il connettore non ha restituito i log del job: **la causa
esatta del precedente probe 5.7.44 non è dimostrata qui**. Il passaggio a 8.4 non
viene considerato una soluzione già verificata.

Il probe ora distingue handshake positivo, protocollo/schema e regressione SQL.
In caso di errore scrive `transport-failure.json` con fase, tipi, HResult, numero
MySQL ed errori certificate policy; nessuna connection string o messaggio grezzo.
La diagnostica sanitizzata raccoglie questo file e anche i log API.

I negativi vengono eseguiti solo dopo il positivo. Non basta un generico
AuthenticationException: sono richiesti `RemoteCertificateNameMismatch` per
l'alias raggiungibile sbagliato e `RemoteCertificateChainErrors` per la CA diversa.
Il controllo segue il comportamento documentato nel
[sorgente MySqlConnector 2.6.1](https://github.com/mysql-net/MySqlConnector/blob/2.6.1/src/MySqlConnector/Core/ServerSession.cs).
DNS, timeout, rete indisponibile o fallimento del positivo non producono PASS.
Nessun callback permissivo o downgrade TLS è stato introdotto.

## Matrice esecuzioni effettive

| Controllo | Esito effettivo locale |
|---|---|
| Registry ufficiale, patch, SHA256, linux/amd64 | PASS statico |
| UID/GID da layer passwd/group | PASS statico; processo da verificare su Docker |
| Restore solution `--locked-mode` | PASS con feed locale dei pacchetti locked |
| Build solution Release `--no-restore` | PASS; prima build completa 10 warning nullable Integration già presenti, 0 errori; rebuild finale 0 warning/errori |
| Unit .NET Release | PASS: 598/598, 0 falliti/ignorati |
| CodeOrganization `check .` | PASS: 660 file, 657 tipi, 0 violazioni |
| Restore locked/build ReleaseTransportProbe | PASS, build 0 warning/errori |
| Restore locked/build StagingSupport | PASS, build 0 warning/errori |
| Python `unittest discover -s tools -p test_*.py` | PASS: 36 test |
| `node tools/test-mysql84.cjs` | PASS: adattatore, input/bundle hash, corpi trigger, indici/FK, quoting, negativi semantici; 200 FK standard |
| Manifest tool ufficiale `--write`, poi verifica | PASS: 756 artifact canonici, invarianti LF/CRLF |
| `node tools/test-release-manifest.cjs` | PASS: 14 negativi sul verificatore reale |
| `verify-project-artifacts.py` | PASS |
| `bash -n` bootstrap/health e py_compile staging | PASS |
| actionlint 1.7.12 sui due workflow | PASS con label self-hosted storica dichiarata nella config locale; shellcheck/pyflakes non disponibili |
| Bootstrap SQL nuovo MySQL 8.4.13 | NON ESEGUITO: Docker non disponibile |
| Migrazioni ordinate, schema/trigger/routine/DEFINER/account reali | NON ESEGUITI localmente; gate predisposto |
| TLS MySqlConnector positivo e negativi CA/hostname | NON ESEGUITI localmente; gate predisposto |
| Transazioni, deadlock, lock timeout e DDL API negato | NON ESEGUITI localmente; probe reale predisposto |
| HTTP IAM/membership/tenant/Exam/Submission/Grade/calendario | NON ESEGUITI localmente; smoke loopback predisposto |
| API healthy, readiness 200, UID processo, porte Docker | NON ESEGUITI localmente; gate predisposto |
| Suite Integration storica MySQL 5.7.24 | PRESERVATA, NON ESEGUITA; nessun accesso a DB esistenti |
| GitHub Actions su questa patch | NON ESEGUITO: nessun commit/push; nuova SHA non pubblicata |

Il client NuGet Windows inizialmente non poteva leggere la configurazione utente
e falliva TLS verso nuget.org. La verifica locale ha usato configurazione/cache
nel workspace e pacchetti scaricati tramite HTTPS validato da Node dal feed
ufficiale, poi restore locked da feed locale. Non sono stati alterati lockfile,
trust di sistema, pacchetti del progetto o policy CI. Questo non certifica
l'accesso HTTPS NuGet del client Windows né la connessione SQL TLS.
Python embedded/actionlint e cache sono isolati in `.migration-tools`, ignorata.

## GitHub Actions e condizioni EC2

`staging-package.yml` mantiene checkout SHA = workflow SHA = revision label
immagine API, manifest canonico e immagine ops derivata dall'artifact API.
Il controllo source/SQL confronta anche il manifest impacchettato. Il runner
Ubuntu opera solo su daemon locale, namespace casuale, senza credenziali AWS/SSH.

Sequenza predisposta: controlli manifest/negativi/adattatore; build API/ops;
nuovo MySQL healthy; provisioning ordinato; compatibilità e account caching SHA2;
secondo provisioning idempotente; TLS/SQL regression; seed sintetico; preflight;
avvio API; UID/porte/readiness; smoke HTTP reale. Il probe SQL usa due connessioni
API e transazioni rollback su marker di release senza modificarne i valori,
richiede esattamente un deadlock 1213 e un lock timeout server 1205. Verifica
SHOW GRANTS API e il rifiuto CREATE TABLE 1142.

Lo smoke HTTP loopback usa login reali, verifica firme JWT e scope, visibilità
tenant, lifecycle Exam/Submission/Grade, replay submit/configurazione calendario,
RBAC e CORS. Simula il proxy fidato esclusivamente sul binding loopback;
`httpsCertificateVerified=false` viene dichiarato nel risultato. Non sostituisce
il collaudo pubblico HTTPS/Nginx, che rimane separato.

Prima di autorizzare una successiva fase EC2 sono necessari:

1. Pubblicazione tramite il processo autorizzato del proprietario, senza commit
   automatici di questa attività; source e manifest devono essere nella stessa SHA.
2. Run source Linux/Windows e staging Ubuntu verdi sulla nuova SHA, con tutti i
   gate SQL/TLS/HTTP effettivamente eseguiti e nessun test disabilitato.
3. Revisione dei risultati reali 8.4.13: nessuna differenza schema/trigger inattesa,
   TLS positivo e due rifiuti certificate policy dimostrati, privilegi minimi,
   readiness, non-root e assenza di port binding MySQL.
4. Chiusura dell'eventuale errore TLS evidenziato dalla nuova diagnostica; una
   failure mantiene il gate bloccato. La baseline Integration 5.7.24 mantiene
   il proprio runner/gate indipendente.
5. Autorizzazione separata alla EC2 con nuovo stato/volume, stessa SHA/digest e
   collaudo HTTPS pubblico successivo. Nessuna autorizzazione EC2 è implicita.

Non esiste ancora un PASS Docker/TLS 8.4. Il risultato disponibile è il codice di
staging adattato e verificato localmente, pronto per il collaudo reale del workflow.
