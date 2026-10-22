using System.Collections.Generic;
using System.Linq;
using ElectronicRegisterAPI.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Xunit;
namespace ElectronicRegisterAPI.Tests.Integration;

internal static class MembershipHttpSurface
{
    internal const int AdminCount=8;
    internal const int MvcCount=163;
    internal const int IdentityCount=6;
    internal static void AssertRoutes(IEnumerable<RouteEndpoint> endpoints)
    {
        var members=endpoints.Where(e=>e.Metadata.GetMetadata<InstitutionMembershipAdminEndpointAttribute>() is not null).ToArray();
        Assert.Equal(AdminCount,members.Length);
        var reads=members.Where(e=>e.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.SequenceEqual(new[]{"GET"})).ToArray();
        Assert.Equal(4,reads.Length);
        Assert.Equal(new[]{"api/institution/admin/audit","api/institution/admin/audit/{eventId:guid}","api/institution/admin/members","api/institution/admin/members/{membershipId:guid}"},reads.Select(e=>e.RoutePattern.RawText).OrderBy(x=>x));
        Assert.All(reads,e=>Assert.Null(e.Metadata.GetMetadata<InstitutionMembershipWriteEndpointAttribute>()));
        var writes=members.Except(reads).ToArray();
        Assert.Equal(new[]{"api/institution/admin/invitations","api/institution/admin/invitations/{id:guid}/reissue","api/institution/admin/invitations/{id:guid}/revoke","api/institution/admin/members/{membershipId:guid}/mutations"},writes.Select(e=>e.RoutePattern.RawText).OrderBy(x=>x));
        Assert.All(writes,write=>{Assert.Equal(new[]{"POST"},write.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods);Assert.Single(write.Metadata.GetOrderedMetadata<InstitutionMembershipWriteEndpointAttribute>());});
        var accept=Assert.Single(endpoints.Where(e=>e.RoutePattern.RawText=="api/Auth/invitations/{id:guid}/accept"));
        Assert.NotNull(accept.Metadata.GetMetadata<InstitutionIdentityEndpointAttribute>());
        Assert.Equal(new[]{ElectronicRegisterAPI.Infrastructure.Security.InstitutionTokenContract.SelectionScheme},accept.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(a=>a.Policy));
        Assert.All(members,e=>
        {
            Assert.Equal(new[]{InstitutionMembershipAdminEndpointAttribute.PolicyName},e.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(a=>a.Policy));
            Assert.Null(e.Metadata.GetMetadata<IAllowAnonymous>());
        });
    }
}
