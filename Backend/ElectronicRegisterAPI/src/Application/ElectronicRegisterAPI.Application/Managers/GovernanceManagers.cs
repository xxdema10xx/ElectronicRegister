using ElectronicRegisterAPI.Application.Services;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Enums;
namespace ElectronicRegisterAPI.Application.Managers;

internal sealed class InstitutionGovernanceManager(IInstitutionGovernanceRepository repository) : IInstitutionGovernanceManager
{
    public Task<PlatformResult> ExecuteAsync(SignedPlatformOperation operation,CancellationToken ct=default)=>repository.ExecuteAsync(operation,ct);
}
internal sealed class MembershipAuditManager(InstitutionSchoolAuthorization authorization,IMembershipAuditRepository repository) : IMembershipAuditManager
{
    public async Task<IReadOnlyList<MembershipAuditEntry>> ListAsync(int page=1,int size=20,Guid? target=null,string? action=null,CancellationToken ct=default)
    {
        var actor=await authorization.AdminAsync(ct);
        if(page<1 || size is <1 or >100 || (long)(page-1)*size>int.MaxValue || target==Guid.Empty || action is not null && !Enum.GetNames<MembershipMutationAction>().Contains(action,StringComparer.Ordinal))throw new ArgumentException("Filtri audit non validi.");
        return await repository.ListAsync(actor,page,size,target,action,ct);
    }
    public async Task<MembershipAuditEntry> GetAsync(Guid eventId,CancellationToken ct=default)
    {
        var actor=await authorization.AdminAsync(ct);
        if(eventId==Guid.Empty)throw new ArgumentException();
        return await repository.GetAsync(actor,eventId,ct)??throw new KeyNotFoundException();
    }
}
