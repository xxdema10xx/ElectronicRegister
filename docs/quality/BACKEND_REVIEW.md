# Review backend ElectronicRegister: incompatibilità e vulnerabilità

> Review di sola lettura. Non è stato modificato codice, configurazione o database, e non sono stati fatti commit o push. Questo file è l'unico creato.

## 1. Sintesi

| Voce | Valore |
|---|---|
| Branch / commit | Analisi statica su `refactor-database-entities` @ `f8c0425` ("checkpoint: lorenzo -> dema"). Aggiornamento del 2026-11-05 su `b53b10b` (revert di `f8c0425`). Il codice di `b53b10b` è identico a `03bb1be`; `f8c0425` differiva solo per una riga vuota in `EmailCategory.cs` |
| Data | 2026-11-05 (prima stesura e aggiornamento) |
| Stato decisioni | **D1 decisa** (MySQL 8.4 LTS, vedi [DATABASE_TARGET_MYSQL84.md](../architecture/DATABASE_TARGET_MYSQL84.md)). **D2 decisa** (nessuna registrazione libera, modello account del proprietario, vedi §1.1). D3–D6 aperte |
| Perimetro | `Backend/ElectronicRegisterAPI` (Api, Application, Business, Domain, Infrastructure, tools/Mt3*), `migrations/`, `infra/`, `global.json`, `Directory.Build.props`, `tools/` (DB, rilascio, PlatformAdmin) |
| Problemi | **29** in tutto: 18 incompatibilità e 11 vulnerabilità (INC-017 e INC-018 approvati dopo la baseline) |
| Per severità | **Critica 0 · Alta 4 · Media 8 · Bassa 17** (INC-006 riclassificato da Media a Bassa; INC-017 Alta, INC-018 Bassa) |
| Verdetto | **NON PRONTO per la produzione** |

**Isolamento multi-tenant.** Non ho trovato falle sfruttabili via HTTP. La conclusione nasce da analisi statica. La baseline su MySQL 8.4.13 ([BASELINE_MYSQL84.md](BASELINE_MYSQL84.md): 1566 casi superati, 16 falliti e 94 non eseguiti su 1676) la **conferma per i percorsi HTTP, i repository, le scritture applicative e il worker**. Dopo il Lotto 0B (INC-017) la conferma vale, **in locale su 8.4**, anche per la revoca dei token basata sulla security generation, le mutazioni di ruolo e l'isolamento delle mutazioni IAM: `Mt5bJwtTests` 23/23, `Mt5bSecurityStateTests` 39/40, `Mt5dMembershipMutationTests` 31/32. I 2 casi non superati osservano l'holding legacy assente dal bootstrap 8.4 (INC-012) e non riguardano revoca, escalation o isolamento. **La conferma in CI arriverà solo dopo la prima run verde** del nuovo dominio `iam-security`. Resta non verificata su 8.4 la CLI di piattaforma. L'isolamento IAM via HTTP è invece coperto in parte da `Mt5eIamHttpTests` (53/53) e `Mt5aMembershipAdminTests` (36/36). Per le aree non verificate resta un risultato da analisi statica. Il blocco viene da tre punti. La piattaforma dati esce dal supporto: EF Core 9 termina il 2026-11-10 e non esiste un provider Pomelo per EF Core 10. Il database target è deciso (D1: MySQL 8.4 LTS), ma il ramo 5.7 è ancora presente nel codice, nei gate e negli strumenti. Infine, in produzione non esiste un percorso per l'onboarding.

### 1.1 Modello account definitivo (decisione del proprietario, 2026-11-05)

- Non esiste registrazione libera: `POST /api/identity/accounts` è stato rimosso e risponde 404.
- Il proprietario della piattaforma crea l'istituto e un solo InstitutionAdmin iniziale con lo script `tools/OwnerProvisioning`. Lo script resetta anche la password di qualsiasi account. Per ora sostituisce la CLI PlatformAdmin, che resta necessaria per la produzione (INC-008).
- Poi l'istituto si gestisce da solo con `POST /api/institution/accounts`:
  - InstitutionAdmin crea account di ogni ruolo, compresi altri InstitutionAdmin, senza limiti di numero;
  - Staff crea solo account Student o Teacher;
  - resta solo il blocco sulla rimozione dell'ultimo admin.
- Un account appartiene a un istituto e l'istituto viene sempre dal token.
- Email istituzionale nello spazio globale esistente: un'email già usata altrove produce un 409 unico, identico in ogni caso e con lo stesso costo BCrypt di una creazione riuscita. Rischio residuo accettato: si capisce che l'indirizzo è "occupato", non dove.
- Password temporanea generata dal sistema e mostrata una sola volta. Il cambio al primo accesso è imposto dal server su ogni token. Il reset incrementa la generation e revoca tutti i token precedenti.
- Procedure, flag e limiti: [INSTITUTION_ACCOUNTS.md](../operations/INSTITUTION_ACCOUNTS.md).

**I 5 rischi principali**

1. **INC-001 (Alta).** EF Core 9.0.8 e Pomelo 9.0.0 escono dal supporto Microsoft il 2026-11-10. Pomelo non ha una release per EF Core 10, quindi l'aggiornamento richiede di cambiare provider o di aspettare.
2. **INC-002 (Alta).** Il target è deciso (D1: MySQL 8.4 LTS) ma il ramo 5.7 è ancora presente: README, gate di rilascio, readiness e PlatformAdmin indicano 5.7.24, che è fuori supporto da ottobre 2023 e ha un problema TLS documentato. CI e staging usano invece 8.4.13.
3. **INC-008 (Alta).** La CLI PlatformAdmin, unico ingresso per onboarding, governance e recovery, accetta soltanto il database di test su loopback (5.7.24). In produzione non si può creare il primo istituto o il primo amministratore senza SQL manuale.
4. **VULN-001 e VULN-002 (Media).** L'autenticazione è esposta ad abusi: la registrazione è anonima e restituisce 409 sulle email esistenti, il login ha una differenza di tempo che rivela le email, e manca un limite per account. Esiste solo il limite di 60 POST al minuto per IP, condiviso con tutte le scritture.
5. **INC-004 (Media).** Il `sql_mode` non è imposto in produzione. I test forzano `STRICT_TRANS_TABLES` (`ER_TEST_STRICT_SESSION`), mentre il MAMP 5.7 di riferimento è documentato come non strict: sono possibili troncamenti e date invalide silenziosi.

## 2. Metodo e copertura

### Comandi eseguiti

| Comando | Esito |
|---|---|
| `dotnet --info` | SDK **10.0.401** (MSBuild 18.9.11), host/runtime **10.0.12**, ASP.NET Core 10.0.12 (presenti anche 8.0.32). `global.json`: 10.0.401, `rollForward: latestFeature` |
| `dotnet restore …slnx --locked-mode` | **OK**, 8 progetti ripristinati, lock file coerenti |
| `dotnet build …slnx --no-restore -c Release` | **OK**: 0 errori, **19 avvisi, tutti in `tests/Integration`** (CA1416: API ACL solo Windows in `PrivateTestFiles.cs:24` e `Mysql84LocalReleaseTarget.cs:47`; CS8602/CS8629 in `Phase2B_AdditionalTests.cs` e `OpenScoreDoubleCountTests.cs:45`). Nessun avviso nel codice di produzione |
| `dotnet list … package --vulnerable --include-transitive` | **Nessun pacchetto vulnerabile** con le origini correnti (nuget.org) |
| `dotnet list … package --deprecated --include-transitive` | Deprecati solo nei test: `xunit` 2.5.3 e `xunit.*` (Legacy, sostituto `xunit.v3`) |
| `dotnet list … package --outdated --include-transitive` | EF Core 9.0.8 → 10.0.14; JwtBearer 10.0.8 → 10.0.14; AspNetCore.OpenApi 10.0.10 → 10.0.14; Microsoft.OpenApi 2.7.5 → 3.11.0; IdentityModel.* 8.0.1 → 8.23.0; Extensions.Options 10.0.0 → 10.0.14; MySqlConnector 2.6.1 → 2.6.2; Scalar 2.14.14 → 2.18.7. **Pomelo 9.0.0 non risulta "outdated": sul feed non esiste una versione stabile più recente** |
| `dotnet test tests/Unit … --no-build --list-tests` | OK, 598 test elencati |
| `dotnet test tests/Integration … --no-build --list-tests` | OK, 1676 test elencati (non eseguiti: richiedono MySQL reale) |
| `node tools/release-artifacts.cjs` (modalità verifica, senza `--write`) | Fallisce con `Release closure/hash mismatch`. **Era il controllo sbagliato per HEAD**: verifica la release storica er-quality-1 (`migrations/release/manifest.json`, SHA `8e7d9ec…`). Nell'aggiornamento: fallisce anche su `03bb1be`, che era verde, e passa sullo snapshot `8e7d9ec` (760 artefatti). Vedi INC-006 e §7 |
| Ricalcolo hash di `migrations/release/manifest.json` (node, sola lettura) | 760 file: 733 coincidono, 27 divergenti, 0 mancanti. È una divergenza attesa: il manifest storico è immutabile per costruzione |
| `node tools/mysql84-release.cjs` (aggiornamento, su `b53b10b`) | **OK**: `er-quality-2-mysql84: new closure and immutable predecessor verified`. È il controllo corretto per HEAD |
| `node tools/test-mysql84-release.cjs` (aggiornamento) | **OK**: 1117 file di closure e checksum verificati, 12 controlli negativi del manifest superati, bundle SQL verificati offline (runtime non testato) |
| Ricalcolo hash di `docs/refactoring/INSTITUTIONAL_API_SURFACE.json` | 28 controller: 25 coincidono, 3 divergenti (vedi INC-014) |
| `dotnet build` di `tools/Mt3aMigration` e `tools/Mt3cTestPreparation` (fuori dalla soluzione) | OK, 0 avvisi. Nota: il build ha generato due `packages.lock.json` e le cartelle `obj/`, che ho **cancellato subito**; `git status` torna pulito |
| Scansione della cronologia git (`git log --all -p -G…`) | La cronologia **non è shallow** (199 commit). Le sole occorrenze sono fixture sintetiche e segnaposto (vedi §7) |

### Cosa ho analizzato

- **Tutti i 28 controller**, 195 action (§6), più le 2 minimal API di health. La richiesta parlava di 30 file: gli altri due che contengono "Controller" sono `CalendarControllerFeatureProvider.cs` e `FormativePlanControllerFeatureProvider.cs`, che non sono controller.
- Pipeline e sicurezza: `Program.cs`, `OperationalPipeline.cs`, `RuntimePolicy.cs`, `InstitutionAuthentication.cs`, `InstitutionAuthorizationHandler.cs`, `InstitutionEndpointGate.cs`, `StaffBodyContractAttribute.cs`, `GlobalExceptionHandler.cs`, i token, Microsoft, BCrypt, `PlatformAuthority`/`PlatformSigning` e la CLI `tools/PlatformAdmin`.
- Isolamento: `InstitutionSchoolQueries`, `InstitutionExamDataScope`, `InstitutionExamRepositoryFactory`, `SubmissionExpiryStore` con il worker, e i repository per studenti, docenti, classi, email, import, calendario, membership, governance e identità.
- **SQL esplicito.** I file con `FromSqlRaw`, `ExecuteSqlRaw`, `CommandText` o `MySqlCommand` sono **36**: 34 in `src/` (il numero indicato) e 2 in `Backend/ElectronicRegisterAPI/tools/`. Contando anche `FromSqlInterpolated`, `ExecuteSqlInterpolated` e `SqlQuery`, sono **44**. **Li ho verificati tutti e 44** con una ricerca mirata delle costruzioni dinamiche (`$"…{x}…"`, concatenazioni, `string.Join`) e ho letto il contesto di ogni frammento dinamico. Esito in §7.
- Import CSV, XLSX e calendario (`InstitutionImportFileReader*.cs`, `InstitutionImportRules.cs`, `CalendarImportRules.cs`).
- Schema: `migrations/bootstrap/001_schema.sql` (66 tabelle) e `migrations/identity-account/001–005`. Ho confrontato le tabelle mappate da EF (59, tutte presenti nel bootstrap) con quelle dello schema: 7 tabelle sono usate solo via SQL raw e 4 tabelle identity sono fuori dal bootstrap.
- `infra/` (Dockerfile, nginx, esempi runtime, compose e script di staging) e `.github/workflows` (solo versioni DB).
- Documenti in `docs/architecture`, `docs/refactoring` e `docs/quality`, trattati come ipotesi e confrontati con il codice.

### Cosa NON ho potuto verificare

- Nella prima stesura **nessun test è stato eseguito**, perché le regole vietano di collegarsi a un database. I risultati TRX versionati non sono stati considerati prove. Nell'aggiornamento del 2026-11-05 è stata eseguita la baseline su MySQL 8.4.13 usa e getta: Unit 598/598, Integration 1566 superati, 16 falliti, 94 non eseguiti su 1676; nessun bug reale tra i test arrivati a verificare il comportamento ([BASELINE_MYSQL84.md](BASELINE_MYSQL84.md)).
- Comportamento runtime su MySQL 5.7.24 e 8.4 (deadlock, lock, TLS, `caching_sha2_password`, sql_mode globale effettivo).
- OpenAPI generata a runtime: viene esposta solo in Development e non ho avviato l'host.
- Configurazione reale di produzione, proxy, certificati e privilegi effettivi degli account DB.
- `Frontend/` (escluso dal perimetro).
- I cicli di vita dei pacchetti sono verificati su fonti web pubbliche (link in INC-001). Non cito CVE perché non ne ho verificata nessuna.

## 3. Tabella riassuntiva

| ID | Titolo | Severità | Stato | Area | Impegno | Dipendenze |
|---|---|---|---|---|---|---|
| INC-001 | EF Core 9 / Pomelo 9 su net10: fine supporto 2026-11-10 e nessun percorso Pomelo 10 | Alta | CONFERMATO | Pacchetti | L | D1, INC-002 |
| INC-002 | Target deciso (D1, 8.4 LTS) ma ramo 5.7.24 EOL ancora presente in codice, gate e strumenti | Alta | CONFERMATO | Database | L | D1 |
| INC-008 | PlatformAdmin accetta solo il DB di test: nessun onboarding o governance in produzione | Alta | CONFERMATO; necessario prima della produzione (oggi script manuale del proprietario) | Rilascio/autorità | M | INC-002, D3 |
| INC-017 | Test IAM, security generation e JWT (`Mt5bSecurityStateTests`, `Mt5dMembershipMutationTests`, `Mt5bJwtTests`: 94 casi) legati a MAMP 5.7 su 13306 tramite `TestMySqlTarget.Port`: non eseguiti sul target 8.4 né inclusi nei filtri della CI 8.4 | Alta | RISOLTO in locale (Lotto 0B); CI da confermare con la prima run verde | Test/copertura | M | D1 |
| INC-003 | Versione MySQL esatta hard-coded (`8.4.13`, `5.7.24`) in validatori, health e CLI | Media | CONFERMATO | Database | S | INC-002 |
| INC-004 | `sql_mode` strict imposto solo nei test, non in produzione né in readiness | Media | CONFERMATO | Database | S | INC-002 |
| INC-007 | `ServerVersion.AutoDetect` a ogni scope DbContext | Media | PROBABILE | Database/prestazioni | S | INC-002 |
| INC-009 | nginx di rilascio senza `client_max_body_size` (1 MB di default) contro upload da 10–64 MB | Media | CONFERMATO | Infra | S | VULN-006 |
| INC-012 | Codice legacy compilato in produzione e repository con fallback senza tenant | Media | CONFERMATO | Legacy | M | — |
| INC-005 | Bootstrap canonico non compatibile con 8.4 senza adattatore (DEFINER `er_test`, `row_number`, `NO_AUTO_CREATE_USER`) | Bassa | CONFERMATO (deliberato) | Schema | S | INC-002 |
| INC-006 | Closure di rilascio 8.4 sensibile a ogni byte: nel piano mancava la procedura di rigenerazione | Bassa | CONFERMATO | Rilascio/processo | S | §8 (regola di processo) |
| INC-010 | `release.runtime.example.json` incoerente: Production non avviabile e versione 5.7.24 | Bassa | CONFERMATO | Infra | S | INC-002 |
| INC-011 | Dipendenze tra i flag `Multitenancy:*` implicite e non validate all'avvio | Bassa | CONFERMATO | Configurazione | S | — |
| INC-013 | Differenze Windows/Linux: `/run/secrets`, PlatformAdmin solo Windows, test ACL | Bassa | CONFERMATO | Ambienti | S | INC-008 |
| INC-014 | `INSTITUTIONAL_API_SURFACE.json` disallineato (3 controller) e ritiro legacy da comunicare | Bassa | CONFERMATO | Contratti API | S | — |
| INC-015 | Il controllo di integrità della cancellazione classe scansiona tutti i tenant a runtime | Bassa | CONFERMATO | Robustezza | S | — |
| INC-016 | Toolchain dei test deprecata (xunit 2.5.3 Legacy, FluentAssertions 6) | Bassa | CONFERMATO | Test | M | — |
| INC-018 | L'esecuzione della suite di integrazione riscrive 6 file versionati in `tests/Integration/TestResults/` (osservazioni SQL) | Bassa | RISOLTO (Lotto 0B) | Test/igiene | S | VULN-007 |
| VULN-001 | Enumerazione account: registrazione anonima con 409 e timing del login | Media | RISOLTO (registrazione rimossa, login a costo uniforme); residuo accettato: spazio email globale | Autenticazione | S | D2 |
| VULN-002 | Nessun limite per account sul login: solo 60 POST/min per IP condivisi | Media | CONFERMATO | Autenticazione | M | VULN-003 |
| VULN-003 | Rate limit per IP collassato se `KnownProxies` non è configurato (consentito dalla policy) | Media | PROBABILE | Infra applicativa | S | INC-009 |
| VULN-004 | Login Microsoft senza flag: con AzureAd non configurato produce 500 e chiamate esterne; validazione JWT lasca | Bassa | PROBABILE | Autenticazione | S | D5 |
| VULN-005 | Unica chiave HS256 per i due schemi, senza `kid` né rotazione | Bassa | CONFERMATO | Chiavi | M | — |
| VULN-006 | Upload: anteprima calendario senza limite di concorrenza; limiti richiesta e file incoerenti | Bassa | PROBABILE | Import | S | INC-009 |
| VULN-007 | Evidenze versionate con percorsi utente, nomi macchina e dominio reale | Bassa | CONFERMATO | Dati personali | S | — |
| VULN-008 | Immagini base Docker per tag e non per digest | Bassa | CONFERMATO | Deploy | S | — |
| VULN-009 | Account migrator di staging con `CREATE USER`, `SET_ANY_DEFINER` e `GRANT OPTION` | Bassa | CONFERMATO | Deploy | S | — |
| VULN-010 | Un singolo InstitutionAdmin può nominare altri admin o sospenderli senza doppio controllo | Bassa | ACCETTATO dal modello definitivo (admin senza limiti, solo blocco dell'ultimo admin) | Autorizzazione | M | D4 |
| VULN-011 | Il gate MVC lascia passare senza controlli gli endpoint con marker Identity; nessuna FallbackPolicy | Bassa | CONFERMATO | Autorizzazione | S | — |

Il conteggio di VULN-011 include anche l'osservazione sulla retention dei dati personali, riportata come "DA VERIFICARE" in §5 e non conteggiata a parte. Totale: 4 Alte + 8 Medie + 17 Basse = 29.

### Dettaglio degli ID approvati dalla baseline MySQL 8.4 (INC-017, INC-018)

Questi due ID vengono dalla [baseline](BASELINE_MYSQL84.md), sono **approvati** e sono inclusi nei conteggi e nella tabella qui sopra. Sono pianificati nel Lotto 0B (§8).

**INC-017 (Alta)**
- **Severità.** La revoca dei token basata sulla security generation, le mutazioni di ruolo e l'isolamento delle mutazioni IAM (`Mt5bJwtTests`, `Mt5bSecurityStateTests`, `Mt5dMembershipMutationTests`) non hanno prove eseguite sul motore di produzione, né in locale né in CI. L'isolamento IAM via HTTP è invece coperto in parte da `Mt5eIamHttpTests`.
- **Evidenza.** `tests/Integration/Mt5bJwtTests.cs:52`, `tests/Integration/Mt5dMembershipMutationTests.cs:61`, `tests/Integration/Mt5bSecurityStateTests.cs:64`, `tests/Integration/TestMySqlTarget.cs:13-16`, `tests/Integration/IntegrationFixture.cs:189` (`VERSION() = 5.7.24`).
- **Scenario.** Una regressione 8.4 nella revoca della generation o nell'isolamento IAM passa inosservata, perché questi test girano solo contro MAMP 5.7.
- **Fix.** Far usare a queste classi la fixture 8.4 usa e getta (`Mysql84FixtureTarget`) come le altre, senza indebolire i controlli d'identità, e aggiungerle alla CI 8.4.
- **Verifica.** Tutti i casi eseguiti davvero su 8.4, in locale e in CI, con ogni fallimento spiegato.
- **Stato (Lotto 0B).** Risolto in locale; CI da confermare.
  - Sul ramo 8.4 `Open()` attesta il server (`Mysql84FixtureTarget.VerifyAsync`) e imposta la capability di sessione `@er_mt5b_fixture_capability` (`tests/Integration/Mysql84OwnedSession.cs`), che sostituisce il predicato d'identità 5.7 nei guard di purge. Il requisito `@er_mt5b_fixture_owners` è invariato. L'asserzione sulla porta 13306 vale solo sul ramo 5.7; sul ramo 8.4 porta, versione, host, datadir e container sono già attestati dalla fixture.
  - `Mysql84CapabilityScopeTests` (2/2) dimostra che il reset del pool azzera la capability sulla stessa sessione fisica e che il percorso applicativo (`AddInfrastructure`) non la vede mai.
  - Il test `Sql_FixtureGuardRequiresExplicitOwnerAndSyntheticCodeAndExcludesHolding` è stato diviso in due. `Sql_FixtureGuardRequiresExplicitOwnerAndSyntheticCode` conserva i due rifiuti 1644 e aggiunge un controllo positivo: con owner esplicito e codice sintetico la stessa sessione cancella. Questo prova che i rifiuti vengono dalle regole su owner e codice, non dalla mancanza di capability. `Sql_FixtureGuardExcludesRealLegacyHolding` contiene solo l'osservazione su `MT3A_LEGACY_TEST` ed è legato a INC-012.
  - Risultati locali (container 8.4.13 usa e getta; stessi numeri con le classi eseguite separatamente e insieme): `Mt5bJwtTests` 23/23, `Mt5bSecurityStateTests` 39/40, `Mt5dMembershipMutationTests` 31/32, `Mysql84CapabilityScopeTests` 2/2. I 2 non superati sono le osservazioni dell'holding legacy (INC-012). Nessun nuovo ID.
  - CI: nuovo dominio `iam-security` con 4 shard (`tools/integration_partition.py`, matrice di `.github/workflows/mysql84-remediation-candidate.yml`), famiglia di preflight `identity`. Ha 52 metodi; i 2 metodi legati a INC-012 sono nella lista esplicita `EXCLUDED_METHODS`, riportati come SKIPPED con ID e motivo. `tools/test_integration_partition.py` fallisce se la lista cambia.
- **Collegati ma fuori da questo ID.** Le dipendenze dei test da regole FK 5.7 (`RESTRICT`, vedi INC-003) e dall'holding legacy MT3A (vedi INC-012).

**INC-018 (Bassa)**
- **Evidenza.** Dopo una normale `dotnet test` risultano modificati `tests/Integration/TestResults/mt3c/http-query-observations.json`, `tests/Integration/TestResults/mt3d/http-query-observations.json`, `tests/Integration/TestResults/mt3d/write-statements.sql.txt`, `tests/Integration/TestResults/mt3e/worker-query-observations.json`, `tests/Integration/TestResults/mt4b/course-sql.json` e `tests/Integration/TestResults/mt5a/directory-sql.json`.
- **Scenario.** Uno sviluppatore committa evidenze rigenerate senza volerlo, oppure le evidenze diventano non deterministiche.
- **Fix.** Scrivere le osservazioni in una cartella ignorata, oppure confrontarle senza riscriverle.
- **Verifica.** `git status` pulito dopo l'esecuzione della suite.
- **Stato (Lotto 0B).** Risolto. Le osservazioni vanno in `AppContext.BaseDirectory/test-evidence/<area>` (`tests/Integration/TestEvidence.cs`). La cartella è sotto `bin/`, quindi ignorata da git, esclusa dalla closure (`git ls-files --exclude-standard`) e assente dagli artifact CI, che caricano solo `.diagnostics/`. I file versionati in `tests/Integration/TestResults/` restano l'evidenza storica e non vengono più riscritti.

## 4. Dettaglio incompatibilità

### INC-001 · EF Core 9 / Pomelo 9 su net10: supporto in scadenza e nessun percorso di aggiornamento diretto (Alta, CONFERMATO)

**Severità.** Fra 5 giorni lo stack di accesso ai dati non riceverà più patch di sicurezza, e l'unico aggiornamento disponibile richiede di cambiare provider.

**Evidenza**

- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/ElectronicRegisterAPI.Infrastructure.csproj:19`: `Microsoft.EntityFrameworkCore 9.0.8`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/ElectronicRegisterAPI.Infrastructure.csproj:25`: `Microsoft.EntityFrameworkCore.Tools 9.0.0`, disallineato da Design/Relational 9.0.8
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/ElectronicRegisterAPI.Infrastructure.csproj:32`: `Pomelo.EntityFrameworkCore.MySql 9.0.0`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/packages.lock.json:127`: Pomelo vincola `"Microsoft.EntityFrameworkCore.Relational": "[9.0.0, 9.0.999]"`, quindi EF 10 è impossibile con Pomelo 9
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/ElectronicRegisterAPI.Infrastructure.csproj:29-30`: `Microsoft.Extensions.Options 10.0.0`, `Microsoft.IdentityModel.Protocols 8.0.1`

**Analisi del grafo.** Il restore in locked mode passa senza downgrade (nessun NU1605). A runtime le `Microsoft.Extensions.*` 9.0.8 richieste da EF vengono soddisfatte dal framework condiviso 10.0.12, e la combinazione EF 9 + Pomelo 9 su .NET 10 è indicata come compatibile da un maintainer Pomelo (issue #2016). JwtBearer 10.0.8 accetta IdentityModel 8.0.1, ma i riferimenti diretti a 8.0.1 tengono l'intero albero IdentityModel alla versione minima (8.23.0 disponibile). OpenApi 10.0.10 con Microsoft.OpenApi 2.7.5 è coerente; la 3.x richiede AspNetCore.OpenApi 11 e non va adottata ora.

**Fatti verificati (aggiornamento del 2026-11-05: NuGet v3 API e Microsoft Learn)**

- **EF Core 9.** È supportato fino al **2026-11-10** e dichiara **.NET 8** come target: [EF Core releases and planning](https://learn.microsoft.com/en-us/ef/core/what-is-new/), [What's New in EF Core 9](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-9.0/whatsnew). Il nuspec di `microsoft.entityframeworkcore` 9.0.8 contiene solo il gruppo `net8.0`. L'uso su .NET 10 si regge **soltanto** sulla dichiarazione di un maintainer Pomelo ([issue #2016](https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/issues/2016)): non c'è una garanzia di Microsoft. La prima stesura indicava la data come un'inferenza dal ciclo di .NET 9; ora è confermata dalla documentazione EF.
- **Pomelo.** Su NuGet l'ultima versione è **9.0.0**. Non esistono versioni 10.x, nemmeno prerelease ([releases](https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/releases)).
- **Oracle `MySql.EntityFrameworkCore`.** La **10.0.12** esiste, con gruppi `net8.0`, `net9.0` e `net10.0`. Nel gruppo `net10.0` dipende da **`MySql.Data` 26.10.0** e da EF Core 10.0.12. Esiste anche la 9.0.20 per EF 9. Adottarlo porta **un secondo driver ADO.NET** accanto a MySqlConnector, oppure richiede di migrare tutto il codice ADO a MySql.Data.
- **Altri pacchetti.** .NET 8 e 9 sono fuori supporto dal 2026-11-10 ([annuncio](https://devblogs.microsoft.com/dotnet/dotnet-8-9-end-of-support/)). La pagina dei provider Microsoft elenca Pomelo per EF 8 e 9 ([providers](https://learn.microsoft.com/en-us/ef/core/providers/)).

**Impatto reale nel codice (riconteggio del 2026-11-05, solo `Backend/ElectronicRegisterAPI/src`)**

| Uso | Occorrenze |
|---|---|
| File `.cs` che importano `MySqlConnector` | **33** (35 con `Backend/ElectronicRegisterAPI/tools`; 4 in `tools/PlatformAdmin`; **58** in `tests/`) |
| `new MySqlCommand` / `new MySqlConnection` | 63 |
| Riferimenti a `MySqlException` | 23 in 14 file |
| Controlli su `Number` (1062, 1451, 1213, 1205, 3995) | 19 |
| `MySqlConnectionStringBuilder` (inclusi `RuntimePolicy` e readiness) | 8 |
| Cast `(MySqlConnection)` | 1 |
| `FOR UPDATE` | **69 testuali in 24 file**, di cui 4 nei commenti, quindi **65 nel codice**. La stima di 68 non coincide con il riconteggio: la differenza dipende da cosa si conta (commenti e concatenazioni come `" FOR UPDATE"` in `IdentityAccountRepository.cs:79`) |
| `LOCK IN SHARE MODE` | 13 |

Non è una sostituzione di pacchetto. Il SQL raw è in gran parte portabile, ma toccano direttamente il driver: la mappatura degli errori, il builder della connection string (`SslMode`, `SslCa`, `AllowUserVariables`), i comandi ADO in transazione EF (`GetDbTransaction`) e la composizione di `FromSql*` con `FOR UPDATE` e `Include`.

**Scenario.** Dopo il 2026-11-10 una vulnerabilità di EF Core 9 o delle sue dipendenze non verrà più corretta. Il team non può passare a EF 10 senza sostituire Pomelo, e la sostituzione tocca il driver.

**Fix proposto.** Il target DB è deciso (D1, MySQL 8.4 LTS). **Il provider finale non va scelto ora.**

- (a) **Subito, entro l'accettazione temporanea del rischio** (proposta in §9, da confermare): allineare `Tools` a 9.0.8, portare IdentityModel a 8.23.x e Options/JwtBearer/OpenApi a 10.0.14. Si resta su EF 9 + Pomelo 9.
- (b) **Per decidere il provider** servono:
  1. uno spike su un branch usa e getta con Oracle `MySql.EntityFrameworkCore` 10.0.12 sulla suite completa su 8.4;
  2. una verifica di convivenza MySqlConnector/MySql.Data nello stesso processo, oppure il costo di migrare le 63 istanze ADO e i 58 file di test;
  3. una verifica del mapping `char(36)` ASCII ↔ `Guid` (`Mysql84GuidMetadataTests`) e della traduzione di `FromSqlInterpolated(... FOR UPDATE)` composta con `Include`/`AsSplitQuery`;
  4. la mappatura dei codici di errore (1062, 1213, 1205) su `MySql.Data.MySqlClient.MySqlException`;
  5. TLS `VerifyFull` con CA e `caching_sha2_password` sul driver Oracle;
  6. licenza di MySql.Data (risulta GPLv2 con eccezione FOSS: da confermare per questo uso) e cadenza di rilascio;
  7. lo stato di Pomelo 10 alla data di revisione.

**Impegno.** L. **Rischi di regressione:** mapping GUID/collation, traduzione LINQ, lock `FOR UPDATE`/`LOCK IN SHARE MODE`, classificazione degli errori di concorrenza.

**Come verificare.** Baseline su 8.4 (`docs/quality/BASELINE_MYSQL84.md`) come riferimento, poi la stessa suite con il provider candidato e un confronto caso per caso. Manca un test che fissi le versioni minime del provider.

### INC-002 · Target MySQL deciso (D1) ma ramo 5.7 ancora presente (Alta, CONFERMATO)

> **Aggiornamento del 2026-11-05.** D1 è decisa: MySQL 8.4 LTS è l'unico target di produzione ([DATABASE_TARGET_MYSQL84.md](../architecture/DATABASE_TARGET_MYSQL84.md)). Il problema resta aperto finché il Lotto 3 non rimuove il ramo 5.7 dal codice e dalla documentazione.

**Severità.** Il codice deve supportare due motori con comportamenti diversi, e quello indicato come target (5.7.24) è fuori supporto dal fornitore e ha un gate TLS aperto.

**Evidenza**

- `README.md:3`: "MySQL 5.7.24"
- `docs/quality/RELEASE_READINESS.md:27`: "Il binario MySQL 5.7.24 MAMP … espone per default TLS 1.0/1.1 … Non è quindi certificato"
- `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Operations/OperationalPipeline.cs:151`: la readiness accetta sia 8.4 (`VERSION() NOT LIKE '8.4.%' OR …er-quality-2-mysql84`) sia la variante 5.7
- `…OperationalPipeline.cs:155`: default LocalTest `"5.7.24"`
- `tools/PlatformAdmin/Program.cs:85`: richiede `"5.7.24"`
- `infra/staging/mysql-image.json` e `.github/workflows/mysql84-remediation-candidate.yml:20`: `mysql:8.4.13@sha256:…`
- `tools/prepare-release-sql.cjs:12`: sono supportate due serie (5.7 di default e `8.4`)

**Differenze che cambiano il comportamento** (verificate nel codice dove indicato):

- **Parole riservate.** `ROW_NUMBER` è riservata da 8.0 e la colonna `institution_import_item.row_number` (`migrations/bootstrap/001_schema.sql:2950`) va quotata. Il runtime la quota (`InstitutionImportRepository.cs:213`) e l'adattatore la riscrive (`tools/mysql84-contract.cjs:25`).
- **sql_mode.** `NO_AUTO_CREATE_USER` (`001_schema.sql:5`) non esiste più in 8.x e l'adattatore la rimuove. Il problema strict è descritto in INC-004.
- **FK.** In 8.4 la regola di default viene riportata come `NO ACTION`, in 5.7 come `RESTRICT`: gestito, ma con versione esatta (INC-003).
- **Collation e charset.** Le tabelle dichiarano esplicitamente `utf8mb4_unicode_ci` e `ascii_*`, quindi il default 8.x (`utf8mb4_0900_ai_ci`, NO PAD) non le tocca. Restano a rischio le stringhe di sessione e le nuove tabelle senza collation esplicita. Le REGEXP usate (85, solo classi POSIX `[[:space:]]` e range) sono compatibili con ICU in 8.x: non ho trovato `[[:<:]]` né backreference.
- **Autenticazione.** In 8.4 `mysql_native_password` è disabilitato di default. Il bootstrap di staging usa `caching_sha2_password` + `REQUIRE SSL` (`infra/staging/db-bootstrap.sh:17`) e MySqlConnector lo supporta. Effetto runtime DA VERIFICARE.
- **TLS.** `RuntimePolicy` impone `SslMode=VerifyFull` e una CA in Staging/Production (`RuntimePolicy.cs:48`): corretto. Sul 5.7.24 MAMP il gate TLS 1.2 non è superato (vedi sopra).
- **DEFINER.** Vedi INC-005.
- **`LOCK IN SHARE MODE`.** È usato in molti punti (`InstitutionWriteAuthorization.cs:25-28`, `InstitutionExamDataScope.cs`). In 8.x è ancora accettato come sinonimo di `FOR SHARE`. Comportamento sotto deadlock DA VERIFICARE IN RUNTIME.
- **JSON e DateTime.** Le colonne JSON sono `longtext` o `json` (44 occorrenze). `DbUtc` e `UTC_TIMESTAMP(6)` rendono le scritture indipendenti dal fuso, ma lo staging imposta `--default-time-zone=+00:00` e la produzione no: le occorrenze di `CURRENT_TIMESTAMP` nello schema (33) dipendono dal fuso del server. DA VERIFICARE.

**Scenario.** Si va in produzione con 5.7.24: motore fuori supporto e TLS non certificato. Oppure si va con 8.4: le prove di regressione del 30 ottobre (FINAL_RELEASE_ASSESSMENT) riguardano 5.7.24, quindi la certificazione è parziale.

**Fix proposto.** Decidere (D1). Raccomando **8.4 LTS** come unico target. Poi: README e documenti di rilascio allineati, rimozione del ramo 5.7 da readiness, PlatformAdmin e prepare-release, e sola esecuzione dell'adattatore 8.4.

**Impegno.** L. **Dipendenze:** INC-003, INC-004, INC-005, INC-010. **Rischi:** dati esistenti su 5.7 da migrare con il percorso `mysql84-remediation`.

**Come verificare.** La CI `mysql84-remediation-candidate.yml` esiste già. Va aggiunto un job unico "target di produzione" e va rimossa la matrice 5.7.

### INC-003 · Versioni MySQL esatte hard-coded (Media, CONFERMATO)

**Severità.** Un aggiornamento di patch (8.4.13 → 8.4.x) rompe validatori, healthcheck e readiness: non comporta perdita di dati, ma blocca il rilascio.

**Evidenza**

- `…Persistence/Migrations/InstitutionClassIntegrity.cs:16`: `var defaultRule = connection.ServerVersion == "8.4.13" ? "NO ACTION" : "RESTRICT";`. La stessa riga compare in `InstitutionAdministrationIntegrity.cs:16`, `Mt5bSecurityStateIntegrity.cs:16`, `Mt5dMembershipEventIntegrity.cs:17`, `Mt5GovernanceIntegrity.cs:23` e `StudentClassTransferIntegrity.cs:14`.
- `infra/staging/db-health.sh:4`: `VERSION()='8.4.13'`
- `…OperationalPipeline.cs:161`: confronto esatto `reader.GetString(0) == expectedVersion`
- `tools/PlatformAdmin/Program.cs:85`: `"5.7.24"`

**Scenario.** Un DBA applica 8.4.14 per una patch di sicurezza. I validatori interpretano la regola come `RESTRICT` e falliscono, e il container DB non diventa healthy.

**Fix.** Confrontare la serie (`StartsWith("8.4.")` o `ServerVersion.Version >= 8.0`), oppure leggere la regola dai metadati. In readiness, rendere la versione attesa configurabile per prefisso. **Impegno:** S. **Rischi:** bassi.

**Verifica.** Test unitario che passi la versione "8.4.14" a un validatore. Oggi non esiste.

### INC-004 · `sql_mode` strict garantito solo nei test (Media, CONFERMATO)

**Severità.** Con un server non strict i dati vengono troncati o convertiti in silenzio. L'impatto è sull'integrità, non sulla riservatezza.

**Evidenza**

- `…Persistence/TestDatabaseSession.cs:12`: `Enabled => Environment.GetEnvironmentVariable("ER_TEST_STRICT_SESSION") == "1"`
- `…TestDatabaseSession.cs:27`: `SET SESSION sql_mode = CONCAT_WS(',', @@SESSION.sql_mode, 'STRICT_TRANS_TABLES')`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs:45-46`: l'intercettore è attivo solo se la variabile è impostata. Né `migrations/identity-account/005_release_gate.sql` né `er_schema_ready()` controllano `@@sql_mode`.
- `docs/architecture/MULTITENANCY_RELEASE_GATE.md:33`: "Falliscono l'assunzione GLOBAL non strict…"

**Scenario.** Il server di produzione è il MAMP 5.7 con sql_mode non strict. Un admin salva un nome oltre la lunghezza della colonna, o una data non valida arriva per un percorso non validato in C#: la riga viene salvata troncata o come `0000-00-00`, senza errore. I trigger mantengono il proprio sql_mode di creazione, ma le DML applicative usano quello del server.

**Fix.** Impostare `sql_mode` per sessione in produzione, con un intercettore non legato ai test o con l'opzione di connessione equivalente. In alternativa, verificare `@@SESSION.sql_mode` nella readiness e rispondere 503 se manca `STRICT_TRANS_TABLES`. **Impegno:** S. **Rischi:** query che oggi passano per tolleranza non strict.

**Verifica.** Nuovo test di integrazione che avvii l'host con server non strict e attenda 503 o errore. Oggi non esiste: i test forzano lo strict.

### INC-005 · Bootstrap canonico non applicabile così com'è a 8.4 (Bassa, CONFERMATO, deliberato)

**Severità.** Il problema si presenta solo se qualcuno applica il file a mano. Lo strumento di rilascio lo corregge.

**Evidenza**

- `migrations/bootstrap/001_schema.sql:825`: `CREATE DEFINER=\`er_test\`@\`localhost\``. Nel file ci sono 40 DEFINER `er_test`, l'account di test, e 59 `root@localhost`.
- `001_schema.sql:2950`: `row_number int` non quotato
- `001_schema.sql:5`: `NO_AUTO_CREATE_USER`
- `migrations/identity-account/005_release_gate.sql:3`: `DROP PROCEDURE er_schema_ready;` senza `IF EXISTS`, quindi la migrazione non è idempotente
- La riscrittura avviene in `tools/prepare-release-sql.cjs:22` (DEFINER di trigger e procedure) e in `tools/mysql84-contract.cjs:25`
- `migrations/bootstrap/README.md:9` prescrive lo script di setup

**Scenario.** Un operatore esegue `001_schema.sql` con il client mysql su 8.4: errore di sintassi su `row_number`. Su 5.7, invece, i trigger risultano di proprietà di `er_test@localhost`, un account di test.

**Deliberato.** Sì: il README vieta il replay e richiede lo strumento.

**Fix.** Generare e versionare due artefatti pronti per serie con definer neutro, oppure aggiungere in testa al file un guard che fallisca se eseguito fuori dallo strumento. **Impegno:** S.

**Verifica.** `tools/test-release-manifest.cjs` e `tools/test-mysql84.cjs` esistono già: va aggiunto un controllo "nessun `er_test` negli artefatti di rilascio".

### INC-006 · Closure di rilascio 8.4 sensibile a ogni byte, senza procedura nel piano (Bassa, CONFERMATO)

> **Riscritto nell'aggiornamento del 2026-11-05.** La prima stesura usava il controllo sbagliato. Si chiamava "Manifest di rilascio non allineato a HEAD" ed era classificata Media. La diagnosi originale è un falso positivo (vedi §7).

**Severità.** Rischio di processo, senza impatto su sicurezza o dati. Una modifica qualsiasi a un file della closure, anche solo spazi bianchi, blocca `verify.yml` e il gate MySQL 8.4 finché il manifest non viene rigenerato. Una rigenerazione fatta senza revisione certificherebbe invece codice non rivisto.

**Evidenza**

- Il controllo corretto per HEAD è `node tools/mysql84-release.cjs` (`.github/workflows/verify.yml:26,70`). Confronta la closure calcolata con `migrations/mysql84-remediation/manifest.json` e lancia `New release closure drift` se diverge (`tools/mysql84-release.cjs:67`). Su `b53b10b` passa.
- `tools/release-artifacts.cjs` valida invece la release storica er-quality-1 (`migrations/release/manifest.json`) legata a `8e7d9ecd42c001f1fa9b30e120535ce1bb0ef6e8`. Fallisce su `03bb1be` e su HEAD per costruzione, e passa sullo snapshot `8e7d9ec` (verificato con `git archive` in una cartella temporanea). `docs/quality/MYSQL_84_FULL_COMPATIBILITY_AUDIT.md:60` lo dice esplicitamente: "Il vecchio `release-artifacts.cjs` continua a certificare soltanto la vecchia sorgente".
- `.github/workflows/staging-package.yml:32` fa il checkout di `inputs.source_sha || '8e7d9ecd…'` prima di eseguire `release-artifacts.cjs` (`:67`). Quel workflow quindi non può fallire per divergenze su HEAD.
- Precedente concreto: `f8c0425` ha aggiunto una sola riga vuota a `EmailCategory.cs`, che è nella closure. Il risultato è stato `New release closure drift` in `Verify` e nel gate 8.4, poi il commit è stato annullato con `b53b10b`.
- La closure (`tools/mysql84-release.cjs:44-60`) include i file tracciati e non tracciati non ignorati sotto `Backend/ElectronicRegisterAPI/src`, `tests/Unit`, `tests/Integration`, `tools`, `infra`, `migrations/{mt1,mt3,identity-account}`. Include anche tutti i percorsi del manifest storico, tra cui 19 documenti esistenti in `docs/` (per esempio `docs/development/LOCAL_DATABASE_SETUP.md` e `docs/architecture/PLATFORM_OPERATOR_AND_ONBOARDING.md`).

**Scenario.** Il Lotto 1 modifica `IdentityRegistrationController.cs`. Il push fa fallire `Verify` con `New release closure drift`. Oppure uno sviluppatore esegue `--write` per sbloccare la CI senza guardare il diff, e la closure certifica codice non rivisto.

**Fix.** La regola di processo è in §8: rigenerazione deliberata con `node tools/mysql84-release.cjs --write` dopo la revisione del diff, poi una nuova verifica. Il manifest storico `migrations/release/manifest.json` **non va mai riscritto**. **Impegno:** S per ogni lotto. **Dipendenze:** tutti i lotti che toccano la closure.

**Verifica.** `node tools/mysql84-release.cjs` e `node tools/test-mysql84-release.cjs` con exit 0 dopo ogni lotto (esistono già).

### INC-007 · `ServerVersion.AutoDetect` a ogni scope (Media, PROBABILE)

**Severità.** Ogni richiesta che crea un DbContext fa un round trip in più verso il DB per scoprirne la versione. Il comportamento SQL dipende dal server raggiunto in quel momento.

**Evidenza.** `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs:44`: `options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));`. Il delegate di `AddDbContext` gira per ogni `DbContextOptions` (scoped di default), e Pomelo `AutoDetect` apre una connessione per interrogare la versione. L'effetto sulle prestazioni è DA VERIFICARE IN RUNTIME.

**Scenario.** Sotto carico, anche le richieste anonime come il login raddoppiano le aperture di connessione. Durante un failover verso una replica con versione diversa, la generazione SQL cambia senza preavviso.

**Fix.** Versione fissa da configurazione (`new MySqlServerVersion(new Version(8,4,0))`) coerente con D1, oppure rilevamento una sola volta all'avvio. **Impegno:** S.

**Verifica.** Test unitario sulla registrazione DI che controlli che non ci sia I/O nella costruzione delle opzioni. Oggi non esiste.

### INC-008 · Nessun percorso di produzione per onboarding, governance e recovery (Alta, CONFERMATO)

**Severità.** Senza onboarding il primo istituto e il primo admin si possono creare solo con SQL manuale, aggirando audit e doppia firma.

**Evidenza**

- `tools/PlatformAdmin/Program.cs:80`: `if(connection.Server!="127.0.0.1" || connection.Port!=testPort || connection.Database!="electronic_register_test")throw new UnauthorizedAccessException("Explicit isolated test target required.");`
- `tools/PlatformAdmin/Program.cs:85`: richiede `5.7.24`, l'hostname della macchina e la datadir MAMP
- `Program.cs:25,48`: solo Windows
- Questi percorsi passano soltanto dalla CLI: `InstitutionOnboardingRepository.cs:56` (`Multitenancy:Onboarding:Enabled`) e `InstitutionGovernanceRepository.cs:16`. Nessun controller espone le operazioni di piattaforma.
- `docs/architecture/MT5C_PROVISIONING_AUTHORITY_BLOCKER.md:7-8`: "Il provider produttivo e le chiavi reali restano azioni amministrative esterne".

**Scenario.** Al go-live l'operatore deve creare la prima fondazione e il suo InstitutionAdmin. La CLI rifiuta un DB diverso da `electronic_register_test` su loopback, quindi l'unica strada è un INSERT manuale: niente firma, niente nonce, niente audit `institution_onboarding_event`.

**Deliberato?** In parte. Il blocco documentato riguarda le chiavi e il provider, non il vincolo al DB di test.

**Fix.** Introdurre un target di produzione esplicito nella CLI: allowlist firmata di host e database, attestazione TLS VerifyFull, server UUID come in `release.mysql84.example.json`. Valutare un host Linux per l'esecuzione e lasciare la firma su Windows (INC-013). **Impegno:** M.

**Verifica.** Test di integrazione della CLI contro un target non di test con attestazione corretta ed errata. Oggi esistono solo i test sul target locale.

**Stato (2026-11-05): necessario prima della produzione.**
- Per l'uso iniziale il proprietario usa `tools/OwnerProvisioning`. Lo script si connette direttamente al DB e rifiuta un server con UUID diverso da quello atteso, un MySQL diverso da 8.4 e una release di schema diversa da `er-quality-2-mysql84`. Scrive l'audit `Operator` (`owner-script`).
- Non ha doppia firma, nonce, attestazione TLS automatica né allowlist dei target. È una procedura manuale e non sostituisce questo fix.

### INC-009 · nginx di rilascio con body massimo di default (1 MB) (Media, CONFERMATO)

**Severità.** Tutte le anteprime di import oltre 1 MB vengono rifiutate dal proxy con 413, prima ancora di arrivare all'API.

**Evidenza**

- `infra/nginx.release.conf:14-20`: il blocco `location /` non contiene `client_max_body_size`. Il default di nginx è 1 MB.
- `…Controllers/InstitutionImportController.cs:24`: `RequestSizeLimit(64 * 1024 * 1024 + 262144)`
- `…Controllers/InstitutionCalendarController.cs:12,88`: 12 MB
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs:35`: `MaxFileBytes` di default 10 MB

**Scenario.** Una segreteria carica un XLSX di 3 MB e riceve 413 da nginx, senza codice ProblemDetails.

**Fix.** Impostare `client_max_body_size` per location (`/api/institution/imports/preview` e `/api/institution/calendar/imports/preview`) al limite dell'API, e 1 MB altrove. **Impegno:** S.

**Verifica.** Smoke HTTP dietro proxy (`tools/StagingSupport` / `smoke.py`) con un file di 2 MB. Oggi non esiste.

### INC-010 · Esempio di runtime di rilascio incoerente (Bassa, CONFERMATO)

**Severità.** È solo un esempio, ma chi lo copia ottiene un servizio che non parte.

**Evidenza**

- `infra/release.runtime.example.json:2`: `"Mode": "Production", "RolloutApproved": false`
- `release.runtime.example.json:10-12`: `SchoolApi`, `CourseApi`, `ExamApi`, `MembershipAdminApi` e `MembershipWriteApi` sono `true`, mentre `RuntimePolicy.cs:52` fallisce se un flag è attivo senza `RolloutApproved`
- `release.runtime.example.json:6`: `"ExpectedVersion": "5.7.24"`, mentre lo staging usa 8.4.13

**Fix.** Allineare l'esempio a D1 e a una scelta coerente di `RolloutApproved`. **Impegno:** S.

**Verifica.** Test che carichi ogni `infra/*.example.json` in `RuntimePolicy.Read`. Oggi non esiste.

### INC-011 · Dipendenze tra flag `Multitenancy:*` non dichiarate (Bassa, CONFERMATO)

**Severità.** Il sistema fallisce in modo chiuso, quindi non c'è un rischio di sicurezza, ma le combinazioni incoerenti non danno errori.

**Evidenza**

- `…Security/InstitutionCourseEndpointAttribute.cs` e `InstitutionExamEndpointAttribute.cs`: CourseApi ed ExamApi richiedono anche `SchoolApi`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs:26`: Calendar = `InstitutionCalendar` + `SchoolApi`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs:30`: FormativePlans = `FormativePlans` + `SchoolApi` + `CourseApi`
- `…Security/InstitutionEndpointGate.cs:25`: `MembershipWriteApi` richiede `MembershipAdminApi`
- `…Security/CalendarControllerFeatureProvider.cs:11` e `FormativePlanControllerFeatureProvider.cs:13`: questi due flag vengono letti **solo all'avvio** (il controller viene rimosso), mentre le policy rileggono `IConfiguration` a ogni richiesta
- `RuntimePolicy.cs:48`: verifica ancora `Identity:Legacy:Enabled`, che il cutover dichiara obsoleto (`INSTITUTIONAL_API_AND_LEGACY_CUTOVER.md:31`)

**Scenario.** Con `ExamApi=true` e `SchoolApi=false` l'avvio riesce, ma tutte le API esami rispondono 403 senza alcun segnale diagnostico.

**Fix.** Validare le combinazioni all'avvio (fail fast con messaggio) e documentare la matrice. **Impegno:** S.

**Verifica.** Test di `RuntimePolicy` sulle combinazioni. Esistono test per i singoli flag (`RuntimeFeatureAvailabilityTests`), non per le combinazioni.

### INC-012 · Codice legacy compilato in produzione e fallback senza tenant (Media, CONFERMATO)

**Severità.** Il codice non è raggiungibile via HTTP oggi, ma un futuro uso del costruttore di default reintroduce query senza filtro istituzione.

**Evidenza**

- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/ElectronicRegisterAPI.Infrastructure.csproj:11,35`: gli script SQL MT1 e MT3 sono incorporati nell'assembly di produzione (`EmbeddedResource …migrations\mt1\*.sql`, `…mt3\*.sql`) e il Dockerfile copia `migrations/mt1` e `mt3` (`infra/Dockerfile:6-7`).
- `…Persistence/Migrations/Mt3aOwnershipMigration.cs`, `Mt1SchemaMigration.cs` e `Mt3ProfileBackfill.cs` sono usati solo da test e tool (verificato con grep). Non hanno chiamate dall'API.
- `Backend/ElectronicRegisterAPI/tools/Mt3aMigration` e `tools/Mt3cTestPreparation` sono fuori dalla `.slnx`. Compilano, ma hanno percorsi fissi (`tools/Mt3cTestPreparation/Program.cs:30`: `C:/xampp/mysql/bin/mysqldump.exe`).
- `Backend/ElectronicRegisterAPI/mt3-staging/`, `mt3b-staging/` e `mt3*-evidence/` contengono copie `.cs` non compilate.
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs:89-92`: "Preparatory MT-1 persistence. Not consumed by legacy auth/API/worker" (`InstitutionRepository`, `CampusRepository`, `InstitutionMembershipRepository` registrati in DI).
- **Fallback senza tenant:**
  - `…Repositories/ExamRepository.cs:23`: `InstitutionExamDataScope? scope = null`
  - `ExamRepository.cs:69`: `_context.Exams.FromSqlRaw("SELECT * FROM exam WHERE id = {0} FOR UPDATE", id)` quando `_scope is null`
  - `ExamSubmissionRepository.cs:28,76`, `GradeRepository.cs:18` e `StudentRepository`/`SubjectRepository`/`ClassSubjectRepository`/`AuditRepository`: `_scope?.Query<T>() ?? _context.Set<T>()`
  - Oggi tutte le istanze ricevono uno scope (`InstitutionExamRepositoryFactory.cs:63-69`, `SubmissionExpiryStore.cs:67-70`).

**Scenario.** Uno sviluppatore istanzia `new ExamRepository(db)` per un nuovo job. `GetByIdAsync(id)` legge esami di qualsiasi istituto (IDOR latente).

**Fix.** Rendere lo scope obbligatorio nei costruttori ed eliminare i rami `_scope is null`. Spostare i migratori MT1/MT3 in un assembly di tooling non distribuito. Eliminare o archiviare `mt3*-staging`. Rimuovere le registrazioni "preparatory" non usate. **Impegno:** M. **Rischi:** i test che usano i migratori vanno spostati.

**Verifica.** Un test di architettura (Roslyn o reflection) che fallisca se un repository esami ha un costruttore senza scope. Oggi esiste `tools/CodeOrganization`, che va esteso.

**Test che dipendono dall'holding legacy `MT3A_LEGACY_TEST` (da decidere nel Lotto 6).** Il bootstrap 8.4 pulito non contiene l'holding: lo creano solo gli script di provisioning 5.7 (`tools/local_test_db.py:220`, `tools/formative_test_db.py:141`, `tools/windows_cli_certification.py:127`). Nessun seed è stato aggiunto alla fixture 8.4. Il Lotto 6 decide se l'holding diventa parte rivista della fixture 8.4, se i test vanno riscritti senza holding reale o se l'holding viene ritirato.

| Test | Casi | Stato su 8.4 | CI 8.4 |
|---|---|---|---|
| `Mt5bSecurityStateTests.Sql_FixtureGuardExcludesRealLegacyHolding` (osservazione separata dal test di guard nel Lotto 0B) | 1 | Fallisce: holding assente (conteggio 0 invece di 1) | Esclusa, `EXCLUDED_METHODS` in `tools/integration_partition.py` |
| `Mt5dMembershipMutationTests.OfflineGraphClassifiesValidatedIamLedgerWithoutChangingData` | 1 | Fallisce: id dell'holding assente, `ValidateGraphAsync` lo richiede | Esclusa, `EXCLUDED_METHODS` |
| `Mt3dHttpTests.Release_WorkerDispatchRevalidatesTenantStateUnderLock(state: "holding-active", …)` | 2 | Fallisce: `mt3a_legacy_scope` vuota | Non nei filtri della CI 8.4 |
| `HistoricalExpiryBoundaryTests.HistoricalOwnerRemainsTechnicalOnlyAndDeliveryIsIdempotent` | 1 | Fallisce: `mt3a_legacy_scope` vuota | Non nei filtri |
| `Phase4_ExamLifecycleTests.Audit_IdentityProfileFaultIsAtomicAndCannotChangeDidacticProfile` | 2 | Fallisce: `MT3A explicit owner required` | Non nei filtri |
| `Mt4aCourseStructureTests.ExistingBackfillIsIdempotentWithAttributedMt4Graph`, `…LegacyEfWritersStillReceiveTriggerDerivedOwners` | 2 | Dipende dall'ordine: passa se un test precedente nello stesso container ha creato l'holding (`Mt3aOwnershipTests.cs:39`, `InstitutionFormativePlanLifecycleTests.cs:132`) | Non nei filtri |

### INC-013 · Differenze Windows/Linux (Bassa, CONFERMATO)

**Severità.** Riguarda l'operatività, non la sicurezza.

**Evidenza**

- `…Program.cs:13`: `AddKeyPerFile("/run/secrets", optional: true)` è un percorso POSIX; su Windows si risolve nella radice del disco corrente.
- `tools/PlatformAdmin/Program.cs:25,48`: solo Windows (store `CurrentUser\My` e ACL NTFS), mentre l'API gira in un container Linux (`infra/Dockerfile:19`).
- Il build ha avvisi CA1416 nei test (§2).
- Case sensitivity: nomi di tabelle e colonne sono tutti minuscoli nello schema e nel SQL raw letto, quindi `lower_case_table_names` non ha effetto (vedi §7).

**Fix.** Rendere il percorso dei secret configurabile, documentare la topologia operatore (firma su Windows, esecuzione dove sta il DB) e marcare i test ACL con `[SupportedOSPlatform]`. **Impegno:** S.

### INC-014 · Contratti API: documentazione disallineata e breaking change del ritiro legacy (Bassa, CONFERMATO)

**Severità.** Le differenze trovate non rompono il contratto HTTP.

**Evidenza**

- `docs/refactoring/INSTITUTIONAL_API_SURFACE.json` non coincide più con i sorgenti di `InstitutionExamController`, `InstitutionGradeController` e `InstitutionSubmissionController`. L'unica differenza è l'aggiunta di `HttpContext.RequestAborted`; route, verbi e ruoli sono invariati (verificato riga per riga).
- `INSTITUTIONAL_API_AND_LEGACY_CUTOVER.md:19-21`: 73 azioni globali ritirate (`/api/Exam`, `/api/Grade` e altre restituiscono 404) senza adapter. Il breaking change è **documentato e deliberato**.
- Fuori da quel documento non ho trovato breaking change. La OpenAPI non è stata generata (§2).

**Fix.** Rigenerare la superficie API, aggiungere alla CI il confronto OpenAPI (snapshot) e comunicare ai client mobile ed esterni la data di ritiro. **Impegno:** S.

### INC-015 · Il controllo integrità della cancellazione classe scansiona tutti i tenant (Bassa, CONFERMATO)

**Severità.** Riguarda prestazioni e disponibilità, non la riservatezza: il risultato è solo un conteggio.

**Evidenza**

- `…Repositories/ClassDeletionIntegrity.cs:28`: `SELECT COUNT(*) FROM institution_lesson l LEFT JOIN formative_module m … WHERE m.id IS NULL`, senza filtro `institution_id`. Viene chiamato a runtime da `InstitutionClassRepository.cs:160`.
- `ClassDeletionIntegrity.cs:19`: interroga `information_schema` a ogni cancellazione.

**Scenario.** Una riga incoerente nell'istituto A blocca le cancellazioni di classe di tutti gli istituti. Con molti dati, ogni cancellazione fa un full scan.

**Fix.** Filtrare per tenant e spostare la verifica dei metadati nella readiness. **Impegno:** S.

### INC-016 · Toolchain dei test deprecata (Bassa, CONFERMATO)

**Evidenza.** `dotnet list --deprecated`: `xunit` 2.5.3 è Legacy (sostituto `xunit.v3`). Sono vecchi anche `FluentAssertions` 6.11.0, `Microsoft.NET.Test.Sdk` 17.11.0 e `xunit.runner.visualstudio` 2.5.3. Non ha impatto in produzione.

**Fix.** Aggiornare a xunit.v3 in un lotto dedicato. Non portare FluentAssertions alla 8.x senza valutare la licenza commerciale introdotta in v8. **Impegno:** M.

## 5. Dettaglio vulnerabilità

### VULN-001 · Enumerazione degli account (Media, CONFERMATO)

**Severità.** Chiunque, senza credenziali, può sapere se un'email è registrata: questo prepara phishing e credential stuffing mirati su studenti e personale.

**Evidenza**

- `…Controllers/IdentityRegistrationController.cs:11`: `[HttpPost]` senza `[Authorize]`. Il marker Identity salta il gate (VULN-011).
- `…Repositories/IdentityAccountRepository.cs:25-27`: un duplicato (1062) diventa `BusinessRuleException("Account non disponibile.")`, che il gestore globale trasforma in 409 (`GlobalExceptionHandler.cs:29`). Il 201 invece restituisce lo UserId.
- `…Managers/InstitutionAuthManager.cs:21-22`: `account is not null && passwords.Verify(...)`. Se l'email non esiste BCrypt non viene eseguito, quindi la risposta arriva in pochi ms invece delle centinaia di un `Verify` con cost 11.

**Scenario.** Un attaccante anonimo invia `POST /api/identity/accounts` con `{"email":"nome.cognome@istituto.it","password":"Xxxxxxx!1"}`: 409 significa che l'account esiste, 201 che non esisteva (e intanto ha creato un account). Lo stesso risultato si ottiene misurando i tempi di `POST /api/Auth/identity/login`. Il limite è 60 POST al minuto per IP.

**Deliberato.** La registrazione anonima sì (`INSTITUTIONAL_API_AND_LEGACY_CUTOVER.md:53`). La risposta 409 e la differenza di tempo non sono motivate in nessun documento.

**Fix (D2)**

- Registrazione: rispondere sempre 202 con lo stesso corpo e completare con verifica email, oppure limitare la registrazione agli inviti (il flusso di onboarding esiste già).
- Login: eseguire un `Verify` su un hash fittizio con lo stesso cost quando l'account non esiste.

**Impegno:** S. **Rischi:** il contratto 201/409 è usato dai client.

**Verifica.** Il test `IdentityAccountTests.RegistrationCreatesOnlyIdentityAndCollisionCannotAcquireHistoricalAccount` (`tests/Integration/IdentityAccountTests.cs:154`) oggi fissa il 409 e va aggiornato. Da aggiungere un test con risposta identica per account esistenti e inesistenti, e un test unitario che verifichi l'invocazione di `Verify` anche senza account.

**Stato (2026-11-05): RISOLTO con D2.**
- La registrazione anonima è rimossa: il vecchio test è sostituito da `PublicRegistrationIsGoneAndCannotCreateOrAcquireAnyAccount`, che verifica il 404.
- Il login esegue esattamente un `Verify` BCrypt dello stesso work factor (11) per email presente, assente o solo esterna.
  - `LoginIsUniformForExistingMissingAndExternalOnlyEmails` verifica stato, corpo e tempi. Il controllo negativo, senza hash fittizio, fallisce: 166 ms contro 24 ms.
  - `AbsentCredentialUsesTheSameBcryptCostAsRealHashes` verifica il costo.
- La creazione istituzionale risponde con un 409 unico per un indirizzo non disponibile, allo stesso costo BCrypt (`EmailUsedInAnotherInstitutionRevealsNothing`).
- **Residuo accettato:** con lo spazio email globale si capisce che un indirizzo è "occupato da qualche parte". Resta aperto anche il limite per account sul login (VULN-002).

### VULN-002 · Brute force e credential stuffing sul login (Media, CONFERMATO)

**Severità.** Il login non ha un limite per account. Il limite per IP è condiviso con tutte le POST, quindi una botnet resta di fatto senza limiti.

**Evidenza**

- `…Operations/OperationalPipeline.cs:61`: il `GlobalLimiter` è partizionato su `RemoteIpAddress`, vale solo per POST `/api/*`, con una finestra fissa di `SensitivePermitLimit` (default 60) al minuto.
- `IdentityAccountRepository.cs:104-129`: una quota per utente esiste **solo** per il cambio password.
- `…Security/BcryptPasswordHasher.cs:7`: `HashPassword(password)` usa il work factor di default della libreria (11).
- `…Policies/PasswordRules.cs:9`: minimo 8 caratteri più un carattere speciale; nessun controllo contro password comuni.

**Scenario.** Con 1000 IP diversi un attaccante prova 60 000 password al minuto su un singolo account docente senza mai essere bloccato. Il login ha successo con password come `Password1!`.

**Fix.** Aggiungere una quota per account sul login, simile a `identity_password_attempt`, con backoff, risposta neutra e senza lockout permanente (evita il DoS mirato). Portare il cost BCrypt a 12 con rehash al login. Valutare un controllo contro password compromesse. **Impegno:** M. **Dipendenze:** VULN-003.

**Verifica.** Nuovo test HTTP "N tentativi falliti sullo stesso account da IP diversi danno 429". Oggi c'è solo il test per IP (`Mt6RuntimeTests.cs:169`).

### VULN-003 · Rate limit collassato dietro un proxy non dichiarato (Media, PROBABILE)

**Severità.** Con una configurazione ammessa dalla policy, un solo client esaurisce il budget POST di tutti gli utenti.

**Evidenza**

- `…Operations/RuntimePolicy.cs:30`: `KnownProxies` è facoltativo anche in Staging e Production.
- `…OperationalPipeline.cs:73-74`: `UseForwardedHeaders` viene attivato solo se ci sono proxy dichiarati.
- `infra/runtime.example.json:6`: `"KnownProxies": []` per lo staging, che secondo `infra/staging/compose.yaml:2` sta dietro un nginx dell'host.
- `OperationalPipeline.cs:61`: la chiave del limite è `RemoteIpAddress`.

**Scenario.** Lo staging viene esposto tramite il nginx dell'host verso `127.0.0.1:${API_PORT}`. Tutte le richieste arrivano con l'IP del bridge Docker. Un utente che fa 60 POST in un minuto blocca login e scritture di tutti per 60 secondi. In più, senza `X-Forwarded-Proto` accettato, `UseHttpsRedirection` reindirizza ogni richiesta, con un possibile loop. Va verificato in runtime.

**Fix.** In Staging e Production richiedere `KnownProxies` non vuoto, oppure un flag esplicito `Transport:DirectExposure=true`. Documentare la topologia. **Impegno:** S.

**Verifica.** Il test `Mt6RuntimeTests.StrictStagingHonorsOnlyConfiguredProxyAndRedirectsUnencryptedRequests` (`:184`) copre il caso configurato. Da aggiungere un test di policy che rifiuti l'avvio senza proxy.

### VULN-004 · Login Microsoft sempre esposto e validazione lasca (Bassa, PROBABILE)

**Severità.** L'effetto principale è un 500 e richieste in uscita generate da utenti anonimi. Il furto d'identità è mitigato dal binding esplicito tenant/oid → UserId.

**Evidenza**

- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs:83-85`: l'URL di discovery viene costruito con `AzureAd:TenantId` senza validazione (`CHANGE-ME` nell'esempio).
- `…Security/MicrosoftTokenValidator.cs:24`: `GetConfigurationAsync()` lancia `InvalidOperationException` se la discovery fallisce, e `MicrosoftIdentityLink.cs:17,21` cattura solo `SecurityTokenException` e `ArgumentException`, quindi la risposta è 500.
- `MicrosoftTokenValidator.cs:25-34`: mancano `ValidAlgorithms` e `ValidTypes`, viene usato il legacy `JwtSecurityTokenHandler` e l'issuer è solo v2. Un ID token con la stessa audience verrebbe accettato come access token.
- L'endpoint (`InstitutionIdentityController.cs:21`) non ha feature flag.

**Scenario.** Su un'installazione senza Azure AD, chiunque invia `POST /api/Auth/identity/microsoft-login {"accessToken":"x"}` e provoca una richiesta verso `login.microsoftonline.com/CHANGE-ME/...` e un 500 loggato. Il comportamento della cache di `ConfigurationManager` dopo un fallimento è DA VERIFICARE.

**Fix.** Aggiungere un flag `Identity:Microsoft:Enabled` con validazione all'avvio (TenantId GUID e Audience), catturare le eccezioni di discovery rispondendo 401 o 503, usare `JsonWebTokenHandler` con `ValidAlgorithms=[RS256]` e controllare il claim `scp`/`roles` o `ver`. **Impegno:** S. **Dipendenze:** D5.

**Verifica.** Esiste `AuditMicrosoftTests.MicrosoftValidator_RequiresConfiguredTenantIssuer` (`tests/Integration/AuditMicrosoftTests.cs:24`). Da aggiungere test per "AzureAd assente → 404 o 401, mai 500" e per "id_token rifiutato".

### VULN-005 · Chiave JWT unica, senza `kid` né rotazione (Bassa, CONFERMATO)

**Severità.** La separazione tra i due schemi è corretta. Il rischio è operativo: una fuga della chiave compromette entrambi gli schemi e la rotazione richiede un riavvio che invalida tutte le sessioni.

**Evidenza.** `…Security/InstitutionAuthentication.cs:27,63`: entrambi gli schemi usano `new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))`. La separazione avviene tramite audience (`SelectionAudience` e `AccessAudience`), `typ` e claim `purpose`. La revoca tramite generation su DB (`:113` e `InstitutionAuthManager.cs:51-55`) è corretta.

**Fix.** Usare un insieme di chiavi (`IssuerSigningKeys`) con `kid` e una chiave di firma corrente, così la rotazione non interrompe le sessioni. Valutare chiavi distinte per Selection e Access. **Impegno:** M.

**Verifica.** Test "token firmato con la chiave precedente ancora accettato durante la finestra; poi rifiutato".

### VULN-006 · Upload: concorrenza e limiti incoerenti (Bassa, PROBABILE)

**Severità.** Richiede un utente autenticato InstitutionAdmin o Staff. L'effetto è un consumo di memoria limitato.

**Evidenza**

- `…Controllers/InstitutionCalendarController.cs:88`: l'anteprima calendario **non** ha `EnableRateLimiting("InstitutionImportUpload")`, che invece è presente sull'import anagrafico (`InstitutionImportController.cs:24`).
- `InstitutionImportController.cs:24`: il body ammesso è di 64 MB, ma il parser rifiuta oltre `MaxFileBytes` (10 MB di default, `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs:35`). Il form multipart viene comunque bufferizzato da ASP.NET.
- `…Services/InstitutionImportFileReader.cs` (lettura in `MemoryStream`, poi `ToArray()`) e `InstitutionImportFileReader.Xlsx.cs:192-205` (un `MemoryStream` per ogni entry, fino a `MaxUncompressedBytes` = 40 MB) moltiplicano la memoria per richiesta.

**Scenario.** Un account Staff compromesso invia in parallelo molte anteprime calendario da 12 MB, ciascuna con XLSX vicini al limite di espansione. Ogni richiesta occupa circa 60–90 MB, mentre il limite è di 60 POST al minuto per IP.

**Fix.** Applicare il limiter di concorrenza anche al calendario, allineare `RequestSizeLimit` a `MaxFileBytes` più un margine per il form, e fare lo streaming dell'hash invece di `ToArray`. **Impegno:** S.

**Verifica.** Test di concorrenza sull'anteprima calendario (oggi esistono solo per l'import, `Import.*Concurr` = 2 test).

### VULN-007 · Dati operativi e personali nelle evidenze versionate (Bassa, CONFERMATO)

**Severità.** Non ho trovato segreti né dati reali di studenti. L'esposizione riguarda dati del personale di sviluppo e informazioni utili per la ricognizione.

**Evidenza** (scansione di 1314 file tracciati in `docs/`, `tests/Integration/TestResults` e `tests/Integration/*.md`)

- 324 file contengono percorsi assoluti `C:\Users\<nome>`, con due nomi utente Windows.
- Le TRX (209 file) contengono `computerName` e `runUser`, cioè nomi macchina e utenti.
- 10 indirizzi distinti sul dominio reale di un istituto. Sono generati da fixture sintetiche (`tests/Unit/InstitutionEmailGeneratorTests.cs:13-17`), ma usano un dominio reale.
- **Nessun segreto**: le 13 occorrenze `Password=` sono variabili o segnaposto (`REPLACE_SECRET`). Nessuna chiave privata, nessun JWT, nessuna chiave AWS e nessun codice fiscale.
- Esempio: `docs/quality/final-assessment-evidence/Unit/<utente>_<macchina>_2026-10-30_13_36_09_net10.0.trx`.

**Fix.** Ripulire le evidenze (percorsi relativi e TRX anonimizzate), aggiungere un hook o un controllo CI che blocchi `C:\Users\` e `computerName=` nei file versionati, e usare `example.test` nelle fixture. Valutare se riscrivere la storia (decisione D6). **Impegno:** S per la pulizia; M se si riscrive la storia.

### VULN-008 · Immagini base non fissate per digest (Bassa, CONFERMATO)

**Evidenza.** `infra/Dockerfile:2` usa `mcr.microsoft.com/dotnet/sdk:10.0` e `:19` `mcr.microsoft.com/dotnet/aspnet:10.0`, per tag. Anche `infra/staging/Dockerfile.ops:1,3` (`node:22-bookworm-slim`, `sdk:10.0-noble`) e `apt-get install` sono senza versione. Positivo: l'immagine MySQL è fissata per digest e il container gira con `USER $APP_UID` (`infra/Dockerfile:26`), quindi non come root.

**Fix.** Fissare le immagini per digest e aggiornarle con un bot. **Impegno:** S.

### VULN-009 · Privilegi elevati del migrator di staging (Bassa, CONFERMATO)

**Evidenza.** `infra/staging/db-bootstrap.sh:18-22`: `GRANT … WITH GRANT OPTION`, `GRANT CREATE USER,SET_ANY_DEFINER ON *.*`, `GRANT SHOW_ROUTINE ON *.*`. Positivo: l'account dell'API ha solo DML più `EXECUTE` su `er_schema_ready` (`tools/prepare-release-sql.cjs:29,35`).

**Scenario.** Chi ottiene il secret del migrator può creare utenti e definer arbitrari sul server di staging.

**Fix.** Usare il migrator solo durante la finestra di migrazione (`ACCOUNT LOCK` fuori finestra), oppure separare l'account che crea gli utenti da quello che applica il DDL. **Impegno:** S.

### VULN-010 · Escalation tra admin di istituto senza doppio controllo (Bassa, CONFERMATO, deliberato)

**Severità.** I controlli presenti (niente autoamministrazione, niente rimozione dell'ultimo admin, audit append-only) limitano il danno. Resta il fatto che un solo admin compromesso controlla l'istituto.

**Evidenza.** `…Repositories/InstitutionMembershipMutationRepository.cs:33` (`Autoamministrazione non consentita`), `:58` (`RequireLastAdmin`). La doppia approvazione esiste solo per il recovery di piattaforma (`InstitutionGovernanceRepository.cs:67`, `ActiveOperatorCount < 2`).

**Scenario.** Un admin compromesso aggiunge `InstitutionAdmin` a un complice e sospende gli altri admin tranne uno.

**Fix (D4).** Richiedere una doppia approvazione per `AddRole InstitutionAdmin` e per la sospensione di un admin, oppure inviare una notifica agli altri admin. **Impegno:** M.

**Rivalutazione (2026-11-05): ACCETTATO dal modello definitivo.**
- Il proprietario ha deciso che gli InstitutionAdmin creano account di ogni ruolo, compresi altri admin, senza limiti di numero, e che resta solo il blocco sull'ultimo admin. La doppia approvazione quindi non si introduce.
- Il rischio "un admin compromesso controlla l'istituto" resta, limitato a quell'istituto:
  - le nuove creazioni lasciano audit (`institution_onboarding_event`, azione `AccountCreated`);
  - il proprietario può resettare qualsiasi account con lo script;
  - l'autoamministrazione resta vietata.
- Una notifica agli altri admin sarebbe una mitigazione utile, ma richiede l'invio email, fuori da questa fase.

### VULN-011 · Il gate MVC lascia passare il marker Identity e non c'è FallbackPolicy (Bassa, CONFERMATO)

**Severità.** Oggi i soli tre endpoint anonimi sono intenzionali. In futuro, un'action con marker Identity e senza `[Authorize]` sarebbe anonima senza che nulla lo segnali.

**Evidenza**

- `…Security/InstitutionEndpointGate.cs:44-45`: `if (identity.Length == 1) return Task.CompletedTask;`, cioè nessun controllo per i controller con `InstitutionIdentityEndpoint`.
- `Program.cs:39`: `AddAuthorization()` senza `FallbackPolicy`.
- Gli endpoint anonimi sono 3: `POST /api/identity/accounts`, `POST /api/Auth/identity/login` e `POST /api/Auth/identity/microsoft-login` (§6).
- I "3 `[AllowAnonymous]`" indicati nella richiesta corrispondono in realtà a 2 `.AllowAnonymous()` sulle health (`OperationalPipeline.cs:125,179`) e a 1 controllo `IAllowAnonymous` nel gate (`InstitutionEndpointGate.cs:51`). Nei controller non c'è nessun `[AllowAnonymous]`: gli endpoint anonimi lo sono perché manca `[Authorize]`.

**Osservazione collegata (DA VERIFICARE).** Gli eventi di audit sono append-only per trigger e contengono snapshot (`before_json`/`after_json`). Le righe di import, gli eventi e i riferimenti esterni sono immutabili, e non ho trovato un percorso di cancellazione o anonimizzazione dei dati personali di studenti e personale. L'anteprima import scade invece dopo `TtlMinutes` e viene ripulita (`InstitutionImportRetention.cs`). Serve una decisione sulla retention (D6).

**Fix.** Rendere esplicito l'anonimato con `[AllowAnonymous]` sulle 3 action e una `FallbackPolicy` che richieda l'autenticazione. Nel gate, per il marker Identity, rifiutare le action che non hanno né `[Authorize]` né `[AllowAnonymous]`. **Impegno:** S.

**Verifica.** Test di discovery che enumeri gli endpoint e confronti l'elenco degli anonimi con un'allowlist. Esiste un audit offline degli `EndpointDataSource` (`Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/DependencyInjection/ServiceCollectionExtensions.cs:7`), che va esteso.

## 6. Tabella di autorizzazione degli endpoint

Tabella generata dai 28 controller: **195 action**, più `GET /health/live` e `GET /health/ready` (anonime, minimal API in `OperationalPipeline.cs:125,126`).

Come leggerla:

- **InstitutionAccess + ruoli**: JWT `InstitutionAccess` (HS256, issuer e audience dedicati, `ClockSkew=0`). In più: membership, ruoli e security generation rivalidati sul DB a ogni richiesta (`InstitutionAuthorizationHandler.cs` → `InstitutionAuthManager.AuthorizeAsync`), controllo del gate MVC sul marker e sul flag, e permesso fine (`InstitutionPermission`) dove indicato.
- **Ruoli**: claim del token, confrontati con il DB tramite `MembershipAuthorizationRules.Matches`. Lo scope dei dati (proprietario, docente assegnato, studente) è applicato nel repository (§7).
- **Flag**: le chiavi `Multitenancy:<Flag>:Enabled`. In Production qualsiasi flag attivo richiede anche `Deployment:RolloutApproved=true` (`RuntimePolicy.cs:52`).
- **Esito**: non ho trovato endpoint istituzionali senza autorizzazione. **Gli unici endpoint anonimi sono le 3 righe indicate come ANONIMO** (deliberati, vedi VULN-001, VULN-002, VULN-004 e VULN-011). Le autorizzazioni più deboli da rivedere sono: le letture dell'elenco docenti per il ruolo Student (nomi docenti dell'istituto, deliberato) e la sola policy InstitutionAdmin, senza doppio controllo, per le mutazioni di membership (VULN-010).

| # | Metodo | Route | Autenticazione | Ruoli | Flag richiesti | Note | Evidenza |
|---|---|---|---|---|---|---|---|
| 1 | GET | `/api/identity/account` | Selection JWT | — | nessuno |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/IdentityAccountController.cs:16` |
| 2 | PUT | `/api/identity/account` | Selection JWT | — | nessuno |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/IdentityAccountController.cs:18` |
| 3 | PUT | `/api/identity/account/password` | Selection JWT | — | nessuno |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/IdentityAccountController.cs:25` |
| 4 | POST | `/api/identity/accounts` | **ANONIMO** | — | nessuno |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/IdentityRegistrationController.cs:11` |
| 5 | GET | `/api/institution/biennia` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionBienniumController.cs:13` |
| 6 | GET | `/api/institution/biennia/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionBienniumController.cs:20` |
| 7 | POST | `/api/institution/biennia` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionBienniumController.cs:30` |
| 8 | PUT | `/api/institution/biennia/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionBienniumController.cs:37` |
| 9 | DELETE | `/api/institution/biennia/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionBienniumController.cs:47` |
| 10 | GET | `/api/institution/biennium-study-areas` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionBienniumStudyAreaController.cs:13` |
| 11 | GET | `/api/institution/biennium-study-areas/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionBienniumStudyAreaController.cs:20` |
| 12 | POST | `/api/institution/biennium-study-areas` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionBienniumStudyAreaController.cs:30` |
| 13 | DELETE | `/api/institution/biennium-study-areas/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionBienniumStudyAreaController.cs:37` |
| 14 | GET | `/api/institution/biennium-study-paths` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionBienniumStudyPathController.cs:13` |
| 15 | GET | `/api/institution/biennium-study-paths/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionBienniumStudyPathController.cs:20` |
| 16 | POST | `/api/institution/biennium-study-paths` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionBienniumStudyPathController.cs:30` |
| 17 | DELETE | `/api/institution/biennium-study-paths/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionBienniumStudyPathController.cs:37` |
| 18 | GET | `/api/institution/calendar/settings` | InstitutionAccess + ruoli | InstitutionAdmin,Staff | SchoolApi +InstitutionCalendar (startup) |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionCalendarController.cs:15` |
| 19 | PUT | `/api/institution/calendar/settings` | InstitutionAccess + ruoli | InstitutionAdmin,Staff | SchoolApi +InstitutionCalendar (startup) |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionCalendarController.cs:18` |
| 20 | GET | `/api/institution/calendar` | InstitutionAccess + ruoli | InstitutionAdmin,Staff | SchoolApi +InstitutionCalendar (startup) |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionCalendarController.cs:21` |
| 21 | GET | `/api/institution/calendar/classes/{classId:guid}` | InstitutionAccess + ruoli | InstitutionAdmin,Staff | SchoolApi +InstitutionCalendar (startup) |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionCalendarController.cs:24` |
| 22 | GET | `/api/institution/calendar/teachers/me` | InstitutionAccess + ruoli | Teacher | SchoolApi +InstitutionCalendar (startup) |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionCalendarController.cs:27` |
| 23 | GET | `/api/institution/calendar/students/me` | InstitutionAccess + ruoli | Student | SchoolApi +InstitutionCalendar (startup) |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionCalendarController.cs:30` |
| 24 | GET | `/api/institution/calendar/months/{year:int}/{month:int}/classes/{classId:guid}` | InstitutionAccess + ruoli | InstitutionAdmin,Staff | SchoolApi +InstitutionCalendar (startup) |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionCalendarController.cs:33` |
| 25 | POST | `/api/institution/calendar/months/{year:int}/{month:int}/classes/{classId:guid}/publish` | InstitutionAccess + ruoli | InstitutionAdmin,Staff | SchoolApi +InstitutionCalendar (startup) |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionCalendarController.cs:41` |
| 26 | GET | `/api/institution/calendar/lessons/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin,Staff,Teacher,Student | SchoolApi +InstitutionCalendar (startup) |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionCalendarController.cs:50` |
| 27 | GET | `/api/institution/calendar/lessons/{id:guid}/module` | InstitutionAccess + ruoli | InstitutionAdmin,Staff,Teacher,Student | SchoolApi +InstitutionCalendar (startup) |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionCalendarController.cs:53` |
| 28 | POST | `/api/institution/calendar/lessons` | InstitutionAccess + ruoli | InstitutionAdmin,Staff | SchoolApi +InstitutionCalendar (startup) |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionCalendarController.cs:56` |
| 29 | PUT | `/api/institution/calendar/lessons/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin,Staff | SchoolApi +InstitutionCalendar (startup) |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionCalendarController.cs:59` |
| 30 | POST | `/api/institution/calendar/lessons/{id:guid}/cancel` | InstitutionAccess + ruoli | InstitutionAdmin,Staff | SchoolApi +InstitutionCalendar (startup) |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionCalendarController.cs:62` |
| 31 | POST | `/api/institution/calendar/lessons/{id:guid}/reschedule` | InstitutionAccess + ruoli | InstitutionAdmin,Staff | SchoolApi +InstitutionCalendar (startup) |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionCalendarController.cs:65` |
| 32 | POST | `/api/institution/calendar/lessons/{id:guid}/substitute` | InstitutionAccess + ruoli | InstitutionAdmin,Staff | SchoolApi +InstitutionCalendar (startup) |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionCalendarController.cs:68` |
| 33 | POST | `/api/institution/calendar/lessons/{id:guid}/complete` | InstitutionAccess + ruoli | InstitutionAdmin,Staff,Teacher | SchoolApi +InstitutionCalendar (startup) |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionCalendarController.cs:71` |
| 34 | POST | `/api/institution/calendar/lessons/{id:guid}/correct-delivered-time` | InstitutionAccess + ruoli | InstitutionAdmin,Staff | SchoolApi +InstitutionCalendar (startup) |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionCalendarController.cs:74` |
| 35 | GET | `/api/institution/calendar/lessons/{id:guid}/history` | InstitutionAccess + ruoli | InstitutionAdmin,Staff | SchoolApi +InstitutionCalendar (startup) |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionCalendarController.cs:77` |
| 36 | GET | `/api/institution/calendar/hours` | InstitutionAccess + ruoli | InstitutionAdmin,Staff | SchoolApi +InstitutionCalendar (startup) |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionCalendarController.cs:85` |
| 37 | POST | `/api/institution/calendar/imports/preview` | InstitutionAccess + ruoli | InstitutionAdmin,Staff | SchoolApi +InstitutionCalendar (startup) |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionCalendarController.cs:88` |
| 38 | GET | `/api/institution/calendar/imports/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin,Staff | SchoolApi +InstitutionCalendar (startup) |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionCalendarController.cs:116` |
| 39 | POST | `/api/institution/calendar/imports/{id:guid}/confirm` | InstitutionAccess + ruoli | InstitutionAdmin,Staff | SchoolApi +InstitutionCalendar (startup) |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionCalendarController.cs:119` |
| 40 | GET | `/api/institution/campuses` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi+CourseApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionCampusController.cs:13` |
| 41 | GET | `/api/institution/campuses/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi+CourseApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionCampusController.cs:29` |
| 42 | POST | `/api/institution/campuses` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi+CourseApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionCampusController.cs:39` |
| 43 | PUT | `/api/institution/campuses/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi+CourseApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionCampusController.cs:46` |
| 44 | DELETE | `/api/institution/campuses/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi+CourseApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionCampusController.cs:56` |
| 45 | GET | `/api/institution/classes` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionClassController.cs:13` |
| 46 | GET | `/api/institution/classes/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionClassController.cs:28` |
| 47 | POST | `/api/institution/classes` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionClassController.cs:38` |
| 48 | PUT | `/api/institution/classes/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionClassController.cs:45` |
| 49 | DELETE | `/api/institution/classes/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionClassController.cs:57` |
| 50 | GET | `/api/institution/class-subjects` | InstitutionAccess + ruoli | InstitutionAdmin,Teacher | SchoolApi+ExamApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionClassSubjectController.cs:13` |
| 51 | GET | `/api/institution/class-subjects/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi+ExamApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionClassSubjectController.cs:31` |
| 52 | POST | `/api/institution/class-subjects` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi+ExamApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionClassSubjectController.cs:41` |
| 53 | PUT | `/api/institution/class-subjects/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi+ExamApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionClassSubjectController.cs:48` |
| 54 | DELETE | `/api/institution/class-subjects/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi+ExamApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionClassSubjectController.cs:58` |
| 55 | GET | `/api/institution/course-editions` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi+CourseApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionCourseEditionController.cs:13` |
| 56 | GET | `/api/institution/course-editions/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi+CourseApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionCourseEditionController.cs:20` |
| 57 | GET | `/api/institution/course-editions/by-code/{code}` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi+CourseApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionCourseEditionController.cs:22` |
| 58 | POST | `/api/institution/course-editions` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi+CourseApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionCourseEditionController.cs:24` |
| 59 | PUT | `/api/institution/course-editions/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi+CourseApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionCourseEditionController.cs:31` |
| 60 | GET | `/api/institution/course-editions/{id:guid}/campuses` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi+CourseApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionCourseEditionController.cs:38` |
| 61 | POST | `/api/institution/course-editions/{id:guid}/campuses` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi+CourseApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionCourseEditionController.cs:45` |
| 62 | GET | `/api/institution/course-editions/{id:guid}/classes` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi+CourseApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionCourseEditionController.cs:52` |
| 63 | POST | `/api/institution/course-editions/{editionId:guid}/classes/{classId:guid}` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi+CourseApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionCourseEditionController.cs:59` |
| 64 | GET | `/api/institution/course-editions/{id:guid}/enrollments` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi+CourseApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionCourseEditionController.cs:66` |
| 65 | GET | `/api/institution/course-editions/{id:guid}/audit` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi+CourseApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionCourseEditionController.cs:73` |
| 66 | GET | `/api/institution/email-policies` | InstitutionAccess + ruoli | InstitutionAdmin,Staff | SchoolApi +InstitutionEmail |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionEmailController.cs:14` |
| 67 | GET | `/api/institution/email-policies/{category}` | InstitutionAccess + ruoli | InstitutionAdmin,Staff | SchoolApi +InstitutionEmail |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionEmailController.cs:16` |
| 68 | POST | `/api/institution/email-policies/{category}` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi +InstitutionEmail |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionEmailController.cs:19` |
| 69 | PUT | `/api/institution/email-policies/{category}` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi +InstitutionEmail |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionEmailController.cs:27` |
| 70 | GET | `/api/institution/email-policies/{category}/history` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi +InstitutionEmail |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionEmailController.cs:35` |
| 71 | POST | `/api/institution/email-policies/preview` | InstitutionAccess + ruoli | InstitutionAdmin,Staff | SchoolApi +InstitutionEmail |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionEmailController.cs:42` |
| 72 | GET | `/api/institution/email-assignments` | InstitutionAccess + ruoli | InstitutionAdmin,Staff,Teacher,Student | SchoolApi +InstitutionEmail |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionEmailController.cs:44` |
| 73 | GET | `/api/institution/email-assignments/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin,Staff,Teacher,Student | SchoolApi +InstitutionEmail |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionEmailController.cs:46` |
| 74 | POST | `/api/institution/email-assignments` | InstitutionAccess + ruoli | InstitutionAdmin,Staff | SchoolApi +InstitutionEmail |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionEmailController.cs:48` |
| 75 | POST | `/api/institution/email-assignments/create-person` | InstitutionAccess + ruoli | InstitutionAdmin,Staff | SchoolApi +InstitutionEmail |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionEmailController.cs:50` |
| 76 | POST | `/api/institution/email-assignments/{id:guid}/override` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi +InstitutionEmail |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionEmailController.cs:52` |
| 77 | POST | `/api/institution/email-assignments/{id:guid}/retire` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi +InstitutionEmail |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionEmailController.cs:54` |
| 78 | GET | `/api/institution/email-assignments/{id:guid}/history` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi +InstitutionEmail |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionEmailController.cs:56` |
| 79 | GET | `/api/institution/enrollments/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi+CourseApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionEnrollmentController.cs:13` |
| 80 | GET | `/api/institution/enrollments` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi+CourseApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionEnrollmentController.cs:15` |
| 81 | POST | `/api/institution/enrollments` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi+CourseApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionEnrollmentController.cs:22` |
| 82 | GET | `/api/institution/enrollments/me` | InstitutionAccess + ruoli | Student | SchoolApi+CourseApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionEnrollmentController.cs:29` |
| 83 | POST | `/api/institution/enrollments/{id:guid}/transition` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi+CourseApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionEnrollmentController.cs:31` |
| 84 | PUT | `/api/institution/enrollments/{id:guid}/completion-date` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi+CourseApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionEnrollmentController.cs:33` |
| 85 | GET | `/api/institution/enrollments/{id:guid}/history` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi+CourseApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionEnrollmentController.cs:35` |
| 86 | GET | `/api/institution/exams` | InstitutionAccess + ruoli | InstitutionAdmin,Staff,Teacher | SchoolApi+ExamApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionExamController.cs:14` |
| 87 | GET | `/api/institution/exams/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin,Staff,Teacher | SchoolApi+ExamApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionExamController.cs:16` |
| 88 | GET | `/api/institution/exams/assigned` | InstitutionAccess + ruoli | Student | SchoolApi+ExamApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionExamController.cs:18` |
| 89 | GET | `/api/institution/exams/{id:guid}/eligible-students` | InstitutionAccess + ruoli | InstitutionAdmin,Staff,Teacher | SchoolApi+ExamApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionExamController.cs:20` |
| 90 | POST | `/api/institution/exams` | InstitutionAccess + ruoli | InstitutionAdmin,Staff,Teacher | SchoolApi+ExamApi | perm ExamCreate | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionExamController.cs:22` |
| 91 | PUT | `/api/institution/exams/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin,Staff,Teacher | SchoolApi+ExamApi | perm ExamUpdate | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionExamController.cs:30` |
| 92 | PUT | `/api/institution/exams/{id:guid}/students` | InstitutionAccess + ruoli | InstitutionAdmin,Staff,Teacher | SchoolApi+ExamApi | perm ExamAssign | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionExamController.cs:38` |
| 93 | POST | `/api/institution/exams/{id:guid}/publish` | InstitutionAccess + ruoli | InstitutionAdmin,Staff,Teacher | SchoolApi+ExamApi | perm ExamPublish | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionExamController.cs:46` |
| 94 | POST | `/api/institution/exams/{id:guid}/close` | InstitutionAccess + ruoli | InstitutionAdmin,Staff,Teacher | SchoolApi+ExamApi | perm ExamClose | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionExamController.cs:54` |
| 95 | POST | `/api/institution/exams/{id:guid}/cancel` | InstitutionAccess + ruoli | InstitutionAdmin,Teacher | SchoolApi+ExamApi | perm ExamCancel | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionExamController.cs:62` |
| 96 | DELETE | `/api/institution/exams/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin,Teacher | SchoolApi+ExamApi | perm ExamDelete | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionExamController.cs:70` |
| 97 | POST | `/api/institution/exams/{id:guid}/start` | InstitutionAccess + ruoli | Student | SchoolApi+ExamApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionExamController.cs:78` |
| 98 | GET | `/api/institution/exams/{id:guid}/submissions` | InstitutionAccess + ruoli | InstitutionAdmin,Staff,Teacher | SchoolApi+ExamApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionExamController.cs:80` |
| 99 | GET | `/api/institution/formative-plans` | InstitutionAccess + ruoli | InstitutionAdmin,Staff | SchoolApi +FormativePlans (startup)+CourseApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionFormativePlanController.cs:13` |
| 100 | GET | `/api/institution/formative-plans/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin,Staff | SchoolApi +FormativePlans (startup)+CourseApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionFormativePlanController.cs:15` |
| 101 | POST | `/api/institution/formative-plans` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi +FormativePlans (startup)+CourseApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionFormativePlanController.cs:17` |
| 102 | PUT | `/api/institution/formative-plans/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi +FormativePlans (startup)+CourseApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionFormativePlanController.cs:19` |
| 103 | GET | `/api/institution/formative-plans/{id:guid}/versions` | InstitutionAccess + ruoli | InstitutionAdmin,Staff | SchoolApi +FormativePlans (startup)+CourseApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionFormativePlanController.cs:21` |
| 104 | POST | `/api/institution/formative-plans/{id:guid}/versions` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi +FormativePlans (startup)+CourseApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionFormativePlanController.cs:28` |
| 105 | GET | `/api/institution/formative-plan-versions/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin,Staff | SchoolApi +FormativePlans (startup)+CourseApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionFormativePlanController.cs:30` |
| 106 | PUT | `/api/institution/formative-plan-versions/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi +FormativePlans (startup)+CourseApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionFormativePlanController.cs:32` |
| 107 | POST | `/api/institution/formative-plan-versions/{id:guid}/clone` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi +FormativePlans (startup)+CourseApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionFormativePlanController.cs:34` |
| 108 | POST | `/api/institution/formative-plan-versions/{id:guid}/publish` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi +FormativePlans (startup)+CourseApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionFormativePlanController.cs:36` |
| 109 | POST | `/api/institution/formative-plan-versions/{id:guid}/validate` | InstitutionAccess + ruoli | InstitutionAdmin,Staff | SchoolApi +FormativePlans (startup)+CourseApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionFormativePlanController.cs:38` |
| 110 | GET | `/api/institution/formative-plan-versions/{id:guid}/hour-summary` | InstitutionAccess + ruoli | InstitutionAdmin,Staff | SchoolApi +FormativePlans (startup)+CourseApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionFormativePlanController.cs:40` |
| 111 | POST | `/api/institution/course-editions/{id:guid}/formative-plan` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi +FormativePlans (startup)+CourseApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionFormativePlanController.cs:42` |
| 112 | GET | `/api/institution/course-editions/{id:guid}/formative-plan` | InstitutionAccess + ruoli | InstitutionAdmin,Staff,Teacher,Student | SchoolApi +FormativePlans (startup)+CourseApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionFormativePlanController.cs:44` |
| 113 | GET | `/api/institution/course-editions/{id:guid}/formative-plan/modules/{moduleId:guid}` | InstitutionAccess + ruoli | InstitutionAdmin,Staff,Teacher,Student | SchoolApi +FormativePlans (startup)+CourseApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionFormativePlanController.cs:46` |
| 114 | GET | `/api/institution/formative-plans/{id:guid}/history` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi +FormativePlans (startup)+CourseApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionFormativePlanController.cs:53` |
| 115 | GET | `/api/institution/grades` | InstitutionAccess + ruoli | InstitutionAdmin,Staff,Teacher,Student | SchoolApi+ExamApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionGradeController.cs:14` |
| 116 | GET | `/api/institution/grades/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin,Staff,Teacher,Student | SchoolApi+ExamApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionGradeController.cs:16` |
| 117 | GET | `/api/institution/grades/count` | InstitutionAccess + ruoli | InstitutionAdmin,Staff,Teacher,Student | SchoolApi+ExamApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionGradeController.cs:18` |
| 118 | GET | `/api/institution/grades/paged` | InstitutionAccess + ruoli | InstitutionAdmin,Staff,Teacher,Student | SchoolApi+ExamApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionGradeController.cs:20` |
| 119 | GET | `/api/institution/grades/filters` | InstitutionAccess + ruoli | InstitutionAdmin,Staff,Teacher,Student | SchoolApi+ExamApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionGradeController.cs:28` |
| 120 | GET | `/api/institution/grades/statistics` | InstitutionAccess + ruoli | InstitutionAdmin,Staff,Teacher,Student | SchoolApi+ExamApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionGradeController.cs:30` |
| 121 | POST | `/api/institution/grades` | InstitutionAccess + ruoli | InstitutionAdmin,Staff,Teacher | SchoolApi+ExamApi | perm GradeCreate | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionGradeController.cs:32` |
| 122 | PUT | `/api/institution/grades/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin,Staff,Teacher | SchoolApi+ExamApi | perm GradeUpdate | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionGradeController.cs:35` |
| 123 | DELETE | `/api/institution/grades/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi+ExamApi | perm GradeDelete | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionGradeController.cs:38` |
| 124 | POST | `/api/Auth/identity/login` | **ANONIMO** | — | nessuno |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionIdentityController.cs:14` |
| 125 | POST | `/api/Auth/identity/microsoft-login` | **ANONIMO** | — | nessuno |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionIdentityController.cs:21` |
| 126 | GET | `/api/Auth/institutions` | Selection JWT | — | nessuno |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionIdentityController.cs:28` |
| 127 | POST | `/api/Auth/select-institution` | Selection JWT | — | nessuno |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionIdentityController.cs:30` |
| 128 | GET | `/api/Auth/context` | InstitutionAccess JWT | — | nessuno |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionIdentityController.cs:32` |
| 129 | GET | `/api/institution/imports/templates/students.csv` | InstitutionAccess + ruoli | InstitutionAdmin,Staff | SchoolApi +InstitutionImport |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionImportController.cs:20` |
| 130 | GET | `/api/institution/imports/templates/teachers.csv` | InstitutionAccess + ruoli | InstitutionAdmin,Staff | SchoolApi +InstitutionImport |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionImportController.cs:22` |
| 131 | POST | `/api/institution/imports/preview` | InstitutionAccess + ruoli | InstitutionAdmin,Staff | SchoolApi +InstitutionImport | concorrenza import | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionImportController.cs:24` |
| 132 | GET | `/api/institution/imports` | InstitutionAccess + ruoli | InstitutionAdmin,Staff | SchoolApi +InstitutionImport |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionImportController.cs:51` |
| 133 | GET | `/api/institution/imports/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin,Staff | SchoolApi +InstitutionImport |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionImportController.cs:53` |
| 134 | GET | `/api/institution/imports/{id:guid}/rows` | InstitutionAccess + ruoli | InstitutionAdmin,Staff | SchoolApi +InstitutionImport |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionImportController.cs:55` |
| 135 | PUT | `/api/institution/imports/{id:guid}/mapping` | InstitutionAccess + ruoli | InstitutionAdmin,Staff | SchoolApi +InstitutionImport |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionImportController.cs:62` |
| 136 | POST | `/api/institution/imports/{id:guid}/confirm` | InstitutionAccess + ruoli | InstitutionAdmin,Staff | SchoolApi +InstitutionImport |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionImportController.cs:64` |
| 137 | GET | `/api/institution/imports/{id:guid}/result` | InstitutionAccess + ruoli | InstitutionAdmin,Staff | SchoolApi +InstitutionImport |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionImportController.cs:66` |
| 138 | POST | `/api/institution/admin/invitations` | InstitutionAccess + InstitutionAdmin (policy) | InstitutionAdmin | MembershipAdminApi+MembershipWriteApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionInvitationController.cs:17` |
| 139 | POST | `/api/institution/admin/invitations/{id:guid}/revoke` | InstitutionAccess + InstitutionAdmin (policy) | InstitutionAdmin | MembershipAdminApi+MembershipWriteApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionInvitationController.cs:19` |
| 140 | POST | `/api/institution/admin/invitations/{id:guid}/reissue` | InstitutionAccess + InstitutionAdmin (policy) | InstitutionAdmin | MembershipAdminApi+MembershipWriteApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionInvitationController.cs:21` |
| 141 | GET | `/api/institution/admin/members/{membershipId:guid}/profile-acquisitions` | InstitutionAccess + InstitutionAdmin (policy) | InstitutionAdmin | MembershipAdminApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionMembershipAdminController.cs:12` |
| 142 | POST | `/api/institution/admin/members/{membershipId:guid}/profile-acquisitions` | InstitutionAccess + InstitutionAdmin (policy) | InstitutionAdmin | MembershipAdminApi+MembershipWriteApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionMembershipAdminController.cs:20` |
| 143 | POST | `/api/institution/admin/members/{membershipId:guid}/mutations` | InstitutionAccess + InstitutionAdmin (policy) | InstitutionAdmin | MembershipAdminApi+MembershipWriteApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionMembershipAdminController.cs:22` |
| 144 | GET | `/api/institution/admin/members` | InstitutionAccess + InstitutionAdmin (policy) | InstitutionAdmin | MembershipAdminApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionMembershipAdminController.cs:24` |
| 145 | GET | `/api/institution/admin/members/{membershipId:guid}` | InstitutionAccess + InstitutionAdmin (policy) | InstitutionAdmin | MembershipAdminApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionMembershipAdminController.cs:36` |
| 146 | GET | `/api/institution/admin/audit` | InstitutionAccess + InstitutionAdmin (policy) | InstitutionAdmin | MembershipAdminApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionMembershipAuditController.cs:13` |
| 147 | GET | `/api/institution/admin/audit/{eventId:guid}` | InstitutionAccess + InstitutionAdmin (policy) | InstitutionAdmin | MembershipAdminApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionMembershipAuditController.cs:21` |
| 148 | GET | `/api/institution/students` | InstitutionAccess + ruoli | InstitutionAdmin,Staff,Teacher | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionStudentController.cs:14` |
| 149 | GET | `/api/institution/students/count` | InstitutionAccess + ruoli | InstitutionAdmin,Staff,Teacher | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionStudentController.cs:16` |
| 150 | GET | `/api/institution/students/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin,Staff,Teacher | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionStudentController.cs:18` |
| 151 | GET | `/api/institution/students/bylastname/{lastName}` | InstitutionAccess + ruoli | InstitutionAdmin,Staff,Teacher | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionStudentController.cs:20` |
| 152 | GET | `/api/institution/students/me` | InstitutionAccess + ruoli | Student | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionStudentController.cs:22` |
| 153 | POST | `/api/institution/students` | InstitutionAccess + ruoli | InstitutionAdmin,Staff | SchoolApi | perm UserCreate | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionStudentController.cs:24` |
| 154 | PUT | `/api/institution/students/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin,Staff | SchoolApi | perm UserUpdate | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionStudentController.cs:37` |
| 155 | DELETE | `/api/institution/students/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi | perm UserDelete | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionStudentController.cs:45` |
| 156 | POST | `/api/institution/students/{id:guid}/transfer-class` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi+CourseApi (filtro) |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionStudentTransferController.cs:18` |
| 157 | GET | `/api/institution/students/{id:guid}/class-history` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionStudentTransferController.cs:26` |
| 158 | GET | `/api/institution/study-areas` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionStudyAreaController.cs:13` |
| 159 | GET | `/api/institution/study-areas/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionStudyAreaController.cs:20` |
| 160 | POST | `/api/institution/study-areas` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionStudyAreaController.cs:30` |
| 161 | PUT | `/api/institution/study-areas/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionStudyAreaController.cs:37` |
| 162 | DELETE | `/api/institution/study-areas/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionStudyAreaController.cs:47` |
| 163 | GET | `/api/institution/study-paths` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionStudyPathController.cs:13` |
| 164 | GET | `/api/institution/study-paths/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionStudyPathController.cs:20` |
| 165 | POST | `/api/institution/study-paths` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionStudyPathController.cs:30` |
| 166 | PUT | `/api/institution/study-paths/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionStudyPathController.cs:37` |
| 167 | DELETE | `/api/institution/study-paths/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionStudyPathController.cs:47` |
| 168 | GET | `/api/institution/subjects` | InstitutionAccess + ruoli | InstitutionAdmin,Teacher,Student | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionSubjectController.cs:12` |
| 169 | GET | `/api/institution/subjects/count` | InstitutionAccess + ruoli | InstitutionAdmin,Teacher,Student | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionSubjectController.cs:14` |
| 170 | GET | `/api/institution/subjects/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin,Teacher,Student | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionSubjectController.cs:16` |
| 171 | GET | `/api/institution/subjects/byname/{name}` | InstitutionAccess + ruoli | InstitutionAdmin,Teacher,Student | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionSubjectController.cs:18` |
| 172 | GET | `/api/institution/subjects/byteacher/{teacherId:guid}` | InstitutionAccess + ruoli | InstitutionAdmin,Teacher,Student | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionSubjectController.cs:20` |
| 173 | POST | `/api/institution/subjects` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionSubjectController.cs:22` |
| 174 | PUT | `/api/institution/subjects/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionSubjectController.cs:29` |
| 175 | DELETE | `/api/institution/subjects/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionSubjectController.cs:36` |
| 176 | GET | `/api/institution/submissions/{id:guid}/session` | InstitutionAccess + ruoli | Student | SchoolApi+ExamApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionSubmissionController.cs:12` |
| 177 | PUT | `/api/institution/submissions/{id:guid}/answers` | InstitutionAccess + ruoli | Student | SchoolApi+ExamApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionSubmissionController.cs:14` |
| 178 | POST | `/api/institution/submissions/{id:guid}/submit` | InstitutionAccess + ruoli | Student | SchoolApi+ExamApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionSubmissionController.cs:16` |
| 179 | POST | `/api/institution/submissions/{id:guid}/submit-expired` | InstitutionAccess + ruoli | Student | SchoolApi+ExamApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionSubmissionController.cs:18` |
| 180 | POST | `/api/institution/submissions/{id:guid}/violations` | InstitutionAccess + ruoli | Student | SchoolApi+ExamApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionSubmissionController.cs:26` |
| 181 | GET | `/api/institution/submissions/{id:guid}/violations` | InstitutionAccess + ruoli | InstitutionAdmin,Staff,Teacher | SchoolApi+ExamApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionSubmissionController.cs:28` |
| 182 | GET | `/api/institution/submissions/{id:guid}/work` | InstitutionAccess + ruoli | Student | SchoolApi+ExamApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionSubmissionController.cs:30` |
| 183 | GET | `/api/institution/submissions/{id:guid}/review` | InstitutionAccess + ruoli | InstitutionAdmin,Staff,Teacher | SchoolApi+ExamApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionSubmissionController.cs:32` |
| 184 | PUT | `/api/institution/submissions/{id:guid}/review` | InstitutionAccess + ruoli | InstitutionAdmin,Teacher | SchoolApi+ExamApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionSubmissionController.cs:34` |
| 185 | POST | `/api/institution/submissions/{id:guid}/finalize` | InstitutionAccess + ruoli | InstitutionAdmin,Teacher | SchoolApi+ExamApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionSubmissionController.cs:41` |
| 186 | GET | `/api/institution/submissions/{id:guid}/result` | InstitutionAccess + ruoli | InstitutionAdmin,Staff,Teacher,Student | SchoolApi+ExamApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionSubmissionController.cs:48` |
| 187 | GET | `/api/institution/teachers` | InstitutionAccess + ruoli | InstitutionAdmin,Staff,Teacher,Student | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionTeacherController.cs:14` |
| 188 | GET | `/api/institution/teachers/count` | InstitutionAccess + ruoli | InstitutionAdmin,Staff,Teacher | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionTeacherController.cs:16` |
| 189 | GET | `/api/institution/teachers/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin,Staff,Teacher,Student | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionTeacherController.cs:18` |
| 190 | GET | `/api/institution/teachers/bylastname/{lastName}` | InstitutionAccess + ruoli | InstitutionAdmin,Staff,Teacher,Student | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionTeacherController.cs:20` |
| 191 | GET | `/api/institution/teachers/me` | InstitutionAccess + ruoli | Teacher | SchoolApi |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionTeacherController.cs:22` |
| 192 | POST | `/api/institution/teachers` | InstitutionAccess + ruoli | InstitutionAdmin,Staff | SchoolApi | perm UserCreate | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionTeacherController.cs:24` |
| 193 | PUT | `/api/institution/teachers/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin,Staff | SchoolApi | perm UserUpdate | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionTeacherController.cs:32` |
| 194 | DELETE | `/api/institution/teachers/{id:guid}` | InstitutionAccess + ruoli | InstitutionAdmin | SchoolApi | perm UserDelete | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionTeacherController.cs:40` |
| 195 | POST | `/api/Auth/invitations/{id:guid}/accept` | Selection JWT | — | nessuno |  | `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InvitationAcceptanceController.cs:17` |

## 7. Falsi positivi scartati e scelte deliberate

**SQL injection: 44 file su 44 verificati, nessuna iniezione.** Tutti i valori arrivano come parametri (`@p`, `{0}` di `FromSqlRaw`, interpolazione EF parametrizzata). I frammenti dinamici sono di tre tipi:

- Costanti o allowlist: `InstitutionAdministrationPersistence.cs:16-17` (`Tables`/`Table()`); i nomi di tabella e colonna in `InstitutionEmailRepository.cs:289-292`, `InstitutionImportRepository.cs:160`, `InstitutionCalendarRepository.cs:293,353`, `InstitutionFormativePlanRepository.cs:326-364`; `Visible`/`Own` sono costanti.
- Metadati `information_schema` dello stesso schema, quotati con backtick: `InstitutionAdministrationPersistence.cs:79-81` con escape del backtick; `Mt5cOnboardingIntegrity.cs:63-65` e `Mt5GovernanceIntegrity.cs:52-54` senza escape, ma i nomi vengono dal DDL controllato dal proprietario.
- `CURRENT_USER()` (`CalendarIntegrity.cs:23`, `ClassDeletionIntegrity.cs:18`) oppure tool di test (`Mt3cTestPreparation`).

Nessuna stringa che arriva dalla richiesta HTTP entra nel testo SQL.

**Mass assignment e over-posting.** I manager copiano esplicitamente solo i campi ammessi. Ad esempio `InstitutionStudentManager.cs:58` copia solo i nomi, e `InstitutionStudentRepository.cs` usa `ExecuteUpdate` sui soli nomi. `InstitutionId` non arriva mai dal client: `AddOwned` lo imposta dal tenant, e `InstitutionExamDataScope.SaveAsync` rifiuta i cambi di owner (`:177-184`). `StaffBodyContractAttribute.cs:18,25` controlla solo le proprietà di primo livello per lo Staff, ma è una difesa aggiuntiva.

**Isolamento multi-tenant (priorità massima).** Ho seguito il flusso, e il tenant deriva sempre dall'identità:

- Il JWT `InstitutionAccess` porta `institution_id`, `membership_id`, i ruoli e `sg`. Viene rivalidato sul DB (`InstitutionAuthManager.cs:51-55`) e conservato in `CurrentTenant`, che è scoped.
- I repository filtrano per `EF.Property<Guid>(e,"InstitutionId") == tenant.InstitutionId` (`InstitutionSchoolQueries.Owned`, `InstitutionExamDataScope.Owned`) o con `institution_id=@t` nel SQL raw.
- La factory degli esami rifiuta un principal diverso dal tenant verificato (`InstitutionExamRepositoryFactory.cs:30-32`).
- Il worker in background usa un owner ririsolto dalla persistenza (`SubmissionExpiryStore.cs:43-48`) e uno scope tecnico con scritture in allowlist.
- Non ho trovato `HasQueryFilter` (il filtro è esplicito), né cache condivise: nessun `IMemoryCache` né dizionari statici di dati tenant.
- `ExamRepository.cs:126` (`SELECT * FROM subject WHERE id = {subjectId} FOR UPDATE` senza tenant) è un falso positivo: `subjectId` viene da un `ClassSubject` letto nello scope, e le FK composite vincolano lo stesso tenant.
- `SubmissionExpiryStore.SelectAsync`, che scansiona tutti i tenant, è deliberato: legge solo chiavi e ririsolve ogni elemento.

La matrice `docs/architecture/MULTITENANCY_ISOLATION_MATRIX.md` (sezioni MT-3C…MT-5A) corrisponde al codice nei punti controllati. Le sezioni storiche descrivono fasi superate.

**XXE, zip bomb e path traversal.**

- `DtdProcessing.Prohibit` e `XmlResolver = null` (`InstitutionImportFileReader.Xlsx.cs:208`).
- Limiti su entry, rapporto di compressione (1000), byte espansi, nodi e profondità XML (`:172-255`).
- Rifiuto di macro, parti binarie, relazioni esterne, `..`, `\` e `:`.
- Il nome file serve solo a ricavare l'estensione e non viene scritto su disco.

**Formula injection.** I valori che iniziano con `= + - @` vengono rifiutati (`InstitutionImportRules.cs:120`, `CalendarImportRules.cs:55`), e le formule XLSX sono vietate. Non esistono export CSV di dati: i template sono statici.

**Altre verifiche (sicurezza applicativa e deploy).**

- **CORS**: origini esplicite, solo HTTPS, nessun `*`, nessuna credenziale (`RuntimePolicy.cs:27-28`).
- **AllowedHosts `"*"`**: c'è solo nell'esempio LocalTest (`appsettings.example.json:10`); in Staging e Production `*` viene rifiutato (`RuntimePolicy.cs:46-48`).
- **OpenAPI e Scalar**: solo in Development (`Program.cs:46-50`); un test lo verifica (`AuditBootstrapTests`, vedi sotto).
- **HSTS**: 180 giorni fuori da LocalTest.
- **Header di sicurezza**, `no-store` e `X-Correlation-ID` generato dal server.
- **Kestrel**: 1 MB di default e timeout degli header a 15 s.
- **Eccezioni**: ProblemDetails senza messaggio né stack, log con codici in allowlist (`GlobalExceptionHandler.cs:34-41`).
- **Segreti**: secret tramite file o variabili d'ambiente; `appsettings*.json` esclusi dalla pubblicazione (`ElectronicRegisterAPI.csproj:10`).
- **TLS verso il DB**: `SslMode=VerifyFull` e CA obbligatori fuori da LocalTest (`RuntimePolicy.cs:48`); vietati `root`, password vuota e `ASPNETCORE_FORWARDEDHEADERS_ENABLED`.

**Validazione JWT.** Firma obbligatoria, solo HS256, issuer e audience distinti per schema, `typ` e `purpose`, lifetime con `ClockSkew=0`, rifiuto dei claim duplicati (anche JSON), di `role`/`roles` estranei e dei claim di profilo, generation di identità e membership confrontate con il DB a ogni richiesta (`InstitutionAuthentication.cs:53-114`). Non c'è confusione tra gli schemi locale e Microsoft: il token Microsoft non viene mai accettato come bearer. Viene scambiato per un ticket Selection solo tramite un binding esplicito `tid`/`oid` → UserId (`MicrosoftIdentityLink.cs:27-32`), senza usare l'email.

**Revoca e recupero.** Il cambio password incrementa la generation e revoca tutti i token (`IdentityAccountRepository.cs:150`). Il recupero "self-service" dell'account non esiste: è una scelta deliberata (`INSTITUTIONAL_API_AND_LEGACY_CUTOVER.md:39`). Esiste solo il recovery di piattaforma con doppia firma.

**PlatformOperator.** RSA-PSS SHA-256 con chiavi da almeno 3072 bit, rifiuto di PEM privati nel registro, impronte distinte per operatore, nonce di 32 byte salvato (`InstitutionGovernanceRepository.cs:41`), finestra massima di 300 s, intento firmato tipizzato (`PlatformIntent` è un record `sealed`, quindi la serializzazione è completa), e controllo ACL del file di configurazione della CLI (`tools/PlatformAdmin/Program.cs:49-55`).

**Concorrenza e integrità.**

- Lock in ordine tenant → risorsa (`InstitutionWriteAuthorization.cs:25-28`, `InstitutionExamDataScope.cs:127-135`).
- Idempotenza tramite EventId e hash della richiesta (membership, acquisizioni, import, governance).
- Revision e concurrency token.
- Esistono test dedicati: `OpenScoreDoubleCountTests`, `Phase3_ConcurrencyTests`, `Quality_PasswordPutBudgetIsSharedByTwoHostsAndCannotBeSpoofedByProxyHeaders`. Non li ho eseguiti, quindi il comportamento sotto deadlock reale resta **DA VERIFICARE IN RUNTIME**.
- La vulnerabilità TOCTOU sui controlli di autorizzazione è un falso positivo: le scritture rivalidano membership e ruoli con `LOCK IN SHARE MODE` dentro la transazione.

**Audit falsificabile.** L'account API ha DML su tutto lo schema, ma le tabelle di eventi hanno trigger `BEFORE UPDATE/DELETE` con `SIGNAL` (ad esempio `migrations/identity-account/001_identity_account.sql:22,24`), e l'account non ha il privilegio `TRIGGER` (`tools/prepare-release-sql.cjs:29`). L'attribuzione dell'attore viene dal tenant e non dal client (`InstitutionExamDataScope.cs:155`).

**INC-006 nella prima stesura (falso positivo, scartato nell'aggiornamento).** "Manifest di rilascio non allineato a HEAD" si basava su `tools/release-artifacts.cjs`, che certifica solo la release storica er-quality-1 (`8e7d9ec…`) e quindi diverge per costruzione da qualsiasi HEAD successivo. Il controllo corretto, `tools/mysql84-release.cjs`, passa su `b53b10b`. Il manifest storico non va rigenerato. Il nuovo INC-006 descrive il rischio di processo reale.

**Segreti nei file e nella cronologia.** Nei file versionati e nei 199 commit della storia ci sono solo segnaposto (`CHANGE_ME`, `REPLACE_SECRET`) e fixture (`fake-password`, `-----BEGIN PRIVATE KEY` dentro stringhe di test in `tools/test_staging.py` e in due patch di `docs/quality/staging-evidence/`). Gli `appsettings.json` cancellati (commit `e2c9e59`, `23c407e`) contenevano soltanto `CHANGE_ME`.

**Case sensitivity delle tabelle.** Lo schema e il SQL raw usano solo identificatori minuscoli: falso positivo.

**Scelte deliberate ma rischiose (già classificate sopra).**

- Registrazione anonima (VULN-001).
- Ritiro delle API legacy senza adapter (INC-014).
- Bootstrap non replayabile e riscritto dallo strumento (INC-005).
- Admin senza doppio controllo (VULN-010).
- Studenti che vedono l'elenco dei docenti dell'istituto (`InstitutionTeacherRepository.cs:14`): solo nomi, nello stesso tenant. È una scelta di prodotto da confermare, non conteggiata come problema.

**Test che non verificano del tutto ciò che dichiarano.**

- `AuditBootstrapTests.RealProgram_StartsAuthenticatesDocumentsAndStops(environment: "Production")` (`tests/Integration/AuditBootstrapTests.cs:30`) usa `--Deployment:Mode=LocalTest` (`:48`) e i flag obsoleti `Identity:Legacy:*` (`:56`). Verifica soltanto che OpenAPI non sia esposta con `environment=Production`, non che il programma parta con la policy Production. Il nome è fuorviante: va rinominato oppure va aggiunto un caso Production reale con configurazione completa.
- I test di integrazione forzano `STRICT_TRANS_TABLES` (`TestDatabaseSession`), quindi non provano il comportamento su un server non strict (INC-004).

Non ho trovato test che passino in modo vacuo: le fixture DB lanciano un'eccezione se manca `TEST_MYSQL_CONNECTION` (ad esempio `tests/Integration/Mt5bJwtTests.cs:51`).

## 8. Piano di risoluzione

I lotti sono piccoli e ordinati per dipendenza. Prima di ogni lotto vanno prese le decisioni elencate in §9.

### Regola di processo: closure di rilascio MySQL 8.4 (vale per ogni lotto)

La procedura è confermata in `tools/mysql84-release.cjs:107` (ramo `--write`, che riscrive solo `migrations/mysql84-remediation/contract.json` e `manifest.json`), in `docs/quality/mysql84-run7-remediation.md:169-174` ("Rigenerati soltanto manifest/contratto della candidata con `node tools/mysql84-release.cjs --write` … Nessun manifest storico … è stato modificato") e in `docs/quality/MYSQL_84_FULL_COMPATIBILITY_AUDIT.md:60` ("Sul working tree nuovo il comando corretto è `mysql84-release.cjs`").

1. **Perimetro.** La closure comprende i file tracciati e non tracciati non ignorati sotto `Backend/ElectronicRegisterAPI/src`, `tests/Unit`, `tests/Integration`, `tools`, `infra`, `migrations/mt1`, `migrations/mt3` e `migrations/identity-account`, con le estensioni `.cs`, `.csproj`, `.cjs`, `.py`, `.ps1`, `.sql`, `.json`, `.config`, `.yml`/`.yaml`, `.sh` e `.conf`. Comprende anche gli elenchi espliciti in `tools/mysql84-release.cjs:44-60`: il manifest storico, il bootstrap, il calendario e i file aggiuntivi come `.github/workflows/verify.yml` e `tests/Integration/IntegrationFixture.cs`. **Ne fanno parte anche 19 documenti esistenti in `docs/`**, ereditati dal manifest storico. Ne restano fuori i documenti nuovi in `docs/`: `docs/quality/BACKEND_REVIEW.md`, `docs/quality/BASELINE_MYSQL84.md` e `docs/architecture/DATABASE_TARGET_MYSQL84.md` (verificato rieseguendo il comando dopo ogni modifica).
2. **Prima di modificare**, eseguire `node tools/mysql84-release.cjs` sul punto di partenza e annotare l'esito, che deve essere verde.
3. **Dopo le modifiche del lotto**, eseguire di nuovo il comando. Se fallisce con `New release closure drift` o `Contract drift`, rivedere il diff dei file della closure (`git diff --stat` più la revisione del codice del lotto). Un drift su file che il lotto non doveva toccare (spazi bianchi, fine riga, file generati) va annullato, non certificato.
4. **Solo dopo la revisione**, rigenerare in modo deliberato con `node tools/mysql84-release.cjs --write`. Rivedere il diff di `migrations/mysql84-remediation/manifest.json` e `contract.json`: devono cambiare solo gli hash dei file toccati intenzionalmente. Un cambiamento di `contract.json` richiede una revisione dello schema.
5. **Rieseguire** `node tools/mysql84-release.cjs` e `node tools/test-mysql84-release.cjs`: entrambi devono dare exit 0.
6. **Committare** codice e manifest rigenerato **nello stesso commit**, citando nel messaggio i file la cui inclusione è stata rivista.
7. **Mai** eseguire `node tools/release-artifacts.cjs --write` né modificare `migrations/release/manifest.json`: è la release storica er-quality-1, immutabile e verificata da `staging-package.yml` sullo SHA `8e7d9ec…`.
8. Non modificare i 19 documenti di `docs/` che sono nella closure, salvo nei lotti che lo prevedono esplicitamente. In quel caso si applicano i punti 3–6.

### Lotto 0: decisioni e baseline (nessun codice)
- [x] D1 decisa (MySQL 8.4 LTS) e registrata in `docs/architecture/DATABASE_TARGET_MYSQL84.md`
- [ ] Accettazione temporanea del rischio EF Core 9: proposta in §9, **in attesa di conferma della data di revisione**
- [ ] D2 · D3 · D4 · D5 · D6
- [x] Baseline Unit e Integration su MySQL 8.4.13 usa e getta (`docs/quality/BASELINE_MYSQL84.md`): Unit 598/598; Integration 1566 superati, 16 falliti, 94 non eseguiti (nessun bug reale tra i test arrivati a verificare il comportamento)
- [x] INC-017 e INC-018 approvati e pianificati nel Lotto 0B

**Test.** `dotnet test tests/Unit/Unit.Tests.csproj -c Release` e tutta la suite `tests/Integration` (1676 casi) su MySQL 8.4.13, comprese le classi che la CI 8.4 non esegue (la CI copre 817 casi su 1676).

### Lotto 0B: test di sicurezza su MySQL 8.4 (solo test harness e CI, prima del Lotto 1)
- [x] INC-017 in locale: 93 casi superati su 95 nelle tre classi, più 2 su 2 in `Mysql84CapabilityScopeTests`; i 2 non superati sono legati a INC-012. [ ] conferma con la prima run verde della CI `iam-security`
- [x] INC-018

Nessuna modifica al codice di produzione in `Backend/ElectronicRegisterAPI/src`. Se un test riportato su 8.4 rivela un problema di produzione, si apre un nuovo ID invece di adattare il test. I file dell'harness e della CI sono nella closure: si applica la regola di processo qui sopra.

**Test.** `Mt5bSecurityStateTests`, `Mt5dMembershipMutationTests` e `Mt5bJwtTests` eseguiti davvero (non bloccati in inizializzazione) su MySQL 8.4.13 usa e getta, in locale e nella CI 8.4. `git status` pulito dopo la suite, salvo le modifiche volute.

### Lotto 1: correzioni rapide di autenticazione e ingresso
- [x] VULN-001 (D2: registrazione rimossa, login a costo uniforme; vedi §5)
- [ ] VULN-011
- [ ] VULN-004

**Test.** Unit e Integration per `IdentityAccountTests`, `AuditMicrosoftTests`, `Mt6RuntimeTests`. Nuovi test: risposta neutra della registrazione, `Verify` fittizio sul login, allowlist degli endpoint anonimi, AzureAd assente senza 500.

### Lotto 2: limiti di richiesta e topologia proxy
- [ ] VULN-003
- [ ] VULN-002
- [ ] INC-009
- [ ] VULN-006

**Test.** `Mt6RuntimeTests` completo e `IdentityAccountTests.Quality_Password*`. Nuovi test: quota login per account da IP diversi, avvio rifiutato senza `KnownProxies`, smoke dietro nginx con un file di 2 MB, concorrenza dell'anteprima calendario.

### Lotto 3: database target e comportamento SQL (dipende da D1)
- [ ] INC-002
- [ ] INC-003
- [ ] INC-004
- [ ] INC-007
- [ ] INC-005
- [ ] INC-010

**Test.** Workflow `mysql84-remediation-candidate.yml` completo, `node tools/test-mysql84.cjs`, `tools/test-release-manifest.cjs` e Integration completa sul target. Nuovi test: validatori con 8.4.x generica, readiness con server non strict (503), versione server fissa senza I/O nelle opzioni.

### Lotto 4: aggiornamento pacchetti (dipende da D1 e dal Lotto 3)
- [ ] INC-001

**Test.** `dotnet restore --locked-mode` dopo aver rigenerato i lock, `dotnet list package --vulnerable/--outdated`, suite Unit e Integration completa. In particolare `Mysql84GuidMetadataTests`, `Mysql84ForeignKeyMetadataTests`, `Phase3_ConcurrencyTests` e `OpenScoreDoubleCountTests`.

### Lotto 5: autorità operative di produzione (dipende da D3)
- [ ] INC-008 (necessario prima della produzione; oggi script del proprietario)
- [ ] INC-013
- [x] VULN-010 (accettato dal modello definitivo, §1.1; nessuna doppia approvazione)
- [ ] VULN-009

**Test.** `Mt5cOnboardingTests`, i test di governance e `tools/test_windows_private_acl.py`. Nuovi test: CLI contro un target di produzione attestato e uno non attestato, doppia approvazione della nomina admin.

### Lotto 6: pulizia legacy e robustezza
- [ ] INC-012, compresi i 7 metodi (9 casi) che dipendono dall'holding legacy (§4, INC-012) e le 2 esclusioni della CI 8.4 legate a INC-012
- [ ] INC-015
- [ ] INC-011

**Test.** Suite completa e `tools/CodeOrganization` esteso (repository senza costruttore privo di scope). Nuovi test: combinazioni dei flag `Multitenancy`, cancellazione classe con riga incoerente in un altro tenant.

### Lotto 7: dati versionati, supply chain e test
- [ ] VULN-007
- [ ] VULN-008
- [ ] VULN-005
- [ ] INC-016

**Test.** Controllo CI anti-percorsi e anti-TRX, build Docker con digest, rotazione JWT con due chiavi, migrazione a xunit.v3 con lo stesso numero di test di `--list-tests` (598 Unit, 1676 Integration).

### Lotto 8: chiusura del rilascio
- [ ] INC-014
- [ ] INC-006

**Test.** Rigenerare `INSTITUTIONAL_API_SURFACE.json` e lo snapshot OpenAPI. Poi verificare la closure 8.4 con la regola di processo qui sopra: `node tools/mysql84-release.cjs`, se serve `--write` dopo la revisione, quindi `node tools/mysql84-release.cjs` e `node tools/test-mysql84-release.cjs` con exit 0. Il manifest storico (`migrations/release/manifest.json`) non si rigenera.

## 9. Decisioni che spettano a te

| # | Decisione | Opzioni | Raccomandazione |
|---|---|---|---|
| D1 | Target DB e provider EF | **DECISA il 2026-11-05: MySQL 8.4 LTS è l'unico target di produzione** ([DATABASE_TARGET_MYSQL84.md](../architecture/DATABASE_TARGET_MYSQL84.md)). Il provider EF finale **non è deciso**: vedi i criteri in INC-001 e la proposta di accettazione del rischio qui sotto | — |
| D2 | Registrazione anonima | (a) Solo tramite invito. (b) Anonima con risposta neutra e verifica email. (c) Lasciare com'è | **DECISA (2026-11-05)**: nessuna registrazione libera. Il proprietario crea l'istituto e il primo admin; l'istituto crea i propri account (§1.1) |
| D3 | Autorità di piattaforma in produzione | (a) CLI con target di produzione attestato (allowlist firmata, VerifyFull, server UUID). (b) Servizio interno dedicato. (c) Procedura SQL manuale con verbale | **(a)**: riusa firma, nonce e audit già implementati |
| D4 | Doppio controllo sulle nomine admin | (a) Doppia approvazione obbligatoria. (b) Notifica agli altri admin più audit. (c) Nessuna modifica | **(b)** ora e **(a)** per gli istituti con più di un admin |
| D5 | Login Microsoft | (a) Dietro flag con validazione all'avvio. (b) Rimuovere fino al bisogno reale. (c) Lasciare com'è | **(a)** se esiste un cliente Entra ID previsto, altrimenti **(b)** |
| D6 | Dati personali e storia git | (a) Ripulire le evidenze e aggiungere un controllo CI, senza riscrivere la storia. (b) Riscrivere la storia (filter-repo) e forzare il push. (c) Nessuna azione. Inoltre: politica di retention e anonimizzazione per eventi e snapshot | **(a)**, più una politica di retention scritta (durata, anonimizzazione degli snapshot negli eventi). La riscrittura della storia non è giustificata: non ci sono segreti né dati reali di studenti |

### Proposta di accettazione temporanea del rischio: EF Core 9 / Pomelo 9 (NON APPROVATA)

> Stato: **PROPOSTA**, in attesa di conferma. Finché non c'è conferma, la data di revisione non è approvata.

| Voce | Proposta |
|---|---|
| Rischio | Dal 2026-11-10 EF Core 9.0.x non riceve più patch di sicurezza Microsoft. Pomelo 9.0.0 è l'ultima versione disponibile e il suo uso su .NET 10 non è garantito da Microsoft (INC-001) |
| Perimetro | `Microsoft.EntityFrameworkCore*` 9.0.x e `Pomelo.EntityFrameworkCore.MySql` 9.0.0 nell'API e nei tool che la riusano. Non copre gli altri pacchetti, che vanno tenuti alle patch correnti di .NET 10 |
| Data di revisione proposta | **2026-12-15**, da confermare |
| Scadenza massima proposta | **2027-03-31**, da confermare. Entro questa data il provider EF 10 deve essere scelto e il Lotto 4 pianificato |
| Condizioni | (1) Lotto 3 avviato su MySQL 8.4. (2) Controllo settimanale degli advisory GitHub e NuGet per EF Core 9, Pomelo e MySqlConnector, e `dotnet list package --vulnerable` in CI. (3) Nessuna nuova dipendenza che richieda EF 10. (4) Allineamento immediato di `Tools` a 9.0.8 e delle patch .NET 10/IdentityModel (INC-001 punto a) |
| Revoca anticipata | Un advisory con impatto su EF Core 9, Pomelo o MySqlConnector senza mitigazione applicabile, oppure un'incompatibilità di runtime osservata su .NET 10 |
| Cosa serve alla revisione | Esito dello spike con il provider candidato sulla baseline 8.4, stato di Pomelo 10, costo della convivenza o migrazione MySqlConnector → MySql.Data (criteri in INC-001) |
