"""Reviewed ownership and deterministic shards; no shared mutable fixtures."""
SHARDS = {'calendar': 1, 'formative': 1, 'email': 1, 'imports': 1,
          'iam': 1, 'iam-security': 4, 'onboarding': 1, 'tenants': 6}
WINDOWS_METHODS = {
    'ActualAdministrativeCliVerifiesSignedIntentAndUsesOnlyOwnerInstalledChannel',
    'Governance_ActualCliUsesTheDedicatedManagerWithoutRequiringNotificationDelivery',
    'PlatformCliRefusesAuthorityConfigurationReadableByEveryoneBeforeMutation',
}
IAM_SECURITY_CLASSES = ('.Mt5bJwtTests', '.Mt5bSecurityStateTests', '.Mt5dMembershipMutationTests',
                        '.Mysql84CapabilityScopeTests')
# Reviewed exclusions: discovered and reported as SKIPPED with ID and reason, never
# executed in CI. Any change must also update test_integration_partition.py.
EXCLUDED_METHODS = {
    'ElectronicRegisterAPI.Tests.Integration.Mt5bSecurityStateTests.Sql_FixtureGuardExcludesRealLegacyHolding':
        ('INC-012', 'Observes the real legacy holding MT3A_LEGACY_TEST, absent from the fresh MySQL 8.4 bootstrap; decision in Lotto 6'),
    'ElectronicRegisterAPI.Tests.Integration.Mt5dMembershipMutationTests.OfflineGraphClassifiesValidatedIamLedgerWithoutChangingData':
        ('INC-012', 'Validates the offline ownership graph of the legacy holding MT3A_LEGACY_TEST, absent from the fresh MySQL 8.4 bootstrap; decision in Lotto 6'),
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
    if cls.endswith(('.Mt5eIamHttpTests', '.IdentityAccountTests')): return 'iam'
    if cls.endswith(IAM_SECURITY_CLASSES): return 'iam-security'
    if cls.endswith(('.Mt2IdentityTests', '.InstitutionClassCrudTests')): return 'tenants'
    raise ValueError('Unassigned integration method: '+method)


def partition(methods, domain, index, count):
    if count != SHARDS[domain] or not 0 <= index < count:
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
