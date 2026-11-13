namespace ElectronicRegisterAPI.Domain.Policies;

// identity_lifecycle_event.rule_code values, also enforced by lifecycle_event_insert.
public static class LifecycleRuleCodes
{
    public const int Version = 1;
    public const string Closed = "Closed";
    public const string Reactivated = "Reactivated";
    public const string NoActiveMembership = "NoActiveMembership";
    public const string StudentPeriodElapsed = "StudentPeriodElapsed";
    public const string AccountRecreated = "AccountRecreated";
    public const string MembershipReactivated = "MembershipReactivated";
    public const string InstitutionReactivated = "InstitutionReactivated";
    public const string ActiveMembershipFound = "ActiveMembershipFound";
}
