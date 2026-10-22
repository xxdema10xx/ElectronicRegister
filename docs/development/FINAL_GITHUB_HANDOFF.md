# Final GitHub handoff after MT-6

Verifica locale del 22 ottobre 2026. Branch `refactor-database-entities`, HEAD
`7f0fed3897c225af5fdf3861974930a41122582f`.
Nessuno stage, commit, push, reset, checkout distruttivo o accesso SQL effettuato.
Le istanze 3306 e 13306 non sono state connesse, modificate o riavviate.
Questo report certifica i candidati locali: non certifica che siano gi? su GitHub.

## Inventario esatto per l'approvazione

- [GITHUB_HANDOFF_FILES.json](GITHUB_HANDOFF_FILES.json): elenco completo di **86 file nuovi**, **49 file modificati**, **121 percorsi da mettere in stage** e **14 modifiche diagnostiche escluse**.
- [GITHUB_STAGING_PATHS.txt](GITHUB_STAGING_PATHS.txt): staging list esplicita, un percorso per riga, utilizzata dai comandi sotto.
- [VERSIONING_CANDIDATES.json](VERSIONING_CANDIDATES.json): inventario complessivo delle 135 modifiche/aggiunte, con `stage_in_handoff` esplicito.
- [manifest bootstrap](../../migrations/bootstrap/manifest.json): tutti i **124 artefatti indispensabili** presenti, versionabili e con SHA256 verificato. Nessun artefatto indispensabile mancante.

La revisione comprende backend e relativi layer, soluzione, CLI PlatformOperator,
progetti/test/otto lock file NuGet, frontend gi? tracciati e relativi lock/example,
SQL canonici MT1/3/4, sei candidate-r3, IAM-D, onboarding/governance,
DDL consolidato, schema-contract, manifest, validatori, tool, documentazione,
GitHub Actions, Dockerfile e configurazioni di esempio. Sono disponibili **48 SQL**
sotto migrations, incluse revisioni storiche R1/R2 non da applicare.

`.gitignore` e `.gitattributes` esistenti sono sufficienti: nessuna ulteriore
modifica in questo checkpoint. Rimangono le eccezioni SQL mirate e i contesti
LF/CRLF dei file storici con hash certificati. Le nuove liste/report ricadono
nelle regole LF di docs/development. I manifest storici non sono stati modificati.

## Sicurezza ed esclusioni

Scansionati tutti i **1719 file pubblici del pacchetto finale**, non soltanto quelli
nuovi. Nessuna chiave privata/certificato privato, datadir o dump dati selezionato;
nessuna corrispondenza con le credenziali private locali note. Le 82 segnalazioni
della scansione iniziale riguardano riferimenti a variabili, input/configurazione,
placeholder o credenziali esplicitamente sintetiche nei test; nessun segreto
operativo individuato. I documenti di consegna aggiunti sono stati controllati.
La scansione per pattern e valori noti non garantisce l'assenza di ogni possibile
segreto sconosciuto: resta richiesta la revisione del diff staged da parte del proprietario.

Esclusi dalle nuove aggiunte: private/, credenziali e configurazioni operative,
.env reali, chiavi private, certificati privati, dump/backup, datadir MySQL,
log e prove private, bin/obj/node_modules. Non vengono copiati nel pacchetto.
I 14 file TestResults modificati, elencati esattamente nel JSON, sono osservazioni
SQL/inventari route rigenerati: non necessari alla riproduzione, rimangono sul
worktree e non entrano nel commit proposto. Il pacchetto usa le loro versioni HEAD.

Sono inoltre gi? presenti in HEAD 97 log storici TestResults oggi coperti da ignore:
Git continua a tracciarli. Sono stati inclusi nella scansione, senza segreti
individuati; non sono nuove aggiunte n? dipendenze del bootstrap. Non ? stata
riscritta la storia e non ? stata eseguita una rimozione dall'indice. Eventuale
bonifica dei risultati storici va revisionata separatamente; ignore non rimuove
file gi? committati.

## Prova indipendente da soli candidati Git

Creato un nuovo snapshot privato con tutti i file gi? versionati e le nuove
aggiunte approvabili, senza copiare la working directory o file ignorati locali.
Le 14 modifiche diagnostiche escluse sono sostituite dai contenuti HEAD.
L'inventario privato di input registra SHA256 per ogni file; gli output build
sono successivi alla creazione e non costituiscono input. Git inizializzato solo
nel pacchetto per verificare attributi/ignore, senza stage o commit.

Risultati reali nel pacchetto:

| Controllo | Esito |
| --- | --- |
| Manifest bootstrap | 124/124 hash corretti, nessun indispensabile ignorato |
| Checkout Git con core.autocrlf=true | 124/124 SHA256 preservati dopo clean/smudge, senza stage |
| Dipendenze ProjectReference | 0 mancanti; tutti gli 8 lock NuGet disponibili |
| Restore intera soluzione | `--locked-mode --ignore-failed-sources`, exit 0, cache locale disponibile |
| Build intera soluzione | exit 0, 0 errori, 18 warning (inclusi advisory/nullable) |
| Test unitari senza SQL | 69 Passed |
| Verificatori Python offline | 11/11 Passed |
| git diff --check | exit 0; avvisi di normalizzazione EOL su sorgenti non soggetti a hash |
| Indice repository principale | vuoto, invariato |

La prova non dipende da sorgenti privati, vecchi dump o configurazioni applicative
locali: questi non sono input. Python/.NET/MySQL e pacchetti NuGet/npm rimangono
prerequisiti esterni da installare/scaricare su un PC nuovo. La cache NuGet ?
una dipendenza di distribuzione esterna, non un artefatto sorgente mancante.
Frontend e lock sono inclusi; non ? stata eseguita una nuova build Expo/mobile.
Non sono stati rilanciati bootstrap SQL n? integrazione per rispettare il divieto
di toccare i database. La precedente certificazione MT-6 **966/966** e la prova
bootstrap su istanza sacrificabile restano documentate nei report MT-6: non sono
presentate come nuove esecuzioni di questo checkpoint.

Evidenze locali escluse da Git: `migrations/mt4/private/github-handoff-20261022/`
(snapshot, manifest input, restore/build/unit log, scansione e risultato audit).
Non sono richieste dal clone o dal setup.

## Audit NuGet: UNKNOWN

Nuovo tentativo online: exit 1, impossibile accedere a
`https://api.nuget.org/v3/index.json`, socket non consentito dalle autorizzazioni
locali. Non ? stato ottenuto un elenco advisory valido. **UNKNOWN**, non zero
vulnerabilit?; nessun pacchetto ? stato aggiornato senza verifica.

Da rieseguire con rete abilitata, conservando output e controllando entrambi gli exit code:

```powershell
$auditPath = Join-Path $env:TEMP 'ElectronicRegister-nuget-audit.json'
dotnet list Backend/ElectronicRegisterAPI/ElectronicRegisterAPI.slnx package --vulnerable --include-transitive --format json > $auditPath
if ($LASTEXITCODE -ne 0) { throw 'NuGet advisory audit unavailable' }
python tools/check-dependency-audit.py $auditPath
if ($LASTEXITCODE -ne 0) { throw 'Advisory audit not cleared' }
```

Conservare tale output fuori Git; la release operativa resta soggetta a questo
controllo e alle condizioni riportate nel report MT-6C.

## Comandi Git da eseguire solo dopo approvazione

Controllare prima che branch/HEAD e lista siano quelli revisionati; eventuali
ulteriori modifiche richiedono nuova revisione. I comandi seguenti non sono stati eseguiti.

```powershell
if ((git branch --show-current) -ne 'refactor-database-entities') { throw 'Unexpected branch' }
if ((git rev-parse HEAD) -ne '7f0fed3897c225af5fdf3861974930a41122582f') { throw 'HEAD changed: review required' }
if (git diff --cached --name-only) { throw 'Index already contains changes: review first' }
python tools/verify-project-artifacts.py
if ($LASTEXITCODE -ne 0) { throw 'Artifact verification failed' }
git add --pathspec-from-file=docs/development/GITHUB_STAGING_PATHS.txt
if ($LASTEXITCODE -ne 0) { throw 'Stage failed: inspect index before continuing' }
git diff --cached --check
if ($LASTEXITCODE -ne 0) { throw 'Staged diff check failed' }
git diff --cached --stat
git diff --cached
# Dopo la revisione completa del diff e l'approvazione del proprietario:
git commit -m "Complete MT-5B through MT-6 and portable database bootstrap"
if ($LASTEXITCODE -ne 0) { throw 'Commit failed' }
git push origin refactor-database-entities
if ($LASTEXITCODE -ne 0) { throw 'Push failed' }
```

Verificare l'identit? del remote origin prima del push senza divulgare eventuali
credenziali nell'URL. Il comando di stage comprende solo i 121 percorsi espliciti,
non tutti i file del worktree. I 14 risultati diagnostici rimarranno modificati.
Dopo il push verificare su un nuovo clone che il commit e tutti i 124 artefatti
siano presenti: questo non ? attestabile prima del push autorizzato.

## Configurare il database su un computer nuovo

Seguire [LOCAL_DATABASE_SETUP.md](LOCAL_DATABASE_SETUP.md). Installare Windows,
PowerShell, Python 3.10+, SDK .NET 10 compatibile con global.json e binari MAMP
MySQL 5.7.24. Porta 13306 libera e datadir nuovo, nessun database da preservare.

```powershell
git clone --branch refactor-database-entities <URL_DEL_REPOSITORY> ElectronicRegister
Set-Location ElectronicRegister
python tools/verify-project-artifacts.py
dotnet restore Backend/ElectronicRegisterAPI/ElectronicRegisterAPI.slnx --locked-mode
dotnet build Backend/ElectronicRegisterAPI/ElectronicRegisterAPI.slnx --no-restore
$password = Read-Host 'Nuova password privata del root di test isolato' -AsSecureString
./tools/setup-local-test-db.ps1 -MySqlBin C:\MAMP\bin\mysql\bin -CreateInstance -ConfirmIsolatedTest -Password $password
./tools/test-local.ps1 -MySqlBin C:\MAMP\bin\mysql\bin -Password $password
```

Non eseguire CreateInstance sul computer attuale: la 13306 esiste gi?.
Non importare datadir o dump, non replayare migrazioni sopra il consolidato,
non usare 3306. Il bootstrap preserva SQL_MODE/ACTION_ORDER e verifica schema,
trigger, FK e validator. Autorit? PlatformOperator, notifiche e segreti operativi
richiedono configurazione esplicita esterna; rollout chiusi per default.
MySQL 5.7 ? legacy: nessuna compatibilit? con un motore pi? recente ? attestata.

**GITHUB HANDOFF READY**
