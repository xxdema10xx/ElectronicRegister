using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using ElectronicRegisterAPI.Api.Controllers;
namespace ElectronicRegisterAPI.Api.Security;

// Closed rollout has no HTTP surface. Enabling requires restart; disabling at runtime
// is also enforced in manager/repository before every access and commit.
internal sealed class FormativePlanControllerFeatureProvider(IConfiguration configuration) : IApplicationFeatureProvider<ControllerFeature>
{
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        if (!bool.TryParse(configuration["Multitenancy:FormativePlans:Enabled"],out var enabled) || !enabled)
        {
            var controller=feature.Controllers.SingleOrDefault(t=>t.AsType()==typeof(InstitutionFormativePlanController));
            if(controller is not null)feature.Controllers.Remove(controller);
        }
    }
}
