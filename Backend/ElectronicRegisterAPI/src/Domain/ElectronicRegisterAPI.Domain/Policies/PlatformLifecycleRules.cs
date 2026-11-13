using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Policies;

// Pure account lifecycle rules (rule version 1). Active means an active membership in an
// active institution. Closing always requires no active membership and no pending personal
// invitation; any Student membership additionally requires the configured period, counted
// from the earliest JoinedAt of a Student membership of any status, to have elapsed.
public static class PlatformLifecycleRules
{
    public static bool IsActive(LifecycleMembership membership) =>
        membership.Status == MembershipStatus.Active && membership.InstitutionStatus == InstitutionStatus.Active;

    public static LifecycleDecision Evaluate(LifecycleSnapshot snapshot, DateTime nowUtc, int studentClosureAfterMonths)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (nowUtc.Kind != DateTimeKind.Utc || studentClosureAfterMonths is < 1 or > 240)
            throw new ArgumentException("Parametri del ciclo di vita non validi.");
        var active = snapshot.Memberships.Any(IsActive);
        if (snapshot.State == AccountLifecycleState.Closed)
            return active ? new(LifecycleAction.Reactivate, LifecycleRuleCodes.ActiveMembershipFound) : new(LifecycleAction.Keep, null);
        if (active || snapshot.HasPendingInvitation)
            return new(LifecycleAction.Keep, null);
        var studentStarts = snapshot.Memberships.Where(m => m.Roles.Contains(InstitutionRole.Student)).Select(m => m.JoinedAtUtc).ToArray();
        if (studentStarts.Length == 0)
            return new(LifecycleAction.Close, LifecycleRuleCodes.NoActiveMembership);
        var deadline = DateTime.SpecifyKind(studentStarts.Min(), DateTimeKind.Utc).AddMonths(studentClosureAfterMonths);
        return nowUtc >= deadline ? new(LifecycleAction.Close, LifecycleRuleCodes.StudentPeriodElapsed) : new(LifecycleAction.Keep, null, deadline);
    }
}
