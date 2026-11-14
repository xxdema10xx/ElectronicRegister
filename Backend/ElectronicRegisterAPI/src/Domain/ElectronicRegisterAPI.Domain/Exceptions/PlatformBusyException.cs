namespace ElectronicRegisterAPI.Domain.Exceptions;

// The creation lost against a concurrent writer (deadlock or lock wait timeout) on every
// automatic attempt. The whole transaction was rolled back; nothing was created or reserved.
// Deliberately not a MembershipConflictException: E1 ("person already in the institution") must
// stay distinguishable from "try again".
public sealed class PlatformBusyException() : Exception("Operazione non completata per concorrenza: riprovare.")
{
    public const string Code = "PlatformBusy";
}
