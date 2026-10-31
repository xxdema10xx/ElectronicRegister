using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Authorization;

// Calendar capabilities confer no IAM, email-policy, plan, exam or grade permissions.
public static class CalendarPermissions
{
    public static bool Can(IEnumerable<InstitutionRole> roles, CalendarCapability capability)
    {
        var set = roles.ToHashSet();
        if (!Enum.IsDefined(capability) || set.Any(r => !Enum.IsDefined(r)))
            return false;
        return capability switch
        {
            CalendarCapability.Manage or CalendarCapability.ReadAudit => set.Contains(InstitutionRole.InstitutionAdmin) || set.Contains(InstitutionRole.Staff),
            CalendarCapability.ReadOwnTeacher or CalendarCapability.ConfirmOwnDelivery => set.Contains(InstitutionRole.Teacher),
            CalendarCapability.ReadOwnStudent => set.Contains(InstitutionRole.Student),
            _ => false
        };
    }
}
