using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Exceptions;
namespace ElectronicRegisterAPI.Domain.Models;

public static class MembershipMutationRules
{
    public static void Validate(MembershipMutation request)
    {
        if(request.MembershipId==Guid.Empty || request.EventId==Guid.Empty || request.ExpectedRevision<0 ||
            !Enum.IsDefined(request.Action) || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length>500 ||
            request.Reason.Any(char.IsControl) ||
            (request.Action is MembershipMutationAction.AddRole or MembershipMutationAction.RemoveRole
                ? request.Role is null || !Enum.IsDefined(request.Role.Value) : request.Role is not null))
            throw new ArgumentException("Mutazione membership non valida.");
    }

    public static void RequireLastAdmin(bool removesActiveAdmin, long otherActiveAdmins)
    {
        if(removesActiveAdmin && otherActiveAdmins<1) throw new MembershipConflictException();
    }

    public static void RequireTransition(MembershipMutation request, MembershipStatus status,
        IReadOnlyCollection<InstitutionRole> roles)
    {
        var valid=request.Action switch
        {
            MembershipMutationAction.AddRole => (status is MembershipStatus.Active or MembershipStatus.Suspended) && !roles.Contains(request.Role!.Value),
            MembershipMutationAction.RemoveRole => (status is MembershipStatus.Active or MembershipStatus.Suspended) && roles.Contains(request.Role!.Value),
            MembershipMutationAction.Suspend => status==MembershipStatus.Active,
            MembershipMutationAction.Reactivate => status==MembershipStatus.Suspended,
            _ => false
        };
        if(!valid) throw new MembershipConflictException();
    }
}
