# Baseline test su MySQL 8.4.13

| Voce | Valore |
|---|---|
| Data | 2026-11-05, esecuzione 12:54–13:22 (UTC+1) più una riesecuzione unica dei falliti |
| Branch / commit | `refactor-database-entities` @ `b53b10b` (codice identico a `03bb1be`). Working tree pulito a parte i documenti nuovi in `docs/` |
| Target | MySQL **8.4.13**, unico target di produzione secondo D1 ([DATABASE_TARGET_MYSQL84.md](../architecture/DATABASE_TARGET_MYSQL84.md)) |
| Esito Unit | **598 / 598 superati** |
| Esito Integration | **1676 casi: 1566 superati, 16 falliti durante l'esecuzione, 94 non eseguiti** perché bloccati in inizializzazione (`InitializeAsync` della classe, categoria A). Il report TRX conta questi 94 come "Failed", ma il corpo del test non è mai partito. Nella riesecuzione unica: 108 casi di nuovo non superati (94 non eseguiti + 14 falliti), 2 superati (dipendenza dall'ordine) |
| Bug reali del codice di produzione | **Nessuno tra i test arrivati a verificare il comportamento**: i 1566 superati e i 9 falliti per schema o versione (categorie B e C, causa identificata in §4). **Nessuna conclusione** per i 94 casi non eseguiti e per i 7 falliti per ambiente, che non hanno verificato il comportamento |

## 1. Ambiente e versioni

| Componente | Versione |
|---|---|
| .NET SDK / runtime | 10.0.401 (rispetta `global.json`) / 10.0.12 |
| Node | 24.20.0 |
| Python | **non installato** (motivo di 2 fallimenti, vedi §4) |
| Docker | Engine 29.9.2, linux/amd64 (Docker Desktop) |
| Immagine DB | `mysql:8.4.13@sha256:5cf1e1293ea42e077e6a22114e6773b9aae3bb8f9933a2acd5a9721518ff8f5b` |
| Testcontainers.MySql / xunit | 4.15.0 / 2.5.3 |

**Istanza.** Ogni gruppo ha usato un container usa e getta creato dalla fixture (`tests/Integration/IntegrationFixture.cs:194-230`), lo stesso percorso della CI. Dettagli:

- Nessun `TEST_MYSQL_CONNECTION`.
- `ER_TEST_MYSQL84=1`, come nel job `integration` di `.github/workflows/mysql84-remediation-candidate.yml:247`.
- Porta host casuale assegnata da Testcontainers, mai 3306. Datadir `/var/lib/mysql` interna al container.
- Container e volume rimossi a fine processo. Ho verificato che non ne è rimasto nessuno.
- Nessun controllo della fixture è stato modificato.
- Un container `mysql84-local` preesistente (porta 3307) **non è stato toccato**. I test lo escludono esplicitamente (`tests/Integration/Mysql84LocalReleaseTarget.cs:40`).
- Password e connection string non sono state stampate né salvate in questo repository.

**Differenza rispetto alla CI.** La CI 8.4 esegue **817 casi su 1676**: solo i domini filtrati in `tools/ci_diagnostic.py` e partizionati da `tools/integration_partition.py`, con un processo e un database per metodo. Questa baseline esegue **tutti i 1676 casi**, con un processo e un container per gruppo di classi. I test dello stesso gruppo condividono il container, quindi possono emergere dipendenze dall'ordine che la CI non vede (vedi C in §4).

## 2. Comandi

```text
dotnet build Backend/ElectronicRegisterAPI/ElectronicRegisterAPI.slnx --no-restore -c Release   # 0 errori, 19 avvisi (solo tests/)
dotnet test tests/Unit/Unit.Tests.csproj -c Release --no-build
# per ogni gruppo Gnn (classi elencate in §3), con ER_TEST_MYSQL84=1 e TEST_MYSQL_CONNECTION non impostata:
dotnet test tests/Integration/Integration.Tests.csproj -c Release --no-build \
  --filter "FullyQualifiedName~ElectronicRegisterAPI.Tests.Integration.<Classe>.|…" \
  --logger trx --blame-hang --blame-hang-timeout 12m
# riesecuzione unica dei 65 metodi falliti (114 casi, theory complete):
dotnet build tools/PlatformAdmin/PlatformAdmin.csproj -c Debug --no-restore   # i test CLI usano `dotnet run --no-build` in Debug
dotnet test tests/Integration/Integration.Tests.csproj -c Release --no-build --filter "FullyQualifiedName=…|…"
node tools/mysql84-release.cjs   # prima e dopo: verde
```

I file TRX e i log sono stati tenuti fuori dal repository (scratchpad di sessione) e **non sono versionati** (VULN-007).

## 3. Totali per gruppo

| Gruppo | Classi | Casi | Superati | Falliti | Non eseguiti | Durata |
|---|---|---|---|---|---|---|
| G01 | `InstitutionClassCrudTests` | 497 | 497 | 0 | 0 | 5 min 40 s |
| G02 | `InstitutionFormativePlanTests` | 137 | 137 | 0 | 0 | 3 min 2 s |
| G03 | `Mt3dHttpTests`, `Mt3cHttpTests`, `Mt3bSchoolTests`, `Mt3aOwnershipTests`, `Mt3ProfileTests`, `Mt3cPreflightTests` | 262 | 259 | 3 | 0 | 4 min 6 s |
| G04 | esami e voti: `Phase*`, `Exam*`, `FinalGradeValidationTests`, `GradeMappingTests`, `ConcurrencyIntegrationTests`, `OpenScoreDoubleCountTests`, `HistoricalExpiryBoundaryTests`, `FinalizeConcurrencyTests`, `Mt6WorkerTests` | 195 | 192 | 3 | 0 | 1 min 54 s |
| G05 | `Mt4bHttpTests`, `Mt4aCourseStructureTests` | 98 | 95 | 3 | 0 | 2 min 19 s |
| G06 | `Mt5cOnboardingTests` | 62 | 59 | 3 | 0 | 1 min 57 s |
| G07 | `Mt5eIamHttpTests`, `Mt2IdentityTests`, `IdentityAccountTests`, `HistoricalProfileAcquisitionTests`, `Mt2MicrosoftIdentityTests`, `IdentityDiagnosticPreflightTests` | 140 | 140 | 0 | 0 | 3 min 30 s |
| G08 | `Mt5bSecurityStateTests`, `Mt5aMembershipAdminTests`, `Mt5dMembershipMutationTests`, `Mt5bJwtTests` | 130 | 36 | 0 | 94 | 1 min 26 s |
| G09 | `InstitutionImportCsvReaderTests`, `InstitutionCalendarFileReaderTests`, `InstitutionImportXlsxReaderTests` | 57 | 57 | 0 | 0 | 7 s |
| G10 | runtime, fixture e metadati: `Mt6RuntimeTests`, `Mt1IntegrityTests`, `RuntimeFeatureAvailabilityTests`, `Mysql84*`, `LocalDatabasePortabilityTests`, `SqlLockEvidenceTests`, `MysqlFixtureConfigurationTests`, `UnavailableFixtureGuardTests`, `StagingCalendarFeatureTests`, `ReleaseLockObserverTests`, `PrivateTestFilesTests`, `Audit*`, `Quality*`, `DiagnosticPreflightTests` | 98 | 94 | 4 | 0 | 3 min 11 s |
| **Totale** | 61 classi | **1676** | **1566** | **16** | **94** | ≈ 27 min |

"Non eseguiti" indica i casi bloccati nell'inizializzazione della classe prima del corpo del test; nessun caso è stato saltato (`Skip`). I 10 gruppi coprono esattamente i 1676 casi elencati da `--list-tests`, senza duplicati (verificato prima dell'esecuzione).

## 4. Casi non superati

I 110 casi non superati sono di due tipi diversi:

| Tipo | Casi | Righe | Significato |
|---|---|---|---|
| **Non eseguiti, bloccati in inizializzazione** | 94 | A | `InitializeAsync` della classe fallisce sull'asserzione della porta prima di creare i dati e l'host; nessun metodo di test arriva al proprio corpo |
| **Falliti durante l'esecuzione** | 16 | B, C, D | Il corpo del test è partito. 2 falliscono su un'asserzione (B), 7 su un'eccezione del database nella preparazione dei dati (C), 7 per un prerequisito d'ambiente mancante (D) |

Le categorie richieste sono: bug reale, schema o versione, ambiente, test instabile. Ogni caso è stato rieseguito **una sola volta**.

| # | Test (casi) | Errore | Categoria | Riesecuzione | ID |
|---|---|---|---|---|---|
| A | `Mt5bSecurityStateTests` (39), `Mt5dMembershipMutationTests` (32), `Mt5bJwtTests` (23): **94 casi** | `Assert.Equal() Failure: Expected: 13306, Actual: <porta del container>` nell'inizializzazione della classe (`Mt5bJwtTests.cs:52`, `Mt5dMembershipMutationTests.cs:61`, `Mt5bSecurityStateTests.cs:64`). Le classi richiedono `TestMySqlTarget.Port`, cioè il MAMP 5.7 su 13306 oppure un 5.7.24 isolato (`TestMySqlTarget.cs:13-16`, `IntegrationFixture.cs:189`) | **Non eseguito**: harness legato a 5.7 | Di nuovo non eseguito (deterministico) | **INC-017** |
| B1 | `Mt1IntegrityTests.CanonicalExpandIsRerunnableAndDoesNotChangeLegacyData` | `Expected: 4, Actual: 0`: il test cerca FK con `delete_rule='RESTRICT'` (`Mt1IntegrityTests.cs:256-260`), mentre 8.4 riporta `NO ACTION` | Schema o versione | Fallito di nuovo | INC-003 |
| B2 | `Mt4aCourseStructureTests.InformationSchemaHasCompositeRestrictConstraints` | `Expected: 7, Actual: 0`: stessa causa (`Mt4aCourseStructureTests.cs:161-162`) | Schema o versione | Fallito di nuovo | INC-003 |
| C1 | `Phase4_ExamLifecycleTests.Audit_IdentityProfileFaultIsAtomicAndCannotChangeDidacticProfile` (2) | `MySqlException: MT3A explicit owner required` su un inserimento EF legacy senza owner (`AuditRegressionTests.cs:55`) | Schema o versione: presuppone l'holding legacy MT3A, assente nel bootstrap pulito | Fallito di nuovo | INC-012 |
| C2 | `HistoricalExpiryBoundaryTests.HistoricalOwnerRemainsTechnicalOnlyAndDeliveryIsIdempotent` | `Sequence contains no elements` su `mt3a_legacy_scope` (`HistoricalExpiryBoundaryTests.cs:26`) | Schema o versione: holding legacy assente | Fallito di nuovo | INC-012 |
| C3 | `Mt3dHttpTests.Release_WorkerDispatchRevalidatesTenantStateUnderLock(state: "holding-active", …)` (2) | `Sequence contains no elements` su `mt3a_legacy_scope` (`ReleaseWorkerDispatchTests.cs:29`). Gli altri stati della stessa theory sono passati | Schema o versione: holding legacy assente | Fallito di nuovo | INC-012 |
| C4 | `Mt4aCourseStructureTests.ExistingBackfillIsIdempotentWithAttributedMt4Graph` | `Explicit legacy institution is absent` (`Mt4aCourseStructureTests.cs:198`) | **Test instabile per dipendenza dall'ordine** (stato legacy condiviso) | **Superato** | INC-012 |
| C5 | `Mt4aCourseStructureTests.LegacyEfWritersStillReceiveTriggerDerivedOwners` | `MT3A explicit owner required` (`Mt4aCourseStructureTests.cs:217`) | **Test instabile per dipendenza dall'ordine** | **Superato** | INC-012 |
| D1 | `StagingCalendarFeatureTests.EverySmokeCaseMatchesRegisteredMvcRoutesPoliciesAndRealDtoContracts`, `…GeneratedStagingRuntimeKeepsCalendarControllerDiscoverable` | `Python was not found` (`StagingCalendarFeatureTests.HttpContracts.cs:51`) | Ambiente | Fallito di nuovo | — (nessun ID: prerequisito locale) |
| D2 | `Mysql84ReleaseTests.PublishedReleaseAndEmbeddedOnboardingValidationRunOnExactServer` | `Explicit disposable MySQL 8.4 release config required` (`Mysql84ReleaseTests.cs:19`): richiede `ER_MYSQL84_RELEASE_CONFIG`, fornita solo dal job `release` della CI | Ambiente | Fallito di nuovo | — |
| D3 | `Mt3aOwnershipTests.AmbiguousLegacyAuditAbortsBackfillAndLeavesEveryRealCellUntouched` | `Win32Exception`: manca `mysqldump.exe` di un'installazione MySQL 8.0 locale (`Mt3aOwnershipTests.cs:241`) | Ambiente (tooling legacy) | Fallito di nuovo | INC-012 |
| D4 | `Mt5cOnboardingTests.ActualAdministrativeCliVerifiesSignedIntentAndUsesOnlyOwnerInstalledChannel`, `…Governance_ActualCliUsesTheDedicatedManagerWithoutRequiringNotificationDelivery`, `…PlatformCliRefusesAuthorityConfigurationReadableByEveryoneBeforeMutation` | `CLI refused the test operator` / `Governance CLI failed` / `Private authority configuration must be accepted`. Prima esecuzione: manca la build Debug di PlatformAdmin. Riesecuzione con build Debug: la CLI rifiuta il container, perché `tools/PlatformAdmin/LocalMysql84Target.cs:25-30` accetta solo container `er84-dotnet-<hex>` con volume nominativo, creati dalla fixture solo con `ER_TEST_MYSQL84_RETAIN=1` (`IntegrationFixture.cs:202-206`). In CI sono i `WINDOWS_METHODS` eseguiti dal job `windows-cli` su 5.7.24 | Ambiente: harness CLI | Fallito di nuovo | INC-008, INC-013 |

**Totale:** non eseguiti A 94 + falliti (B 2 + C 7 + D 7 = 16) = **110**.

Tra i test che sono arrivati a verificare il comportamento (B e C) nessun fallimento indica un bug reale del codice applicativo. Per A (non eseguiti) e D (prerequisiti mancanti) non si può concludere nulla sul comportamento. Le categorie A, B e C mostrano però che **parte della suite verifica ancora assunzioni 5.7** (porta 13306, regola FK `RESTRICT`, holding legacy MT3A). Quelle aree sono di fatto senza copertura sul target 8.4.

### Elenco completo della categoria A (94 casi non eseguiti)

- **`Mt5bSecurityStateTests` (39):** `MappingHasCompositeOwnerRestrictForeignKeyAndNoNavigationGraph`, `MembershipMutationAndAcquisitionRequireExplicitWriteGates`, `ScopedReaderUsesPersistedGenerationAndFailsClosedOutsideOwnerOrWhenAbsent`, `Sql_BackfillInitializesMissingOnlyWithoutResettingExistingGeneration`, `Sql_CompositeForeignKeyRejectsWrongOwnerAndOrphanAfterPrivilegedFixturePurge`, `Sql_ConcurrentChangesHaveNoLostOrDuplicateIncrements`, `Sql_FixtureGuardRequiresExplicitOwnerAndSyntheticCodeAndExcludesHolding`, `Sql_IdentityResetAndOperationalDeleteAreRejected` ×7, `Sql_InitialStateAndOwnerArePersistent`, `Sql_InsertSelectMultirowAndDuplicateNoopRemainCompatible`, `Sql_InstitutionMutexSerializesAndSecondTransactionSeesCommittedGeneration`, `Sql_MissingStateFailsClosedForRoleAndStatus`, `Sql_MissingStateRejectsNoopsAndRoleDelete` ×3, `Sql_NewStateRejectsAnyNonzeroInitialGeneration` ×2, `Sql_OverflowRejectsRoleDeleteWithoutLosingRole`, `Sql_OverflowRollsBackTheMutationWithoutReset`, `Sql_RoleAddRemoveRestoreNeverRestoresGeneration` ×4, `Sql_RollbackPreservesGenerationAndBothTenants`, `Sql_SidecarIdentityAndNegativeGenerationAreRejected` ×3, `Sql_StatusRoundtripAndNoopAreMonotone`, `Sql_SuspendedTimestampAloneDoesNotAdvanceGeneration`, `TechnicalProvisioningUsesSqlGenerationWithoutInventingHumanActorOrProfiles`, `TechnicalWritersCannotMutateOperationalInstitutionsEvenForGlobalAdmin` ×3.
- **`Mt5dMembershipMutationTests` (32):** `AuditCannotBeChangedOrPurgedWithoutExplicitSyntheticOwnership`, `AuditOrCommitFailureRollsBackMutationGenerationAndEvent` ×2, `AuthorizationIsCurrentScopedAndNeverUsesGlobalUserAdmin` ×5, `ConcurrentConflictingReuseOfEventIdHasOnlyOneWinner`, `ConcurrentSameEventAppliesOnceAndDistinctStaleRevisionLoses`, `ConflictDoesNotAdvanceRevisionOrAppendAudit` ×3, `EventIdentityIsTenantScopedAndCannotAffectOtherTenant`, `ExhaustedRevisionIsRejectedWithoutAuditOrWrapping`, `ForgedOrIncompleteInternalContextCannotAuthorizeWrite` ×3, `MissingTargetStateFailsClosedEvenForAnExistingReplay`, `MutualAdminRevocationsSerializeAndLeaveAnActiveAdmin` ×2, `NestedRoleSnapshotsAreRejectedByTheAdditiveSqlGuard`, `OfflineGraphClassifiesValidatedIamLedgerWithoutChangingData`, `OperationalIamCannotWriteIntoNonActiveInstitution` ×3, `RealJwtIsPermanentlyRevokedByIamAndOtherTenantRemainsValid`, `ReplayReauthorizesRevokedActorUnderMutex`, `ReplayReturnsOriginalResultAndConflictingEventDoesNotMutate`, `RolesAndStatusAreAuditedWithSqlOwnedRevisionAndTenantIsolation`, `SoleAdminCannotRemoveTheirOwnAuthority` ×2.
- **`Mt5bJwtTests` (23):** `DuplicateSignedJwtHeaderPropertiesAreRejected` ×2, `InstitutionAccessNeverOpensLegacyEndpointGate`, `InvalidGenerationClaimsAreRejectedBeforeAuthorization` ×13, `MissingStateDeniesExistingTokenAndNewSelection`, `RealProgramIssuesPersistedGenerationAndNeverGlobalAdminPrivilege`, `RemovedMembershipAndGenerationBearingSelectionTicketFailClosed`, `RestoringRolesOrStatusNeverRevivesOldTokenAndDoesNotRevokeB` ×2, `SignedTenantMismatchAndStaleOrFutureGenerationAreForbidden`, `StatusNoopAndRolledBackMutationKeepIssuedTokenValid`.

## 5. Isolamento multi-tenant: esito

**Confermato su MySQL 8.4 per i percorsi HTTP (compreso, in parte, l'isolamento IAM via HTTP), i repository e le scritture applicative. Non verificato su 8.4 per la revoca dei token basata sulla security generation, le mutazioni di ruolo e l'isolamento delle mutazioni IAM (`Mt5bJwtTests`, `Mt5bSecurityStateTests`, `Mt5dMembershipMutationTests`), né per la CLI di piattaforma.**

L'affermazione di `BACKEND_REVIEW.md` §1 ("nessuna falla sfruttabile via HTTP", da analisi statica) è coerente con questi test, **passati** su 8.4:

| Area | Test superati (esempi) |
|---|---|
| Lettura e scrittura istituzionale, ID esteri | `InstitutionClassCrudTests.EveryOperationIsTenantScopedAndForeignIdsLookMissing`, `…AcademicForeignTenantIsIndistinguishableAndUnknownOwnerRejected`, `Mt3cHttpTests.CrossTenantIdsAreIndistinguishableFromMissingAndNeverChangePersistedData`, `Mt3bSchoolTests.CrossTenantMutationIsGenericNotFoundAndBUnchanged`, `Mt4bHttpTests.Mt4cSecurity_ForeignAndMissingResourcesAreIndistinguishableAndTenantRolesNeverCombine` |
| Contesto tenant per richiesta | `Mt2IdentityTests.SimultaneousRequestsNeverShareTenantContext`, `Mt3cHttpTests.ConcurrentHttpRequestsOfSharedIdentityKeepSeparateTenantScopes`, `InstitutionClassCrudTests.TransferParallelTenantContextsCannotBleedAcrossRequests`, `Mt4bHttpTests.ParallelTenantQueriesAndPostsNeverMixDataAndSqlWritersCarryOwner` |
| Esami, voti e visibilità | `Phase4_ExamLifecycleTests.Audit_GradeReadsCannotEscapeOwnership`, `…Audit_StudentTeacherSubjectsCannotBypassClassVisibility`, `Phase2C_VisibilityTests` (10 su 10), `Mt3dHttpTests.HttpSqlHasTenantParametersConstantListCostAndExplicitOwnedWrites` |
| Worker in background | `Mt3dHttpTests.Mt3eWorkerDeliversBothTenantsWithEmptyRequiredAndPreservesAttempt`, `…Mt3eFailedItemDoesNotBlockOtherTenantAndNewSweepRecovers`, `…Release_WorkerDispatchRevalidatesTenantStateUnderLock` (tutti gli stati tranne `holding-active`) |
| FK composite e owner nel DB | `Mt3aOwnershipTests.RealCompositeForeignKeysRejectCrossTenantUpdate`, `Mt4aCourseStructureTests.CompositeForeignKeysRejectOtherTenant`, `Mt3ProfileTests.SqlRejectsCrossInstitutionMembershipDuplicateOwnershipAndInvalidReferences` |
| Gate e policy | `Mt3cHttpTests.GateMetadataCannotBypassMissingContextOrWrongPolicy`, `Mt5aMembershipAdminTests.MarkerCannotBypassGateWithCombinedOrAlternativeMetadata`, `Mt5eIamHttpTests.NonTenantAdminCannotWriteEvenWithGlobalAdminRole`, `…ForeignAndAbsentTargetsAreIndistinguishableAndLeaveBothTenantsUntouched` |
| Import, email, calendario | `InstitutionClassCrudTests.BulkImport_DatabaseGuardsProtectHistoryAndCrossTenantLinks`, `…EmailPolicy_TenantsHaveIndependentConventionsAndForeignRecipientsAreNotFound`, `InstitutionFormativePlanTests.Calendar_TeacherInTwoInstitutionsCannotLeakLessonsAcrossSelectedContexts` |

L'isolamento IAM via HTTP è coperto in parte: `Mt5eIamHttpTests` 53/53 e `Mt5aMembershipAdminTests` 36/36 superati.

**Non verificato su 8.4** (categoria A, non eseguita, più D4): isolamento e revoca delle mutazioni IAM (`Mt5dMembershipMutationTests.EventIdentityIsTenantScopedAndCannotAffectOtherTenant`, `…RealJwtIsPermanentlyRevokedByIamAndOtherTenantRemainsValid`, `…AuthorizationIsCurrentScopedAndNeverUsesGlobalUserAdmin`), security generation (`Mt5bSecurityStateTests`), validazione JWT della generation (`Mt5bJwtTests`) e CLI di piattaforma. Queste classi non sono nemmeno nei filtri della CI 8.4. Finché INC-017 non è risolto, l'affermazione sull'isolamento resta **da analisi statica** per queste aree.

## 6. Effetti collaterali osservati

- **L'esecuzione dei test riscrive 6 file versionati** in `tests/Integration/TestResults/` (`mt3c/http-query-observations.json`, `mt3d/http-query-observations.json`, `mt3d/write-statements.sql.txt`, `mt3e/worker-query-observations.json`, `mt4b/course-sql.json`, `mt5a/directory-sql.json`). Contengono solo osservazioni SQL, senza percorsi, host né password. Li ho **ripristinati a HEAD** dopo l'esecuzione e dopo la riesecuzione. ID: **INC-018**.
- La build Debug di PlatformAdmin crea solo file in cartelle ignorate (`bin/`, `obj/`).
- `node tools/mysql84-release.cjs` è verde prima e dopo l'esecuzione.

## 7. Cosa non è stato verificato

- I 94 casi non eseguiti (IAM, JWT generation) e i 16 falliti, in particolare la CLI su 8.4.
- La suite con un processo e un database per metodo, come fa la CI. Qui i test dello stesso gruppo condividono un container.
- Il job `release` (`ER_MYSQL84_RELEASE_CONFIG`) e il job `windows-cli`, perché richiedono il runner Python e configurazioni CI.
- Lo stato della CI su `b53b10b`: `gh` non è installato, quindi va controllato a mano.
- Concorrenza e deadlock sotto carico reale, TLS `VerifyFull` e `caching_sha2_password` contro un server con TLS. Il container di test usa la configurazione di default della fixture.
