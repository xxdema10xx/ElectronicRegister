namespace ElectronicRegisterAPI.Domain.Enums;

// user.lifecycle_state. Closed accounts are never deleted or anonymized and can be reactivated.
public enum AccountLifecycleState
{
    Active,
    Closed
}
