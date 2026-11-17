# Test d'integrazione non superati: causa e correzioni

| Voce | Valore |
|---|---|
| Data | 2026-11-17 |
| Branch | `refactor-database-entities` |
| Commit di partenza | `22aae74` (baseline), `9a4b46e` + `b4d9bba` (D-07, D-02) |
| Commit di questo intervento | `183ff1a` (INC-003), `e4ab650` (INC-012), `57cb316` (classe non assegnata) |
| Esito finale | CI verde su `57cb316`: tutte e 5 le run, 1741 casi d'integrazione eseguiti e superati su 1745, 4 esclusi con ID |
| Bug reali del codice di produzione | Nessuno |
| Regressioni dei commit D-07 / D-02 | Nessuna |

## 1. Sintesi

I 127 casi non superati (45 metodi) del report precedente **non indicavano difetti del codice**. Dipendevano da come la suite era stata lanciata in locale:

- un solo processo e un solo database per tutte le classi;
- senza `ER_TEST_MYSQL84=1`;
- includendo test che la CI esclude con un ID.

La CI esegue invece un processo e un database MySQL 8.4.13 usa-e-getta per ogni metodo, tramite `tools/ci_diagnostic.py`. Sulla stessa base (`22aae74`) era verde.

Con l'esecuzione corretta, tutti i 45 metodi sono superati in CI su `57cb316`. In più sono state risolte due cause di esclusione storiche ed è stata trovata una lacuna di copertura:

| Causa | Casi | Azione |
|---|---|---|
| INC-003: i test si aspettavano la regola FK `RESTRICT` (5.7), mentre 8.4 riporta `NO ACTION` | 2 | Test aggiornati: verificano il comportamento reale. Esclusione tolta |
| INC-012: holding legacy `MT3A_LEGACY_TEST` assente nel bootstrap 8.4 | 9 (7 metodi) | I test creano l'holding solo nel container usa-e-getta. Esclusioni tolte |
| Classe mai eseguita: `PlatformIdentitySchemaTests` senza dominio CI | 5 | Assegnata al dominio `full` |

## 2. Stato della CI prima dell'intervento

Run su `22aae74` del 2026-11-15, tutte verdi:

| Workflow | Run | Esito |
|---|---|---|
| MySQL 8.4 corrective release and upgrade | [43656847600](https://github.com/xxdema10xx/ElectronicRegister/actions/runs/43656847600) | success |
| Integration full (extended suite on MySQL 8.4) | [43656847681](https://github.com/xxdema10xx/ElectronicRegister/actions/runs/43656847681) | success |
| Verify portable backend | 43656847630 | success |
| Staging package on current code | 43656847661 | success |
| Historical er-quality-1 package and real SQL TLS | 43656847618 | success |

I commit `9a4b46e` e `b4d9bba` non erano stati pushati: non esisteva una CI per loro da sola. Sono stati verificati in CI insieme alle correzioni, su `57cb316`.

## 3. Numeri

Conteggi ricavati dalla discovery reale (`dotnet test --list-tests`) e dalla partizione `tools/integration_partition.py` di ciascun commit. Gli esiti vengono dai report delle run CI.

**Integration**

| Commit | Casi totali | Eseguiti e superati in CI | Non superati | Esclusi con ID | Mai eseguiti |
|---|---|---|---|---|---|
| `22aae74` (baseline) | 1733 | 1713 (1710 Linux + 3 `windows-cli`) | 0 | 15 (13 di metodi esclusi + 2 casi) | 5 (`PlatformIdentitySchemaTests`, senza dominio) |
| `b4d9bba` (con D-07 e D-02) | 1745 | nessuna run CI propria; con la partizione di allora 1725 eseguibili | — | 15 | 5 |
| `57cb316` (dopo le correzioni) | 1745 | **1741** (1738 Linux + 3 `windows-cli`) | **0** | **4** | **0** |

Su `57cb316`:
- **Dominio `full`:** report del gate PASS, 781 casi, 777 eseguiti, 4 di metodi esclusi, nessun caso escluso.
- **Altri domini:** 961 casi in 451 metodi, tutti PASS.

| Dominio | Metodi | Casi |
|---|---|---|
| calendar | 41 | 55 |
| email | 26 | 39 |
| formative | 56 | 87 |
| iam | 62 | 114 |
| iam-security | 54 | 97 |
| imports | 29 | 49 |
| onboarding | 41 | 60 |
| tenants | 142 | 460 |
| full | 393 (+4 esclusi) | 777 (+4 esclusi) |
| windows-cli | 3 | 3 |

**Unit:** `22aae74` 668/668, `57cb316` 683/683 (+15 test dei commit D-07/D-02). Eseguiti in locale (Release) e dal job di CI.

**Il run locale del report precedente** (`b4d9bba`, un solo processo, senza `ER_TEST_MYSQL84=1`) dava 341 superati e 127 non superati, contro i 131 di `22aae74` con lo stesso metodo. Non è una misura valida della suite: vedi §4.

## 4. Casi non superati iniziali: categoria e azione

I 127 casi (45 metodi) del run locale. Esito su `57cb316` in CI: **tutti superati**.

| Test (casi) | Errore iniziale | Categoria | Azione |
|---|---|---|---|
| `Mt5aMembershipAdminTests`, 10 metodi (36) | Fallimento nel setup della classe | **AMBIENTE**: le tre classi MT5 condividono la collection `Mt5aIdentityOnly` (`ICollectionFixture<Mysql84IdentityFixture>`), che crea il container 8.4 solo con `ER_TEST_MYSQL84=1` (`Mysql84FixtureTarget.cs`, `Mysql84IdentityFixture.InitializeAsync`). Senza la variabile le classi prendono il percorso 5.7 | Esecuzione come la CI. Nessuna modifica |
| `Mt5dMembershipMutationTests`, 19 metodi (31) | `MT3A explicit owner required` nel setup | **AMBIENTE**: come sopra | Come sopra |
| `Mt5eIamHttpTests`, 13 metodi (53) | `MT3A explicit owner required` nel setup | **AMBIENTE**: come sopra | Come sopra |
| `InstitutionAccountProvisioningTests.ImportAndAConcurrentSingleCreationObtainDistinctAddresses` (1) | `HttpClient.Timeout` di 15 s | **AMBIENTE**: carico della suite in un solo processo; in CI, con un processo per metodo, passa | Nessuna modifica |
| `Mt5dMembershipMutationTests.OfflineGraphClassifiesValidatedIamLedgerWithoutChangingData` (1) | holding assente | **ESCLUSO-CI** INC-012 | Corretto (§5.2) |
| `Phase4_ExamLifecycleTests.Audit_IdentityProfileFaultIsAtomicAndCannotChangeDidacticProfile` (2) | `MT3A explicit owner required` | **ESCLUSO-CI** INC-012 | Corretto (§5.2) |
| `HistoricalExpiryBoundaryTests.HistoricalOwnerRemainsTechnicalOnlyAndDeliveryIsIdempotent` (1) | `Sequence contains no elements` su `mt3a_legacy_scope` | **ESCLUSO-CI** INC-012 | Corretto (§5.2) |
| `Mt3dHttpTests.Release_WorkerDispatchRevalidatesTenantStateUnderLock`, `holding-active` (2) | come sopra | **ESCLUSO-CI** INC-012 | Corretto (§5.2) |

Nessun caso è risultato **REGRESSIONE**:
- prima di correggere qualcosa ho rieseguito su `22aae74`, con lo stesso metodo, gli stessi metodi falliti, e sono falliti anche lì (131 casi);
- tutti passano in CI con i commit D-07 e D-02.

Nessun caso è risultato **BUG REALE**.

## 5. Correzioni

### 5.1 INC-003: regola FK verificata per comportamento (`183ff1a`)

`Mt1IntegrityTests.CanonicalExpandIsRerunnableAndDoesNotChangeLegacyData` e `Mt4aCourseStructureTests.InformationSchemaHasCompositeRestrictConstraints` contavano le FK con `delete_rule='RESTRICT'`. Su 8.4 le stesse FK, dichiarate senza azione esplicita, sono `NO ACTION`. InnoDB le applica in modo identico, e la release 8.4 rifiuta addirittura un `RESTRICT` esplicito (`Mysql84ForeignKeyMetadataTests`).

- **Metadati:** sono ammessi `RESTRICT` o `NO ACTION`, mai `CASCADE` o `SET NULL`. Il numero di vincoli richiesto resta lo stesso (4 e 7).
- **Comportamento (nuovo):** per ogni FK il test cancella davvero la riga padre referenziata. Richiede l'errore 1451 con il nome del vincolo nel messaggio e la riga ancora presente. Il padre ha un solo tipo di figlio, così il vincolo è identificato esattamente. Due eccezioni:
  - `fk_mt4_edition_owner`: il padre `institution` è referenziato da più FK di owner, quindi si richiede 1451 su una FK qualsiasi;
  - `fk_membership_user`: si usa l'eccezione della fixture che supera il trigger `platform_user_delete`, così la barriera osservata è la FK.
- Esclusioni INC-003 tolte da `integration_partition.py`, con il test di blocco aggiornato.

### 5.2 INC-012: holding legacy solo nella fixture di test (`e4ab650`)

L'holding tecnica (`institution` `MT3A_LEGACY_TEST` + `mt3a_legacy_scope`) esiste sull'istanza 5.7, dove la crea il setup, ma non nel bootstrap 8.4. Il bootstrap non deve contenere dati legacy e non è stato toccato.

`tests/Integration/LegacyTestHolding.cs` la crea:
- con l'identità del provisioning 5.7 (id fisso, stato `provisioning`);
- in modo idempotente;
- **solo** dentro il container usa-e-getta attestato (`Mysql84FixtureTarget.IsOwned` + `VerifyAsync`). Su qualsiasi altro database non scrive nulla.

È lo stesso schema già usato da `Mt3aOwnershipTests`. Ogni test interessato la richiede come prerequisito. Nessuna asserzione è stata modificata e nessun trigger o schema è stato toccato.

Metodi tornati in CI: `Phase4…Audit_IdentityProfileFault…` (2), `HistoricalExpiry…HistoricalOwner…`, `Mt4a…ExistingBackfill…`, `Mt4a…LegacyEfWriters…`, `Mt5b…Sql_FixtureGuardExcludesRealLegacyHolding`, `Mt5d…OfflineGraph…`, `Mt3d…Release_WorkerDispatch…` casi `holding-active` (2).

Nel test di blocco:
- il meccanismo dei filtri sui casi resta coperto con un insieme esplicito;
- il controllo "esclusione obsoleta → errore" è verificato sul dominio `full`, che conserva esclusioni.

### 5.3 Classe mai eseguita (`57cb316`)

`PlatformIdentitySchemaTests` (5 metodi) è stata aggiunta in `904a239` senza proprietario in `integration_partition.py`. La CI filtra la discovery per dominio, quindi non la scopriva e la run restava verde. Ora appartiene a `full` (`FULL_CLASSES` 51 → 52).

**Rischio residuo:** una classe nuova non assegnata continua a non far fallire nulla, perché nessun controllo confronta la discovery completa con la partizione. Va aggiunto un controllo (vedi §7).

## 6. CI su `57cb316`

| Workflow | Run | Esito |
|---|---|---|
| Integration full | [44148762030](https://github.com/xxdema10xx/ElectronicRegister/actions/runs/44148762030) | success (14 shard + gate) |
| MySQL 8.4 corrective release and upgrade | [44148762019](https://github.com/xxdema10xx/ElectronicRegister/actions/runs/44148762019) | success al tentativo 3 (22/22 job) |
| Verify portable backend | 44148762060 | success |
| Staging package on current code | 44148762122 | success |
| Historical er-quality-1 package and real SQL TLS | 44148762112 | success |

La run principale ha richiesto due rilanci, **senza modifiche al codice**:

1. **Tentativo 1:** `integration (iam-security, 0, 4)` rosso. Lo step dei test era verde (`ci_diagnostic.py` fallisce in modo chiuso). È fallito il caricamento dell'artifact: `Failed to FinalizeArtifact: Unable to make request: ECONNRESET`, un errore di rete del servizio artifact di GitHub. Di conseguenza anche `final-gate` è rosso.
2. **Tentativo 2** ("Re-run failed jobs"): lo shard è verde, ma `final-gate` resta rosso con `Windows provenance mismatch: GITHUB_RUN_ATTEMPT`. Il report di `windows-cli` viene dal tentativo 1, e il gate (`tools/windows_cli_evidence.py:73-75`) rifiuta prove di Windows prodotte in un tentativo diverso. È una protezione corretta e non è stata indebolita. Conseguenza pratica: se una run principale va rilanciata, `windows-cli` deve essere rilanciato insieme al gate.
3. **Tentativo 3** (rilancio di `windows-cli`, che rilancia anche il gate dipendente): 22/22 verde.

## 7. Cosa resta escluso e decisioni richieste

| Test | ID | Causa provata | Decisione richiesta |
|---|---|---|---|
| `Mt3aOwnershipTests.AmbiguousLegacyAuditAbortsBackfillAndLeavesEveryRealCellUntouched` | INC-012 | Lo strumento di backfill legacy invoca `mysqldump.exe` di un'installazione MySQL 8.0 su Windows, assente sul runner Linux | Dismettere lo strumento legacy oppure portarlo su un dump eseguibile nel container |
| `Mysql84ReleaseTests.PublishedReleaseAndEmbeddedOnboardingValidationRunOnExactServer` | INC-017 | Richiede `ER_MYSQL84_RELEASE_CONFIG`, fornita solo dal job `release` (upgrade) della run principale, dove viene eseguito | Nessuna: già coperto dal job di release |
| `PrivateTestFilesTests.EachFixtureHasItsOwnPrivateDirectoryOutsideDatabaseDatadirs`, `…EveryoneReadIsActuallyAppliedAndCanBeRestoredOnTheNewFileOnly` | INC-013 | Controllo delle ACL di Windows: `PlatformNotSupportedException` su Linux | Spostarli nel job `windows-cli` se il team vuole eseguirli in CI |

INC-008 e INC-013 (CLI amministrativa, 3 metodi `WINDOWS_METHODS`) **non sono esclusi**: girano nel job dedicato `windows-cli` su MySQL 5.7.24 nativo, verde su `57cb316`.

**Proposta, non applicata:** un controllo in `tools/test_integration_partition.py` (o nel job di discovery) che assegni a un dominio ogni metodo della discovery completa. Impedirebbe che una classe nuova resti fuori dalla CI senza errori, come è successo a `PlatformIdentitySchemaTests`.

## 8. Note sull'esecuzione locale

- **Usare l'orchestratore:** `ER_TEST_MYSQL84=1 python tools/ci_diagnostic.py --integration <dominio> --shard <n> --shards <N>`. Un run della suite in un solo processo non è confrontabile con la CI.
- **Timeout del preflight:** su Windows con Docker Desktop il primo provisioning (immagine, bundle di release, avvio dell'API) supera spesso i 240 s del preflight (`PREFLIGHT_TIMEOUT`), e il dominio viene bloccato. Il timeout non è stato cambiato. Per la verifica locale delle correzioni ho eseguito ogni metodo in un proprio processo e con un proprio container, senza quel limite. Ogni metodo ha richiesto circa 150–240 s, quasi tutti di provisioning.
- **Manifest della release:** ogni modifica a file `.cs`, `.py` o `.json` sotto `src`, `tests` o `tools` richiede `node tools/mysql84-release.cjs --write`, altrimenti la fixture rifiuta il provisioning (`New release closure drift`). `contract.json` è rimasto invariato in tutti i commit. Non modificare sorgenti mentre una suite è in esecuzione nello stesso checkout.
- **Effetti collaterali:** dopo i test, `git status` mostrava solo le modifiche volute (INC-018). La build della soluzione rigenera i `packages.lock.json` di `tools/` e `docs/quality/final-assessment-evidence/`, e `tools/CodeOrganization` su Windows riscrive l'inventario in CRLF. Questi effetti sono stati ripristinati, non committati.
