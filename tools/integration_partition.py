"""Reviewed ownership and deterministic shards; no shared mutable fixtures."""
import re
SHARDS = {'calendar': 1, 'formative': 1, 'email': 1, 'imports': 1,
          'iam': 2, 'iam-security': 4, 'onboarding': 1, 'tenants': 6}
WINDOWS_METHODS = {
    'ActualAdministrativeCliVerifiesSignedIntentAndUsesOnlyOwnerInstalledChannel',
    'Governance_ActualCliUsesTheDedicatedManagerWithoutRequiringNotificationDelivery',
    'PlatformCliRefusesAuthorityConfigurationReadableByEveryoneBeforeMutation',
}
# Extended suite (integration-full.yml): every remaining integration class, with its own
# shards so the main workflow and its final gate are unchanged. Classes are explicit:
# a new test class still fails closed until it is assigned to a domain.
EXTENDED_SHARDS = {'full': 14}
FULL_CLASSES = tuple('.' + name for name in (
    'AuditBootstrapTests', 'AuditMicrosoftTests', 'ConcurrencyIntegrationTests', 'DiagnosticPreflightTests',
    'ExamRegressionTests', 'ExamSecurityTests', 'ExamValidationTests', 'FinalGradeValidationTests',
    'FinalizeConcurrencyTests', 'GradeMappingTests', 'HistoricalExpiryBoundaryTests', 'HistoricalProfileAcquisitionTests',
    'IdentityDiagnosticPreflightTests', 'InstitutionCalendarFileReaderTests', 'InstitutionImportCsvReaderTests',
    'InstitutionImportXlsxReaderTests', 'LocalDatabasePortabilityTests', 'Mt1IntegrityTests', 'Mt2MicrosoftIdentityTests',
    'Mt3ProfileTests', 'Mt3aOwnershipTests', 'Mt3bSchoolTests', 'Mt3cHttpTests', 'Mt3cPreflightTests', 'Mt3dHttpTests',
    'Mt4aCourseStructureTests', 'Mt4bHttpTests', 'Mt5aMembershipAdminTests', 'Mt6RuntimeTests', 'Mt6WorkerTests',
    'Mysql84ForeignKeyMetadataTests', 'Mysql84GuidMetadataTests', 'Mysql84PurgeCapabilityTests', 'Mysql84ReleaseTests',
    'MysqlFixtureConfigurationTests', 'OpenScoreDoubleCountTests', 'Phase2B_AdditionalTests',
    'Phase2B_ConversionAndOverrideTests', 'Phase2C_ApiTests', 'Phase2C_VisibilityTests', 'Phase3_ConcurrencyTests',
    'Phase3_TransactionRollbackTests', 'Phase3_WorkerRecoveryTests', 'Phase4_ExamLifecycleTests', 'PrivateTestFilesTests',
    'QualityPasswordQuotaTests', 'QualitySafeLoggingTests', 'ReleaseLockObserverTests', 'RuntimeFeatureAvailabilityTests',
    'StagingCalendarFeatureTests', 'UnavailableFixtureGuardTests'))
# Classes of the extended suite that use the identity-only fixture collection.
FULL_IDENTITY_CLASSES = ('IdentityDiagnosticPreflightTests', 'Mt5aMembershipAdminTests', 'Mt6RuntimeTests')
IAM_SECURITY_CLASSES = ('.Mt5bJwtTests', '.Mt5bSecurityStateTests', '.Mt5dMembershipMutationTests',
                        '.Mysql84CapabilityScopeTests')
# Reviewed exclusions: discovered and reported as SKIPPED with ID and reason, never
# executed in CI. Any change must also update test_integration_partition.py.
EXCLUDED_METHODS = {
    'ElectronicRegisterAPI.Tests.Integration.Mt5bSecurityStateTests.Sql_FixtureGuardExcludesRealLegacyHolding':
        ('INC-012', 'Observes the real legacy holding MT3A_LEGACY_TEST, absent from the fresh MySQL 8.4 bootstrap; decision in Lotto 6'),
    'ElectronicRegisterAPI.Tests.Integration.Mt5dMembershipMutationTests.OfflineGraphClassifiesValidatedIamLedgerWithoutChangingData':
        ('INC-012', 'Validates the offline ownership graph of the legacy holding MT3A_LEGACY_TEST, absent from the fresh MySQL 8.4 bootstrap; decision in Lotto 6'),
    'ElectronicRegisterAPI.Tests.Integration.Mt1IntegrityTests.CanonicalExpandIsRerunnableAndDoesNotChangeLegacyData':
        ('INC-003', 'Expects MySQL 5.7 delete_rule RESTRICT; MySQL 8.4 reports NO ACTION for the same composite FKs'),
    'ElectronicRegisterAPI.Tests.Integration.Mt4aCourseStructureTests.InformationSchemaHasCompositeRestrictConstraints':
        ('INC-003', 'Expects MySQL 5.7 delete_rule RESTRICT; MySQL 8.4 reports NO ACTION for the same composite FKs'),
    'ElectronicRegisterAPI.Tests.Integration.Phase4_ExamLifecycleTests.Audit_IdentityProfileFaultIsAtomicAndCannotChangeDidacticProfile':
        ('INC-012', 'Legacy EF insert without explicit owner relies on the MT3A legacy holding, absent from the fresh MySQL 8.4 bootstrap'),
    'ElectronicRegisterAPI.Tests.Integration.HistoricalExpiryBoundaryTests.HistoricalOwnerRemainsTechnicalOnlyAndDeliveryIsIdempotent':
        ('INC-012', 'Reads mt3a_legacy_scope of the legacy holding, absent from the fresh MySQL 8.4 bootstrap'),
    'ElectronicRegisterAPI.Tests.Integration.Mt4aCourseStructureTests.ExistingBackfillIsIdempotentWithAttributedMt4Graph':
        ('INC-012', 'Requires the legacy holding; with one database per method it is always absent'),
    'ElectronicRegisterAPI.Tests.Integration.Mt4aCourseStructureTests.LegacyEfWritersStillReceiveTriggerDerivedOwners':
        ('INC-012', 'Legacy EF writers need the MT3A legacy holding; with one database per method it is always absent'),
    'ElectronicRegisterAPI.Tests.Integration.Mt3aOwnershipTests.AmbiguousLegacyAuditAbortsBackfillAndLeavesEveryRealCellUntouched':
        ('INC-012', 'Legacy backfill tooling needs a local MySQL 8.0 mysqldump.exe (Windows), absent on the Linux runner'),
    'ElectronicRegisterAPI.Tests.Integration.Mysql84ReleaseTests.PublishedReleaseAndEmbeddedOnboardingValidationRunOnExactServer':
        ('INC-017', 'Runs in the release (upgrade) job of the main workflow with ER_MYSQL84_RELEASE_CONFIG on the released server'),
    'ElectronicRegisterAPI.Tests.Integration.PrivateTestFilesTests.EachFixtureHasItsOwnPrivateDirectoryOutsideDatabaseDatadirs':
        ('INC-013', 'Windows-only ACL check of the private test-file infrastructure (PlatformNotSupportedException on the Linux runner)'),
    'ElectronicRegisterAPI.Tests.Integration.PrivateTestFilesTests.EveryoneReadIsActuallyAppliedAndCanBeRestoredOnTheNewFileOnly':
        ('INC-013', 'Windows-only ACL check of the private test-file infrastructure (PlatformNotSupportedException on the Linux runner)'),
}
# Reviewed theory-case exclusions (display names as listed by dotnet test --list-tests); the
# other cases of the same method still run. Any change must also update the locking test.
EXCLUDED_CASES = {
    'ElectronicRegisterAPI.Tests.Integration.Mt3dHttpTests.Release_WorkerDispatchRevalidatesTenantStateUnderLock(state: "holding-active", workerFirst: True)':
        ('INC-012', 'The holding-active state needs mt3a_legacy_scope of the legacy holding, absent from the fresh MySQL 8.4 bootstrap'),
    'ElectronicRegisterAPI.Tests.Integration.Mt3dHttpTests.Release_WorkerDispatchRevalidatesTenantStateUnderLock(state: "holding-active", workerFirst: False)':
        ('INC-012', 'The holding-active state needs mt3a_legacy_scope of the legacy holding, absent from the fresh MySQL 8.4 bootstrap'),
}


def owner(method):
    cls, name = method.rsplit('.', 1)
    if cls.endswith('.SqlLockEvidenceTests'): return 'calendar'
    if cls.endswith('.Mt5cOnboardingTests'):
        return 'windows-cli' if name in WINDOWS_METHODS else 'onboarding'
    if 'Calendar_' in name: return 'calendar'
    if 'EmailPolicy_' in name: return 'email'
    if 'BulkImport_' in name: return 'imports'
    if cls.endswith('.InstitutionFormativePlanTests'): return 'formative'
    if cls.endswith(('.Mt5eIamHttpTests', '.IdentityAccountTests', '.OwnerProvisioningTests', '.InstitutionAccountProvisioningTests')): return 'iam'
    if cls.endswith(IAM_SECURITY_CLASSES): return 'iam-security'
    if cls.endswith(('.Mt2IdentityTests', '.InstitutionClassCrudTests')): return 'tenants'
    if cls.endswith(FULL_CLASSES): return 'full'
    raise ValueError('Unassigned integration method: '+method)


def partition(methods, domain, index, count):
    if count != {**SHARDS, **EXTENDED_SHARDS}.get(domain) or not 0 <= index < count:
        raise ValueError('Unreviewed shard configuration')
    ordered = sorted(set(m for m in methods if owner(m) == domain and m not in EXCLUDED_METHODS))
    if not ordered: raise ValueError('Empty domain discovery: '+domain)
    # Round robin over a sorted complete inventory balances method counts without
    # unstable Python hashes. Every method still gets its own process/database.
    return ordered, ordered[index::count]


def exclusions(methods, domain):
    """Excluded methods of a domain; a stale entry (renamed or removed) fails closed."""
    expected = {m: v for m, v in EXCLUDED_METHODS.items() if owner(m) == domain}
    missing = sorted(set(expected) - set(methods))
    if missing: raise ValueError('Stale integration exclusion: '+', '.join(missing))
    return expected


def case_exclusions(cases, domain):
    """Excluded theory cases of a domain, by display name; a stale entry fails closed."""
    expected = {c: v for c, v in EXCLUDED_CASES.items() if owner(c.split('(', 1)[0]) == domain}
    missing = sorted(set(expected) - set(cases))
    if missing: raise ValueError('Stale integration case exclusion: '+', '.join(missing))
    return expected


def case_filters(method_cases, excluded):
    """VSTest clauses excluding exactly the reviewed cases of one method.

    dotnet test hands --filter to MSBuild, which rejects commas, quotes and parentheses even
    when escaped, so full display names cannot be used. Each excluded case is selected by a
    plain argument token, accepted only if every discovered case of the method containing
    that token is itself excluded (fail closed otherwise).
    """
    clauses = []
    for case in sorted(excluded):
        arguments = case.split('(', 1)[1] if '(' in case else ''
        tokens = [t for t in re.split(r'[^A-Za-z0-9_.-]+', arguments) if t]
        chosen = next((t for t in tokens if all(c in excluded for c in method_cases if t in c)), None)
        if chosen is None:
            raise ValueError('Excluded case cannot be selected without also excluding others: ' + case)
        clause = 'DisplayName!~' + chosen
        if clause not in clauses:
            clauses.append(clause)
    covered = [c for c in method_cases if any(clause.split('!~', 1)[1] in c for clause in clauses)]
    if sorted(covered) != sorted(excluded):
        raise ValueError('Case exclusion filter does not match exactly the reviewed cases')
    return clauses
