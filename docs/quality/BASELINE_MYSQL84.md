# Baseline test su MySQL 8.4.13

| Voce | Valore |
|---|---|
| Data | 2026-11-05, esecuzione 12:54–13:22 (UTC+1) più una riesecuzione unica dei falliti |
| Branch / commit | `refactor-database-entities` @ `b53b10b` (codice identico a `03bb1be`). Working tree pulito a parte i documenti nuovi in `docs/` |
| Target | MySQL **8.4.13**, unico target di produzione secondo D1 ([DATABASE_TARGET_MYSQL84.md](../architecture/DATABASE_TARGET_MYSQL84.md)) |
| Esito Unit | **598 / 598 superati** |
| Esito Integration | **1676 casi: 1566 superati, 16 falliti durante l'esecuzione, 94 non eseguiti** perché bloccati in inizializzazione (`InitializeAsync` della classe, categoria A). Il report TRX conta questi 94 come "Failed", ma il corpo del test non è mai partito. Nella riesecuzione unica: 108 casi di nuovo non superati (94 non eseguiti + 14 falliti), 2 superati (dipendenza dall'ordine) |
| Aggiornamento Lotto 0B (§8) | Stesso giorno, working tree con le modifiche di harness e CI del Lotto 0B: **1679 casi, 1664 superati, 15 falliti, 0 non eseguiti**. INC-017 risolto in locale, INC-018 risolto, nessun nuovo ID |
| Bug reali del codice di produzione | **Nessuno tra i test arrivati a verificare il comportamento**: i 1566 superati e i 9 falliti per schema o versione (categorie B e C, causa identificata in §4). **Nessuna conclusione** per i 94 casi non eseguiti e per i 7 falliti per ambiente, che non hanno verificato il comportamento |

## 1. Ambiente e versioni

| Componente | Versione |
|---|---|
| .NET SDK / runtime | 10.0.401 (rispetta `global.json`) / 10.0.12 |
| Node | 24.20.0 |
| Python | **non installato** alla baseline (motivo di 2 fallimenti, vedi §4); 3.12.10 installato per il Lotto 0B (§8) |
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

**Confermato su MySQL 8.4, in locale, per i percorsi HTTP, i repository, le scritture applicative, il worker e, dopo il Lotto 0B (§8), anche per la revoca dei token basata sulla security generation, le mutazioni di ruolo e l'isolamento delle mutazioni IAM.** La conferma in CI arriverà solo con la prima run verde del dominio `iam-security`. Resta non verificata su 8.4 la CLI di piattaforma.

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
| **Revoca JWT per security generation** (Lotto 0B) | `Mt5bJwtTests` 23/23: `RestoringRolesOrStatusNeverRevivesOldTokenAndDoesNotRevokeB`, `SignedTenantMismatchAndStaleOrFutureGenerationAreForbidden`, `InvalidGenerationClaimsAreRejectedBeforeAuthorization` (13 casi), `RemovedMembershipAndGenerationBearingSelectionTicketFailClosed`, `MissingStateDeniesExistingTokenAndNewSelection`, `RealProgramIssuesPersistedGenerationAndNeverGlobalAdminPrivilege` |
| **Security generation nel DB** (Lotto 0B) | `Mt5bSecurityStateTests` 39/40: `Sql_RoleAddRemoveRestoreNeverRestoresGeneration`, `Sql_ConcurrentChangesHaveNoLostOrDuplicateIncrements`, `Sql_RollbackPreservesGenerationAndBothTenants`, `Sql_CompositeForeignKeyRejectsWrongOwnerAndOrphanAfterPrivilegedFixturePurge`, `ScopedReaderUsesPersistedGenerationAndFailsClosedOutsideOwnerOrWhenAbsent`, `TechnicalWritersCannotMutateOperationalInstitutionsEvenForGlobalAdmin`, `Sql_FixtureGuardRequiresExplicitOwnerAndSyntheticCode` |
| **Mutazioni IAM ed escalation** (Lotto 0B) | `Mt5dMembershipMutationTests` 31/32: `EventIdentityIsTenantScopedAndCannotAffectOtherTenant`, `RealJwtIsPermanentlyRevokedByIamAndOtherTenantRemainsValid`, `AuthorizationIsCurrentScopedAndNeverUsesGlobalUserAdmin` (5 casi), `ReplayReauthorizesRevokedActorUnderMutex`, `SoleAdminCannotRemoveTheirOwnAuthority`, `ForgedOrIncompleteInternalContextCannotAuthorizeWrite`, `OperationalIamCannotWriteIntoNonActiveInstitution` |

L'isolamento IAM via HTTP è coperto da `Mt5eIamHttpTests` 53/53 e `Mt5aMembershipAdminTests` 36/36.

**IAM e revoca JWT: confermati su 8.4 in locale.** Nessun test su revoca dei token, escalation di privilegi o isolamento tra istituti fallisce, e non sono stati aperti nuovi ID. I 2 casi non superati delle classi del Lotto 0B (`Sql_FixtureGuardExcludesRealLegacyHolding`, `OfflineGraphClassifiesValidatedIamLedgerWithoutChangingData`) osservano l'holding legacy `MT3A_LEGACY_TEST`, assente nel bootstrap 8.4 pulito. Sono legati a INC-012 ed esclusi dalla CI con ID e motivo.

**Non verificato su 8.4:**
- la conferma in CI, che arriverà solo con la prima run verde di `iam-security`;
- la CLI di piattaforma (D4);
- i casi legati all'holding legacy (INC-012).

## 6. Effetti collaterali osservati

- **(Risolto nel Lotto 0B, §8.)** **L'esecuzione dei test riscriveva 6 file versionati** in `tests/Integration/TestResults/` (`mt3c/http-query-observations.json`, `mt3d/http-query-observations.json`, `mt3d/write-statements.sql.txt`, `mt3e/worker-query-observations.json`, `mt4b/course-sql.json`, `mt5a/directory-sql.json`). Contengono solo osservazioni SQL, senza percorsi, host né password. Li ho **ripristinati a HEAD** dopo l'esecuzione e dopo la riesecuzione. ID: **INC-018**.
- La build Debug di PlatformAdmin crea solo file in cartelle ignorate (`bin/`, `obj/`).
- `node tools/mysql84-release.cjs` è verde prima e dopo l'esecuzione.

## 7. Cosa non è stato verificato

- I 94 casi non eseguiti alla baseline sono ora eseguiti (§8). Restano i falliti elencati in §8, in particolare la CLI su 8.4.
- La conferma in CI del dominio `iam-security`, che arriverà solo con la prima run verde.
- La suite con un processo e un database per metodo, come fa la CI. Qui i test dello stesso gruppo condividono un container. Per `iam-security` è stato eseguito in locale lo shard 2 di 4 con l'orchestratore della CI (§8).
- Il job `release` (`ER_MYSQL84_RELEASE_CONFIG`) e il job `windows-cli`, perché richiedono il runner Python e configurazioni CI.
- Lo stato della CI su `b53b10b`: `gh` non è installato, quindi va controllato a mano.
- Concorrenza e deadlock sotto carico reale, TLS `VerifyFull` e `caching_sha2_password` contro un server con TLS. Il container di test usa la configurazione di default della fixture.

## 8. Aggiornamento Lotto 0B: INC-017 e INC-018

Eseguito il 2026-11-05 sul working tree con le modifiche del Lotto 0B, su container MySQL 8.4.13 usa e getta creati dalla fixture. Il container `mysql84-local` non è stato toccato. Nessuna modifica al codice di produzione e nessun seed aggiunto.

**Modifiche dell'harness**
- Sul ramo 8.4, `Open()` delle tre classi attesta il server e imposta la capability di sessione (`tests/Integration/Mysql84OwnedSession.cs`). L'asserzione sulla porta 13306 resta sul ramo 5.7.
- `Mysql84CapabilityScopeTests` dimostra due cose: il reset del pool azzera la capability sulla stessa sessione fisica, e il percorso applicativo non la vede mai.
- Il test di guard è diviso in due:
  - `Sql_FixtureGuardRequiresExplicitOwnerAndSyntheticCode` conserva i due rifiuti 1644 e aggiunge un controllo positivo: con owner e codice corretti, la cancellazione riesce;
  - `Sql_FixtureGuardExcludesRealLegacyHolding` contiene solo l'osservazione sull'holding (INC-012). Sul ramo 8.4 usa per la regex l'espressione già rivista dalla release (`tools/mysql84-remediation.cjs`), perché 8.4 rifiuta `BINARY … REGEXP`.
- **INC-018:** le evidenze vanno in `bin/…/test-evidence/<area>` (`tests/Integration/TestEvidence.cs`).

**Classi del Lotto 0B (INC-017)**

| Classe | Eseguite separatamente: eseguiti / superati / falliti | Insieme, stesso container: eseguiti / superati / falliti |
|---|---|---|
| `Mt5bJwtTests` | 23 / 23 / 0 | 23 / 23 / 0 |
| `Mt5bSecurityStateTests` | 40 / 39 / 1 | 40 / 39 / 1 |
| `Mt5dMembershipMutationTests` | 32 / 31 / 1 | 32 / 31 / 1 |
| `Mysql84CapabilityScopeTests` | 2 / 2 / 0 | 2 / 2 / 0 |
| **Totale** | **97 / 95 / 2** | **97 / 95 / 2** |

I 2 falliti sono:
- `Sql_FixtureGuardExcludesRealLegacyHolding`: `Expected: 1, Actual: 0`, perché l'holding è assente;
- `OfflineGraphClassifiesValidatedIamLedgerWithoutChangingData`: `NullReferenceException` alla riga 286, perché manca l'id dell'holding.

Entrambi sono legati a INC-012 ed esclusi dalla CI.

**CI `iam-security`**
- **Configurazione:** 4 shard, 52 metodi (13 per shard), più 2 esclusi con ID e motivo (`EXCLUDED_METHODS`).
- **Durata stimata, shard peggiore:** 1775 s; 3385 s se scadono entrambi i suoi metodi con timeout da 900 s. `SUITE_TIMEOUT` è 3600 s.
- **Prova locale con l'orchestratore della CI** (`ci_diagnostic.py --integration iam-security --shard 2 --shards 4`): PASS in 1014 s. Preflight `identity` superato, 13 metodi su 13, casi delle theory completi.

**Suite completa (un solo processo)**
- Integration: **1679 casi, 1664 superati, 15 falliti, 0 non eseguiti**. Rispetto alla baseline ci sono 3 casi in più: la divisione del test di guard (+1) e `Mysql84CapabilityScopeTests` (+2).
- `git status` dopo la suite: solo le modifiche volute. `tests/Integration/TestResults/` resta invariata.

| Falliti | Casi | Categoria | ID |
|---|---|---|---|
| B1, B2 (FK `RESTRICT` contro `NO ACTION`) | 2 | Schema o versione | INC-003 |
| C1, C2, C3 (holding legacy assente) | 5 | Schema o versione | INC-012 |
| `Sql_FixtureGuardExcludesRealLegacyHolding`, `OfflineGraphClassifiesValidatedIamLedgerWithoutChangingData` | 2 | Schema o versione (holding legacy) | INC-012 |
| D2 (`ER_MYSQL84_RELEASE_CONFIG`), D3 (`mysqldump.exe`), D4 (CLI ×3) | 5 | Ambiente | INC-012, INC-008, INC-013 |
| `Mt5aMembershipAdminTests.InvalidFiltersReturn400(query: "status=")` | 1 | **Test instabile**: `HttpClient.Timeout` di 15 s sotto il carico della suite completa. Nella riesecuzione unica la theory è passata 12 casi su 12 | — |

**Cambiamenti rispetto alla baseline**
- C4 e C5 (`Mt4a`, dipendenza dall'ordine) sono passati.
- D1 (`StagingCalendarFeatureTests`) è passato 2 su 2 con Python 3.12.10.

**Unit.** In questa sessione non sono stati eseguiti. Il criterio di controllo delle applicazioni di Windows ha bloccato il caricamento di `Unit.Tests.dll` (`0x800711C7`), anche dopo una ricompilazione. Il Lotto 0B non modifica `tests/Unit` né il codice di produzione. Il dato di riferimento resta 598/598 (§1) e la CI esegue il check `unit`.
