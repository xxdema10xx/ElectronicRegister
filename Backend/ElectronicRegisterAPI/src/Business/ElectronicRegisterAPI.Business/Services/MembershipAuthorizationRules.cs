using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Business.Services;

public static class MembershipAuthorizationRules
{
    public static bool IsSelectable(MembershipAuthorization value) =>
        value.SecurityGeneration is >= 0 &&
        value.Status == MembershipStatus.Active && value.InstitutionStatus == InstitutionStatus.Active &&
        value.Roles.Count > 0 && value.Roles.All(Enum.IsDefined) && value.Roles.Distinct().Count() == value.Roles.Count;

    public static bool Matches(TenantContext token, MembershipAuthorization current) =>
        IsSelectable(current) && current.UserId == token.UserId && current.InstitutionId == token.InstitutionId &&
        current.MembershipId == token.MembershipId && token.SecurityGeneration.HasValue &&
        token.SecurityGeneration == current.SecurityGeneration && token.Roles.SetEquals(current.Roles);
}
