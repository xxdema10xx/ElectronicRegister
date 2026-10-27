using ElectronicRegisterAPI.Application.Services;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Application.Managers;

internal sealed class InstitutionEmailManager(InstitutionSchoolAuthorization access, IInstitutionEmailRepository repository,
    IInstitutionEmailGenerator generator, InstitutionEmailFeature feature) : IInstitutionEmailManager
{
    private async Task<TenantContext> Actor(CancellationToken ct)
    { if (!feature.Enabled) throw new UnauthorizedAccessException("Modulo email non abilitato."); return await access.RequireAsync(ct); }
    public async Task<IReadOnlyList<EmailPolicyVersion>> PoliciesAsync(CancellationToken ct) => await repository.PoliciesAsync(await Actor(ct),ct);
    public async Task<EmailPolicyVersion> PolicyAsync(EmailCategory category,long? revision,CancellationToken ct) => await repository.PolicyAsync(await Actor(ct),category,revision,ct);
    public async Task<EmailPolicyVersion> WritePolicyAsync(EmailCategory category,EmailPolicyWrite r,CancellationToken ct) => await repository.WritePolicyAsync(await Actor(ct),category,r,ct);
    public async Task<GeneratedInstitutionEmail> PreviewAsync(EmailPreviewRequest r,CancellationToken ct)
    {
        var actor=await Actor(ct); EmailTemplate template;
        if(r.Draft is not null)
        {
            if(!actor.Roles.Contains(InstitutionRole.InstitutionAdmin))throw new UnauthorizedAccessException();
            if(!Enum.IsDefined(r.Category))throw new ArgumentException();
            template=r.Draft;
        }
        else
        {
            var policy=await repository.PolicyAsync(actor,r.Category,r.PolicyRevision,ct);
            if(!actor.Roles.Contains(InstitutionRole.InstitutionAdmin)&&policy.State!="Published")throw new UnauthorizedAccessException();
            template=policy.Template;
        }
        // Pure interpolation: no availability checks, registry reads or writes.
        return generator.Preview(template,new(r.FirstName,r.LastName));
    }
    public async Task<EmailAssignment> ReserveAsync(EmailReserve r,CancellationToken ct) => await repository.ReserveAsync(await Actor(ct),r,ct);
    public async Task<EmailAssignment> CreatePersonAsync(EmailCreatePerson r,CancellationToken ct)
    {
        r=r with { FirstName=InstitutionSchoolAuthorization.Name(r.FirstName),LastName=InstitutionSchoolAuthorization.Name(r.LastName) };
        return await repository.CreatePersonAsync(await Actor(ct),r,ct);
    }
    public async Task<EmailAssignment> OverrideAsync(Guid id,EmailOverride r,CancellationToken ct) => await repository.OverrideAsync(await Actor(ct),id,r,ct);
    public async Task<EmailAssignment> RetireAsync(Guid id,EmailRetire r,CancellationToken ct) => await repository.RetireAsync(await Actor(ct),id,r,ct);
    public async Task<EmailAssignment> AssignmentAsync(Guid id,CancellationToken ct) => await repository.AssignmentAsync(await Actor(ct),id,ct);
    public async Task<EmailPage<EmailAssignment>> AssignmentsAsync(int page,int size,CancellationToken ct) => await repository.AssignmentsAsync(await Actor(ct),page,size,ct);
    public async Task<IReadOnlyList<EmailHistory>> HistoryAsync(Guid? assignment,EmailCategory? category,int page,int size,CancellationToken ct) => await repository.HistoryAsync(await Actor(ct),assignment,category,page,size,ct);
}
