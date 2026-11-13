using System;
using System.Net;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.Enums;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

// D-C through platform governance: reactivating a suspended institution reopens, in the
// same transaction, the Closed accounts whose membership there is active again.
public sealed partial class Mt5cOnboardingTests
{
    [Fact]
    public async Task Governance_InstitutionReactivationReopensClosedAccountsOfItsActiveMembers()
    {
        ConfigureGovernance();
        var a = await Active();
        await Govern(GovernanceOp(a.P.InstitutionId, PlatformAction.Suspend));
        // The account is Closed while its only institution is suspended (state written as
        // the lifecycle job would: state, identity generation and ledger event together).
        await Exec("UPDATE `user` SET lifecycle_state='Closed',closed_at_utc=UTC_TIMESTAMP(6) WHERE id=@p0", users[0]);
        await Exec("INSERT INTO identity_account_state(user_id,generation) VALUES(@p0,0) ON DUPLICATE KEY UPDATE user_id=user_id", users[0]);
        await Exec("UPDATE identity_account_state SET generation=generation+1 WHERE user_id=@p0", users[0]);
        await Exec("INSERT INTO identity_lifecycle_event(id,user_id,transition,rule_code,rule_version,security_generation,occurred_at_utc) SELECT @p1,user_id,'Closed','NoActiveMembership',1,generation,UTC_TIMESTAMP(6) FROM identity_account_state WHERE user_id=@p0", users[0], Guid.NewGuid().ToString());
        var closedGeneration = await Count("SELECT generation FROM identity_account_state WHERE user_id=@p0", users[0]);
        await Govern(GovernanceOp(a.P.InstitutionId, PlatformAction.Reactivate));
        Assert.Equal(1, await Count("SELECT COUNT(*) FROM `user` WHERE id=@p0 AND lifecycle_state='Active' AND reactivated_at_utc IS NOT NULL", users[0]));
        Assert.Equal(1, await Count("SELECT COUNT(*) FROM identity_lifecycle_event WHERE user_id=@p0 AND transition='Reactivated' AND rule_code='InstitutionReactivated' AND security_generation=@p1", users[0], closedGeneration + 1));
        // Password unchanged: the member logs in again and enters the reactivated institution.
        await Expect(await Http("/api/Auth/context", await Access(a.P.InstitutionId, a.Member)), HttpStatusCode.OK);
    }
}
