# Night run 2026-11-06: staging sul commit corrente, flusso di produzione con TLS, copertura completa dei test

**Consegnabile domani: con limiti.** Le tre lacune di verifica sono chiuse in CI e tutti i workflow sono verdi. I limiti sono nel §6: certificati, DNS e proxy reali su EC2, INC-008 e INC-010 (`ExpectedVersion`), e l'impostazione della branch protection, che spetta all'utente.

## 1. SHA ed esiti delle run

Branch `refactor-database-entities`, partenza da 7e31414, ultimo SHA di codice b3a9030. Il commit di questo report aggiunge solo documentazione, l'esempio di configurazione e il manifest rigenerato; l'esito della sua CI è riportato nel messaggio finale della sessione.

**Esiti su b3a9030: tutti verdi.**

| Workflow | Esito | Run |
|---|---|---|
| MySQL 8.4 corrective release and upgrade (CI principale) | success | [42135535707](https://github.com/xxdema10xx/ElectronicRegister/actions/runs/42135535707) |
| Integration full (14 shard + gate) | success | [42135535461](https://github.com/xxdema10xx/ElectronicRegister/actions/runs/42135535461) |
| Staging package on current code, incluso il flusso di produzione | success (`runner` e `v5.6.1`) | [42135535406](https://github.com/xxdema10xx/ElectronicRegister/actions/runs/42135535406) |
| Historical er-quality-1 package and real SQL TLS | success | [42135535743](https://github.com/xxdema10xx/ElectronicRegister/actions/runs/42135535743) |
| Verify portable backend | success | [42135535579](https://github.com/xxdema10xx/ElectronicRegister/actions/runs/42135535579) |

**Commit per fase**

| Fase | Commit | Prima run verde della fase |
|---|---|---|
| 1, staging sul commit corrente | 1ab1196, 32ab590 (`fetch-depth: 0`) | [42129475342](https://github.com/xxdema10xx/ElectronicRegister/actions/runs/42129475342) |
| 2, flusso di produzione con TLS | 033f82e | [42130770153](https://github.com/xxdema10xx/ElectronicRegister/actions/runs/42130770153) |
| 3, copertura completa | cbac374, 464635f, b3a9030 | [42131545908](https://github.com/xxdema10xx/ElectronicRegister/actions/runs/42131545908): 12 shard su 14 (vedi §3.3); poi [42135535461](https://github.com/xxdema10xx/ElectronicRegister/actions/runs/42135535461): 14 su 14 |

## 2. Inventario dei test di integrazione

Calcolato con `dotnet test --list-tests`: 1702 casi, 819 metodi.

| Voce | Casi |
|---|---|
| CI principale, eseguiti | 932 |
| CI principale, job `windows-cli` | 3 |
| `integration-full`, eseguiti | 752 |
| Esclusi con ID (2 CI principale + 11 metodi full + 2 casi full) | 15 |
| Falliti nell'ultima run | 0 |
| **Totale** | **932 + 3 + 752 + 15 + 0 = 1702** |

Il dettaglio delle esclusioni è in [BASELINE_MYSQL84.md §9](BASELINE_MYSQL84.md). La lista è bloccata da un test: aggiungere o togliere una voce fa fallire `tools/test_integration_partition.py`.

I test unit (598) sono fuori da questo conteggio e girano nella CI principale.

## 3. Fasi

### 3.1 Fase 1: staging sul commit corrente (completata)
- Il nuovo `.github/workflows/staging-current.yml` costruisce `infra/Dockerfile` su `github.sha` e lancia il controllo di `infra/staging/host.py`: package, avvio, audit HTTP e smoke.
- Che venga testato proprio `github.sha` è verificato in quattro punti; il job fallisce se uno non coincide:
  - `git rev-parse HEAD`;
  - lo SHA è diverso da quello storico;
  - la `revision` in `state.json`;
  - la label `org.opencontainers.image.revision` dell'immagine API.
- `ops.py` verifica la closure 8.4 e applica il bundle della release correttiva.
- La nuova fase `release-preflight` di `host.py` esegue l'audit completo dello schema come root, via socket, solo sul database usa e getta. Il migratore non riceve grant aggiuntivi.
- `tools/release-preflight.cjs` ha un ramo per il contratto 8.4. Il ramo storico è invariato.
- Il workflow storico e il suo SHA sono intatti e verdi.

### 3.2 Fase 2: flusso di produzione con TLS (completata, 23 passi su 23)

`infra/staging/production_flow.py` gira nella stessa job, sull'immagine appena costruita, con:
- `ASPNETCORE_ENVIRONMENT=Production`, `Deployment:Mode=Production`, `RolloutApproved=true`;
- MySQL 8.4.13 con TLS ≥ 1.2 e `SslMode=VerifyFull`, CA e password generate nella run, utenti non root;
- HTTPS tramite `infra/nginx.release.conf`, con immagine nginx fissata per digest.

Casi negativi superati:
- avvio rifiutato senza `RolloutApproved`;
- connessione al DB rifiutata con CA sbagliata e con TLS disattivato;
- l'API non pubblica alcuna porta HTTP;
- HTTP sul proxy risponde solo con un redirect 308;
- nessuna password nei log dell'API e del proxy.

Lo script `OwnerProvisioning` funziona con `VerifyFull` senza modifiche.

`smoke.py` non è stato modificato: non conteneva la registrazione pubblica né altri flussi rimossi. L'audit HTTP passa 54 su 54, più 8 su 8.

**Non dimostrabile in CI:**
- certificati pubblici reali e catena della CA pubblica;
- DNS reale;
- nginx sull'host EC2 e firewall o security group;
- `KnownProxies` reali;
- MySQL gestito di produzione con la sua CA;
- lo smoke HTTPS pubblico, che resta un gate separato su EC2.

### 3.3 Fase 3: copertura completa (completata)

La CI principale non copriva 51 classi, cioè 386 metodi e 765 casi. Ora le esegue il workflow separato `integration-full.yml`:
- 14 shard, calcolati sul caso peggiore (≈ 3465 s su 3600);
- un gate che controlla presenza degli shard, ripartizione e conteggio.

Prima run completa (464635f): 12 shard su 14. Il log riportava due fallimenti, classificati uno per uno:
- `PrivateTestFilesTests` ×2: categoria a) ambiente. Su Linux lanciano `PlatformNotSupportedException` perché le ACL Windows non esistono. Esclusi con INC-013. Non riguardano autorizzazione o tenant del prodotto.

Nessun fallimento è stato classificato come c), cioè possibile bug reale.

**Test instabile** `Mt5aMembershipAdminTests.InvalidFiltersReturn400("status=")`:
- non escluso e mai rieseguito per farlo passare;
- passato in entrambe le run complete, 12 casi su 12;
- causa tracciata: timeout di 15 s di `HttpClient` sotto il carico della suite in un solo processo, eliminato dall'isolamento per metodo della CI;
- correzione proposta, se dovesse ripresentarsi: timeout configurabile nel test (BASELINE §9).

## 4. Bug reali trovati

| ID | Severità | Descrizione | Stato |
|---|---|---|---|
| INC-019 | Bassa | Un avvio in Production rifiutato dalla `RuntimePolicy` termina con exit code 139 (SIGSEGV) invece di un errore pulito. Il rifiuto funziona, ma sembra un crash | Aperto. Fix proposta in `Program.cs` (`Environment.Exit(2)`), non applicata: codice di produzione |

Nessun bug Critico, Alto o Medio. Dettagli in [BACKEND_REVIEW.md](BACKEND_REVIEW.md).

## 5. Scelte prese in autonomia
- **Esclusione per caso.** Il filtro usa un token `DisplayName!~holding-active`, validato perché escluda solo i 2 casi previsti. MSBuild rifiuta virgole, virgolette e parentesi in `--filter`, anche con escape.
- **`Mysql84ReleaseTests`** è escluso da `integration-full` (INC-017) perché richiede `ER_MYSQL84_RELEASE_CONFIG`. La sua verifica è coperta dal job di release della CI principale.
- **`PrivateTestFilesTests`** ×2 è escluso con INC-013: richiede Windows.
- **Annotazioni.** Ho aggiunto uno step che riassume i fallimenti come annotazioni (`tools/ci_annotations.py`), con password, token e percorsi oscurati. Log e artifact non sono leggibili senza autenticazione.
- **`RolloutApproved: true`** in `infra/release.runtime.example.json`, documentato come scelta esplicita in `INSTITUTION_ACCOUNTS.md`: con `false` la release non parte. `ExpectedVersion` resta `5.7.24` perché `tools/test_staging.py` lo fissa (Lotto 3).
- **nginx** fissato per digest al momento della run. **Lo script del proprietario** gira con `er_migrator`, non con root.
- **INC-019** aperto invece di toccare il codice di produzione.
- **Push.** Ho fatto un push dopo ogni fase. Il push rifiutato da GitHub per un errore transitorio è andato a buon fine al terzo tentativo, senza force push.

## 6. Cosa deve fare l'utente

**1. Branch protection su `main`.** Check richiesti:
- `final-gate`, dalla CI principale;
- `gate`, da `integration-full`;
- `current (runner)` e `current (v5.6.1)`, da `staging-current`;
- `source (ubuntu-latest)` e `source (windows-latest)`, da Verify.

**Attenzione:** la CI principale (`mysql84-remediation-candidate.yml`) e il workflow storico (`staging-package.yml`) oggi **non partono sulle PR verso `main`**: si attivano solo per push e PR verso `refactor-database-entities`. Per richiedere `final-gate` su una PR a `main` bisogna aggiungere `main` ai loro trigger. Non l'ho fatto perché la decisione era di lasciare la CI principale com'è.

**2. Configurazione di produzione reale** (non nel repo):
- `Deployment:RolloutApproved=true` come scelta consapevole;
- `Readiness:ExpectedVersion=8.4.13`;
- connection string con `SslMode=VerifyFull`, `SslCa` del server, utente non root;
- `KnownProxies` con l'IP reale del proxy;
- `AllowedHosts` con il dominio reale;
- i flag `Multitenancy` (`SchoolApi`, `MembershipWriteApi`) come nell'esempio;
- segreti JWT ed email solo nell'ambiente.

**3. Su EC2:** certificati pubblici, DNS, nginx e lo smoke HTTPS pubblico.

**4. In locale:** i test unit e l'avvio delle DLL nuove restano bloccati da Smart App Control. La verifica locale si appoggia ai container.

## 7. Non verificato
- I punti elencati in §3.2 sotto "Non dimostrabile in CI".
- `PrivateTestFilesTests` e i test della CLI Windows: girano solo su un host Windows amministrativo.
- I test unit in locale (in CI sono verdi).
- I 15 casi esclusi, per i motivi registrati con il loro ID.
- La causa esatta del 139 (INC-019).
