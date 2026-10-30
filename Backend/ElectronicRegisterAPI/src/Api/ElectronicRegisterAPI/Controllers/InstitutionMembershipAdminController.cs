using ElectronicRegisterAPI.Api.Security;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Api.Contracts;
using ElectronicRegisterAPI.Api.ExceptionHandling;
using Microsoft.AspNetCore.Mvc;

namespace ElectronicRegisterAPI.Api.Controllers;

[ApiController, Route("api/institution/admin/members"), InstitutionMembershipAdminEndpoint]
public sealed class InstitutionMembershipAdminController(IInstitutionMembershipAdminManager manager) : ControllerBase
{
    [HttpGet("{membershipId:guid}/profile-acquisitions")]
    public async Task<IActionResult> Acquisitions(
        Guid membershipId,
        CancellationToken ct,
        int page = 1,
        int size = 20,
        string? status = null
    ) => Ok(await manager.AcquisitionsAsync(membershipId, page, size, Filter("status", status), ct));
    [HttpPost("{membershipId:guid}/profile-acquisitions"), InstitutionMembershipWriteEndpoint, MembershipApiErrors]
    public async Task<IActionResult> AcquireProfile(Guid membershipId, ElectronicRegisterAPI.Domain.DTOs.Membership.AcquireHistoricalProfile request, CancellationToken ct) => Ok(await manager.AcquireProfileAsync(membershipId, request, ct));
    [HttpPost("{membershipId:guid}/mutations"), InstitutionMembershipWriteEndpoint, MembershipApiErrors]
    public async Task<IActionResult> Mutate(Guid membershipId, [FromBody] MembershipMutationRequest request, CancellationToken ct) => Ok(MembershipMutationResponse.From(await manager.MutateAsync(request.ToMutation(membershipId), ct)));
    [HttpGet]
    public async Task<IActionResult> List(
        CancellationToken ct,
        int pageNumber = 1,
        int pageSize = 20,
        string? status = null,
        string? role = null,
        string? search = null
    ) => Ok(await manager.ListAsync(pageNumber, pageSize, Filter("status", status), Filter("role", role), Filter("search", search), ct));
    // MVC normalizes whitespace strings to null. Present-but-empty filters are invalid,
    // while absent filters mean no restriction.
    private string? Filter(string name, string? value) => value ?? (Request.Query.ContainsKey(name) ? "" : null);
    [HttpGet("{membershipId:guid}")]
    public async Task<IActionResult> Detail(Guid membershipId, CancellationToken ct) => Ok(await manager.GetAsync(membershipId, ct));
}
