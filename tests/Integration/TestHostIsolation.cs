using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Linq;

namespace ElectronicRegisterAPI.Tests.Integration;

internal static class TestHostIsolation
{
    internal static void Configure(IServiceCollection services)
    {
        // Test hosts never read/write the developer's Windows DPAPI key ring.
        // Institution JWT signing and all authentication policies remain unchanged.
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        // The framework warm-up service otherwise reads the OS key ring even
        // when the actual data protection provider is ephemeral. Keep business
        // workers registered, including ExpiredExamSubmissionWorker.
        foreach(var descriptor in services.Where(d=>d.ServiceType==typeof(IHostedService) &&
            d.ImplementationType?.Name=="DataProtectionHostedService" &&
            d.ImplementationType.Assembly.GetName().Name=="Microsoft.AspNetCore.DataProtection").ToArray())
            services.Remove(descriptor);
    }
}
