# Identità di piattaforma e ciclo di vita degli account - report

Esecuzione autonoma del 13 novembre 2026 sul branch `refactor-database-entities`, a partire da `3553d78`.
Target: **MySQL 8.4.13** (D1). Design: [PLATFORM_IDENTITY_AND_LIFECYCLE.md](../architecture/PLATFORM_IDENTITY_AND_LIFECYCLE.md).
Decisioni prese in autonomia e scostamenti dal prompt: [PLATFORM_IDENTITY_DECISIONS.md](PLATFORM_IDENTITY_DECISIONS.md).
Contratto API: [PLATFORM_IDENTITY_ACCOUNTS.md](../api/PLATFORM_IDENTITY_ACCOUNTS.md). Nessun push.

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

File con 300 coppie di omonimi, due persone già attive in un altro istituto e una chiusa (Docker Desktop, 16 core):

| Misura | Prima | Dopo l'ottimizzazione (A27) |
|---|---:|---:|
| Preview | 0,8 s | 0,8 s |
| Conferma | 11,3 s | 7,8 s |
| di cui BCrypt (fuori transazione) | — | circa 4,4 s |

600 password BCrypt (cost 11) su 2 core richiedono circa 26 s: in produzione su istanze piccole la conferma di un
file grande può superare i timeout di un proxy configurato sotto il minuto.

### Suite di integrazione completa

Esecuzione unica, un solo processo, 16 min 45 s: **1730 casi, 1709 superati, 21 falliti, 0 non eseguiti**
(baseline 8.4: 1676 casi, 16 falliti e 94 non eseguiti).

- 13 fallimenti già noti nella baseline (INC-003/INC-012, ambiente): `Mt1Integrity.CanonicalExpandIsRerunnable…`,
  `Mt4aCourseStructure.InformationSchemaHasCompositeRestrictConstraints`, `Mt3dHttp.Release_WorkerDispatch…` (2),
  `HistoricalExpiryBoundary…`, `Mt5bSecurityState.Sql_FixtureGuardExcludesRealLegacyHolding`,
  `Mt5dMembershipMutation.OfflineGraph…`, `Phase4_ExamLifecycle.Audit_IdentityProfileFault…` (2), `Mysql84Release…`,
  i 3 test CLI di `Mt5cOnboarding` (D4).
- **Regressioni introdotte da questo lavoro e trovate dalla suite completa:**
  - `Mt3aOwnership.OwnershipBackfill…`, `Mt4aCourseStructure.ExistingBackfill…`, `Mysql84GuidMetadata…`,
    `InstitutionFormativePlan.LegacyBackfill…`: il controllo GUID di `Mt3aOwnershipMigration` rifiuta colonne `*_id`
    non GUID. **Corretto dopo la suite** rinominando `fiscal_code_*_key_id` in `fiscal_code_*_key_version`
    (contratto rigenerato su 8.4.13, `002_validate.sql` a zero); la suite completa **non è stata rieseguita**.
  - `InstitutionClassCrud.BulkImport_StaffRevocationWaitsForCommit…`: la lettura preliminare dell'import apriva una
    seconda transazione. **Corretto dopo la suite** (lettura solo con il flag attivo); non rieseguito.
  - **Ancora aperti:** `Mt5bSecurityState.TechnicalProvisioning…` (teardown che cancella un utente senza la deroga
    D-B), `Mt3aOwnership.AmbiguousLegacyAudit…` (`IdentityAccountIntegrity` rifiuta le azioni
    `identity_account_event` scritte dalla creazione di account nello stesso database condiviso; da verificare se
    preesistente), `StagingCalendarFeatureTests.EverySmokeCase…` (errore di inferenza del body sulle rotte, da
    analizzare: in baseline falliva prima per Python assente).

## Cosa non è stato verificato

- Nessuna esecuzione su MySQL reale di staging o produzione, né sull'istanza 13306 storica (vietata).
- Nessun percorso di rilascio automatico (EC2/staging) applica il successore: va eseguito in modo rivisto con
  l'identità di migrazione del deployment. La certificazione runtime di er-quality-2 (`mysql84_release.py`) non è
  stata rieseguita, perché non cambia.
- Comportamento sotto carico reale e con più repliche dell'API (il mutex globale serializza le creazioni).
- Integrazione futura con Microsoft 365 (non implementata), invio di credenziali (non esiste).
- Frontend: non usa il nuovo contratto.
- I test CLI di `Mt5cOnboardingTests` che richiedono container `er84-dotnet-*` (caso D4 della baseline).

## Limiti noti e lavoro rimasto

- Primo admin senza CF fuori dalla deduplica (D-D); scadenza studente dal `joined_at` della membership (E3).
- Nomi senza lettere latine non rappresentabili; nessuna traslitterazione.
- Rotazione delle chiavi documentata, non implementata; colonne CF immutabili.
- Un account chiuso che riceve un invito non può accettarlo finché non viene riattivato.
- Rischio residuo di deadlock (A21) tra la creazione di account e flussi `REPEATABLE READ` che prendono il mutex
  email dai trigger dei profili: InnoDB annulla una transazione e il client riceve 409.
- Applicazione del successore agli ambienti esistenti e procedura di rilascio dedicata.
