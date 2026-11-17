using System;
using System.Threading.Tasks;
using MySqlConnector;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

// INC-012: the technical MT3A legacy holding (institution MT3A_LEGACY_TEST + mt3a_legacy_scope)
// exists on the historical MySQL 5.7 test instance, where its setup provisions it, but not in
// the clean MySQL 8.4 bootstrap, which must not contain legacy data. Tests that exercise legacy
// writers or the holding itself create it here, only inside the attested disposable 8.4
// container and with the identity the 5.7 provisioning uses. Idempotent; on any database that is
// not an owned fixture container nothing is written (the 5.7 instance already has its holding).
// No production bootstrap, schema or trigger is involved.
internal static class LegacyTestHolding
{
    internal static readonly Guid Id = Guid.Parse("4d543341-0000-4000-8000-000000000001");

    internal static async Task EnsureAsync(string connectionString)
    {
        if (!Mysql84FixtureTarget.IsOwned(connectionString))
            return;
        await using var c = new MySqlConnection(connectionString);
        await c.OpenAsync();
        Assert.True(await Mysql84FixtureTarget.VerifyAsync(c), "Legacy holding is created only in the owned disposable container");
        await using var tx = await c.BeginTransactionAsync();
        await using (var institution = new MySqlCommand("INSERT INTO institution(id,code,name,status) SELECT @id,'MT3A_LEGACY_TEST','Technical legacy test holding','provisioning' " +
            "WHERE NOT EXISTS(SELECT 1 FROM institution WHERE id=@id OR code='MT3A_LEGACY_TEST')", c, tx))
        {
            institution.Parameters.AddWithValue("@id", Id);
            await institution.ExecuteNonQueryAsync();
        }
        await using (var scope = new MySqlCommand("INSERT INTO mt3a_legacy_scope(scope,institution_id) SELECT 'legacy',id FROM institution " +
            "WHERE code='MT3A_LEGACY_TEST' AND NOT EXISTS(SELECT 1 FROM mt3a_legacy_scope WHERE scope='legacy')", c, tx))
            await scope.ExecuteNonQueryAsync();
        await tx.CommitAsync();
    }
}
