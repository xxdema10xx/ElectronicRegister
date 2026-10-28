using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using ElectronicRegisterAPI.Api.Controllers;
namespace ElectronicRegisterAPI.Api.Security;
internal sealed class CalendarControllerFeatureProvider(IConfiguration configuration) : IApplicationFeatureProvider<ControllerFeature>
{
    public void PopulateFeature(IEnumerable<ApplicationPart> parts,ControllerFeature feature)
    {
        if(!bool.TryParse(configuration["Multitenancy:InstitutionCalendar:Enabled"],out var enabled)||!enabled)
        {var c=feature.Controllers.SingleOrDefault(t=>t.AsType()==typeof(InstitutionCalendarController));if(c is not null)feature.Controllers.Remove(c);}
    }
}
