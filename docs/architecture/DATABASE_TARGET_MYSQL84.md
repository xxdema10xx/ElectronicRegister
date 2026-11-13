# Decisione D1: MySQL 8.4 LTS come unico target di produzione

| Voce | Valore |
|---|---|
| Stato | **DECISA** |
| Data | 2026-11-05 |
| Decisore | Proprietario del progetto |
| Branch / commit di riferimento | `refactor-database-entities` @ `b53b10b` (codice identico a `03bb1be`) |
| Origine | `docs/quality/BACKEND_REVIEW.md`, INC-002 e §9 D1 |

## Decisione

MySQL **8.4 LTS** è l'**unico** motore di database supportato in produzione. La versione certificata oggi è **8.4.13**, l'immagine `mysql:8.4.13@sha256:5cf1e1293ea42e077e6a22114e6773b9aae3bb8f9933a2acd5a9721518ff8f5b` già usata in CI e in staging. Gli aggiornamenti di patch all'interno della serie 8.4 sono ammessi solo dopo la correzione di INC-003 (versioni esatte hard-coded).

MySQL 5.7.24 non è più un target di produzione. **In questa fase non viene rimosso nulla di 5.7**: la rimozione avviene nel Lotto 3 del piano di risoluzione.

Il **provider EF Core** non fa parte di questa decisione. Resta aperto: vedi INC-001 e la proposta di accettazione temporanea del rischio in `BACKEND_REVIEW.md` §9, che non è ancora approvata.

## Motivazione

- La serie MySQL 5.7 è fuori supporto dal fornitore da ottobre 2023, e la build 5.7.24 è molto indietro anche rispetto all'ultima patch della serie.
- Il binario 5.7.24 MAMP usato come riferimento espone TLS 1.0/1.1 e non supera il gate TLS 1.2 con `VerifyFull` (`docs/quality/RELEASE_READINESS.md:27`).
- CI, staging e la release correttiva `er-quality-2-mysql84` sono già costruiti su 8.4.13: `.github/workflows/mysql84-remediation-candidate.yml`, `infra/staging/mysql-image.json`, `migrations/mysql84-remediation/`. `node tools/mysql84-release.cjs` passa su `b53b10b`.
- Un solo motore riduce le divergenze di comportamento già documentate: parole riservate (`row_number`), sql_mode (`NO_AUTO_CREATE_USER`), regole FK di default (`NO ACTION`/`RESTRICT`), plugin di autenticazione, DEFINER.
- 8.4 è una release LTS, quindi la stabilità è prevedibile per il ciclo di vita dell'applicazione.

## Conseguenze

- Le prove di regressione valide per la produzione sono quelle su 8.4. Le evidenze storiche su 5.7.24 (FINAL_RELEASE_ASSESSMENT, MULTITENANCY_RELEASE_GATE e altre) restano documentazione storica e non certificano la produzione.
- La baseline di riferimento per i lotti successivi va eseguita su un'istanza usa e getta 8.4.13 (`docs/quality/BASELINE_MYSQL84.md`).
- I dati eventualmente presenti su 5.7 vanno portati a 8.4 con il percorso `migrations/mysql84-remediation` (predecessore immutabile più correzioni). Non si fa replay del bootstrap.
- Il Lotto 3 diventa il punto in cui il ramo 5.7 viene rimosso dal codice e dalla documentazione (INC-002, INC-003, INC-004, INC-005, INC-010).
- Ogni modifica a file della closure di rilascio 8.4 segue la regola di processo in `BACKEND_REVIEW.md` §8 (`node tools/mysql84-release.cjs --write` deliberato).

## Cosa resta su 5.7 fino al Lotto 3 (inventario, nessuna modifica in questa fase)

| Elemento | Riferimento | Comportamento attuale |
|---|---|---|
| README | `README.md:3` | Dichiara "MySQL 5.7.24" |
| Readiness API | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Operations/OperationalPipeline.cs:151,155` | Accetta la variante 5.7 (gate `er-quality-1-closure-1`); in LocalTest il default è `5.7.24` |
| CLI PlatformAdmin | `tools/PlatformAdmin/Program.cs:85` | Richiede `VERSION() = 5.7.24` e la datadir MAMP (vedi anche INC-008) |
| Fixture di integrazione con connessione fornita | `tests/Integration/IntegrationFixture.cs:161,189` | `TEST_MYSQL_CONNECTION` è ammesso solo su `127.0.0.1:13306` (MAMP 5.7) o su un'istanza isolata con `VERSION() = 5.7.24`. Il percorso 8.4 locale è solo il container Testcontainers |
| Fixture Windows CLI | `tools/windows-cli-mysql.json:2`, `.github/workflows/mysql84-remediation-candidate.yml:271` | Scarica `mysql-5.7.24-winx64.zip` per la certificazione Windows della CLI |
| Bundle SQL di rilascio | `tools/prepare-release-sql.cjs:12` | La serie di default è 5.7; 8.4 solo con `mysqlSeries: '8.4'` |
| Esempio runtime di rilascio | `infra/release.runtime.example.json:6` | `"ExpectedVersion": "5.7.24"` (INC-010) |
| Validatori di integrità | `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence/Migrations/InstitutionClassIntegrity.cs:16` e altri cinque | Distinguono 8.4.13 esatto da "tutto il resto = 5.7" (INC-003) |
| Script di setup locale e migrazione | `tools/local_test_db.py`, `tools/formative_test_db.py`, `tools/New-InstitutionStaffTestInstance.ps1`, `tools/Invoke-*Migration.ps1`, `docs/development/LOCAL_DATABASE_SETUP.md` | Istanze MAMP 5.7.24 su 13306 o porte isolate |
| Documenti di rilascio storici | `docs/quality/RELEASE_READINESS.md`, `docs/quality/FINAL_RELEASE_ASSESSMENT.md`, `docs/architecture/MULTITENANCY_RELEASE_GATE.md` | Prove e gate su 5.7.24 |

Alcuni di questi file sono nella closure di rilascio 8.4: `IntegrationFixture.cs`, i file in `tools/` e `infra/`, `docs/development/LOCAL_DATABASE_SETUP.md`. La loro modifica nel Lotto 3 richiede la rigenerazione deliberata del manifest.

## Fuori dall'ambito di questa decisione

- Scelta del provider EF Core 10 (INC-001).
- Accettazione del rischio EF Core 9: proposta, non approvata.
- D2–D6, che restano aperte.
