# Identità di piattaforma e ciclo di vita degli account - report

Esecuzione autonoma del 13 novembre 2026 sul branch `refactor-database-entities`, a partire da `3553d78`.
Target: **MySQL 8.4.13** (D1). Design: [PLATFORM_IDENTITY_AND_LIFECYCLE.md](../architecture/PLATFORM_IDENTITY_AND_LIFECYCLE.md).
Decisioni prese in autonomia e scostamenti dal prompt: [PLATFORM_IDENTITY_DECISIONS.md](PLATFORM_IDENTITY_DECISIONS.md).
Contratto API: [PLATFORM_IDENTITY_ACCOUNTS.md](../api/PLATFORM_IDENTITY_ACCOUNTS.md). Pubblicato su `origin/refactor-database-entities` (vedi sotto).

## Commit

| Fase | Commit | Contenuto |
|---|---|---|
| 1 | `b143f00` | Documento di design e registro delle decisioni |
| 2 | `94119ac` | Successore `er-platform-identity-1`: migrazione, validazione, contratto, runner, scaffold, deroga D-B nei test |
| 3 | `d57e5f1` | Domain e Application (con gli adattamenti di Infrastructure/API necessari, A20) |
| 4 | `a718f4f` | Job di ciclo di vita, riattivazioni D-C, readiness, import E4, documentazione |
| 5 | questo commit | Report e risultati della verifica |

## Risultato consegnato

- **Schema** (additivo, successore di er-quality-2-mysql84): colonne CF protette e stato `Active/Closed` su `user`,
  ledger append-only `identity_lifecycle_event`, trigger che rendono `user.email` immutabile e vietano il `DELETE` di
  `user` (G0), con la deroga legata al container di test attestato (D-B). `contract.json` di er-quality-2 invariato;
  i file sono nella closure 8.4. Inventario: 71→72 tabelle, 180→186 trigger, 200→201 FK, 303→308 indici.
- **Indirizzi**: dominio unico, `nome.cognome[N]` deterministico, allocazione sotto il mutex email globale con lettura
  in blocco delle riserve e ritentativi su 1062; un indirizzo emesso non torna mai libero.
- **Codice fiscale**: obbligatorio, validato (omocodia, carattere di controllo), HMAC-SHA256 per unicità e ricerca,
  AES-256-GCM per il valore; chiavi da configurazione senza default, avvio rifiutato se mancano o sono deboli.
- **Deduplica**: stessa risposta per persona nuova, già attiva (password `null`, D-A) o chiusa (riattivazione);
  `409` neutro se la persona è già nell'istituto (E1); `422` se i nomi non coincidono con la scheda (E2).
- **Ciclo di vita**: job giornaliero configurabile; chiusura immediata per docenti/staff/admin, dopo la durata per gli
  studenti; login dei chiusi indistinguibile da un indirizzo inesistente; riattivazione per ricreazione, per membership
  o istituto riattivati (D-C, stessa transazione) e rete di sicurezza del job. `IMailboxProvisioner` no-op.
- **Import E4**: modalità `CreatePlatformAccounts` con CF cifrato nello staging, report completo delle righe errate,
  una sola transazione per file.
- **Policy email per istituto**: scritture non operative con il flag attivo, dati e indirizzi conservati.

## Verifica

Tutti i test di integrazione girano su container `mysql:8.4.13` creati da Testcontainers (`ER_TEST_MYSQL84=1`),
come in CI. Nessuna istanza 3306, 3307 o 13306 è stata usata; il container `mysql84-local` non è stato toccato.
Le prove manuali della Fase 2 hanno usato container usa-e-getta propri (porte 13399, 13411–13415), poi rimossi.

| Controllo | Esito |
|---|---|
| `python tools/verify-project-artifacts.py` | verde |
| `node tools/mysql84-release.cjs` / `test-mysql84-release.cjs` / `test-release-manifest.cjs` / `test-mysql84.cjs` | verdi (contract.json invariato; closure rigenerata con `--write` a ogni fase, solo file rivisti) |
| `python -m unittest discover -s tools -p 'test_*.py'` | 150 test, OK (8 saltati) |
| `dotnet restore … --locked-mode` | OK, nessun pacchetto aggiunto |
| `dotnet run --project tools/CodeOrganization -- check .` | 0 violazioni (711 file) |
| `dotnet build` (Release) | 0 errori |
| Unit | 657/657 |
| Integrazione completa | vedi sotto |
| `git diff --check` | pulito |
| `git check-ignore -v migrations/bootstrap/001_schema.sql` | nessuna corrispondenza (exit 1, atteso) |
| Runner `Invoke-PlatformIdentityMigration.ps1` su container predecessore | verificato end-to-end (19,6 s), rifiuta il replay |
| `002_validate.sql` | 13/13 a zero |

### Import da 600 righe

File con 300 coppie di omonimi, due persone già attive in un altro istituto e una chiusa. Misure della chiusura delle
verifiche (16 core; processo di test e API limitati a 2 core con affinità di CPU, MySQL non limitato; A37):

| Misura | Prima (`c5e56fa`) | Dopo (budget di hashing) |
|---|---:|---:|
| Conferma, 16 core | 7,1 s | 7,8 s |
| Conferma, 2 core | 32,6 s | 33,2 s |
| Anteprima | 0,8-1,1 s | 0,8 s |

Il parallelismo è ora limitato (`ImportHashParallelism`, tetto ai core, budget condiviso dal processo) ma **il tempo di
una singola conferma non migliora**: BCrypt (costo 11, invariato) è CPU-bound, circa 0,055 s per riga su 2 core. Su 2
core il client di test con timeout di 15 s veniva interrotto (verificato): il test usa ora 3 minuti (A39). Timeout
consigliato (≥120 s) e 600 righe per file in `docs/api/PLATFORM_IDENTITY_ACCOUNTS.md`.

### Suite di integrazione completa (a gruppi, worktree pulito a `a1a191a`)

Gruppi sequenziali di classi, container ripuliti tra un gruppo e l'altro, circa 27 minuti complessivi, mai in un solo
processo. Picco di memoria (container MySQL + testhost) misurato ogni 5 s: 1,3-3,6 GiB; Docker dispone di 16 GiB.

| Gruppo | Casi | Superati | Falliti | Non eseguiti | Durata | Picco |
|---|---:|---:|---:|---:|---:|---:|
| 0 (InstitutionClassCrudTests) | 497 | 497 | 0 | 0 | 4 m 5 s | 3,6 GiB |
| 1 | 177 | 177 | 0 | 0 | 2 m 33 s | 2,2 GiB |
| 2 | 176 | 171 | 5 | 0 | 3 m 23 s | 1,8 GiB |
| 3 | 176 | 173 | 3 | 0 | 2 m 37 s | 1,3 GiB |
| 4 | 177 | 177 | 0 | 0 | 3 m 15 s | 1,6 GiB |
| 5 | 178 | 174 | 4 | 0 | 4 m 10 s | 1,6 GiB |
| 6 | 176 | 173 | 3 | 0 | 2 m 53 s | 1,5 GiB |
| 7 | 176 | 176 | 0 | 0 | 3 m 21 s | 1,5 GiB |
| **Totale** | **1733** | **1718** | **15** | **0** | | |

Falliti: 0 nuovi. I 15 sono i 13 già noti (INC-003/INC-012 e D4: `Mt1Integrity.CanonicalExpand…`,
`Mt4aCourseStructure.InformationSchema…`, `Mt3dHttp.Release_WorkerDispatch…` (2), `HistoricalExpiryBoundary…`,
`Mt5bSecurityState.Sql_FixtureGuard…`, `Mt5dMembershipMutation.OfflineGraph…`, `Phase4_ExamLifecycle.Audit_IdentityProfile…`
(2), `Mysql84Release…`, 3 test CLI di `Mt5cOnboarding`) più `Mt4aCourseStructure.ExistingBackfill…` e `LegacyEfWriters…`,
che falliscono **identici** su `3553d78` (manca l'holding legacy, INC-012): il report precedente le indicava per errore come
regressioni (A41). Di questi 15 ho confrontato con la baseline solo i due ultimi; per gli altri 13 ho verificato il
nome del test, non il testo dell'errore. Baseline 8.4: 16 falliti e 94 non eseguiti (in CI gli INC sono esclusi).

Una prima esecuzione a gruppi era invalida e l'ho scartata: la fixture verifica la closure di rilascio, che divergeva
mentre modificavo i sorgenti nello stesso albero. La esecuzione valida è stata fatta da un worktree pulito.

## Correzioni della chiusura delle verifiche

- **Compito 1**: `identity_lifecycle_event` non era classificata da `Mt3aOwnershipMigration` ("Unclassified table"): il
  backfill falliva anche dopo la correzione `*_key_version` (A40). `OwnershipBackfill`, `Mysql84GuidMetadata`,
  `LegacyBackfill`, `BulkImport_StaffRevocation…`, `TechnicalProvisioning…`, `AmbiguousLegacyAudit…` e
  `StagingCalendarFeatureTests` passano singolarmente e con la propria classe.
- **Compito 3** (revisione del diff): G0/D-B verificati (la deroga si lega solo con `ownedFixture`, il runner di
  produzione la rifiuta, test del rifiuto anche per il principal API); nessun log o errore con il CF; `ToString` dei record con
  segreti ora redatto (A38); login `Closed` identico a un indirizzo ignoto per password (test sui tempi) e per Microsoft
  (nuovo scenario `closed-account`); flag `false` di default; nessun percorso rilascia un indirizzo (trigger e riserve permanenti).
  **Scostamento**: con il flag spento `POST /api/institution/accounts` risponde `404`, non "comportamento invariato" (A32).
- **Compito 5** (A21): creazione singola e import ritentano fino a 4 volte su deadlock/lock wait; esauriti i tentativi
  `503` `PlatformBusy` con `Retry-After`, distinto dal `409` di E1. Test con deadlock reale del server: il ritentativo
  riesce, a tentativi esauriti la risposta è distinta e non restano account né riserve (A33-A35).

## Cosa non è stato verificato

- Nessuna esecuzione su MySQL reale di staging o produzione, né sull'istanza 13306 storica (vietata).
- Il successore è applicato automaticamente solo al database NUOVO del rilascio EC2/staging con `--enable-platform-identity` (A44); per gli ambienti esistenti va eseguito in modo rivisto con
  l'identità di migrazione del deployment. La certificazione runtime di er-quality-2 (`mysql84_release.py`) non è
  stata rieseguita, perché non cambia.
- Comportamento sotto carico reale e con più repliche dell'API (il mutex globale serializza le creazioni); i tempi su 2 core sono
  una simulazione con affinità di CPU, non un container.
- Integrazione futura con Microsoft 365 (non implementata), invio di credenziali (non esiste).
- Frontend: non usa il nuovo contratto.
- I test CLI di `Mt5cOnboardingTests` che richiedono container `er84-dotnet-*` (caso D4 della baseline) e gli altri 12 noti (INC).

## Limiti noti e lavoro rimasto

- Primo admin senza CF fuori dalla deduplica (D-D); scadenza studente dal `joined_at` della membership (E3).
- Nomi senza lettere latine non rappresentabili; nessuna traslitterazione.
- Rotazione delle chiavi documentata, non implementata; colonne CF immutabili.
- Un account chiuso che riceve un invito non può accettarlo finché non viene riattivato.
- Deadlock (A21): ritentati automaticamente 4 volte; se persistono, `503 PlatformBusy` (riprovare con lo stesso `eventId`).
- Import grandi: ~0,055 s per riga su 2 core (CPU-bound); proxy con timeout ≥120 s e file da 600 righe.
- Applicazione del successore agli ambienti esistenti e procedura di rilascio dedicata.
