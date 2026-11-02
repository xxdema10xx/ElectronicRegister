"""Reviewed ownership and deterministic shards; no shared mutable fixtures."""
SHARDS = {'calendar': 1, 'formative': 1, 'email': 1, 'imports': 1,
          'iam': 1, 'onboarding': 1, 'tenants': 6}
WINDOWS_METHODS = {
    'ActualAdministrativeCliVerifiesSignedIntentAndUsesOnlyOwnerInstalledChannel',
    'Governance_ActualCliUsesTheDedicatedManagerWithoutRequiringNotificationDelivery',
    'PlatformCliRefusesAuthorityConfigurationReadableByEveryoneBeforeMutation',
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
    if cls.endswith(('.Mt2IdentityTests', '.InstitutionClassCrudTests')): return 'tenants'
    raise ValueError('Unassigned integration method: '+method)


def partition(methods, domain, index, count):
    if count != SHARDS[domain] or not 0 <= index < count:
        raise ValueError('Unreviewed shard configuration')
    ordered = sorted(set(m for m in methods if owner(m) == domain))
    if not ordered: raise ValueError('Empty domain discovery: '+domain)
    # Round robin over a sorted complete inventory balances method counts without
    # unstable Python hashes. Every method still gets its own process/database.
    return ordered, ordered[index::count]
