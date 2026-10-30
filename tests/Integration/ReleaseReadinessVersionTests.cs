using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

public sealed partial class InstitutionClassCrudTests
{
    [Fact]
    public async Task Release_ReadinessRequiresGateRevisionMarker()
    {
        await Expect(await Request("/health/ready"),HttpStatusCode.OK);
        await Execute("DELETE FROM er_schema_release WHERE version='er-quality-1-closure-1'");
        try{await Expect(await Request("/health/live"),HttpStatusCode.OK);await Expect(await Request("/health/ready"),HttpStatusCode.ServiceUnavailable);}
        finally{await Execute("INSERT INTO er_schema_release(version) VALUES('er-quality-1-closure-1')");}
        await Expect(await Request("/health/ready"),HttpStatusCode.OK);
    }
}
