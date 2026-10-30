using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

public sealed partial class InstitutionClassCrudTests
{
    private async Task AsCrudAccount(Func<Task> test, bool tableSelectOnly = false)
    {
        var settings = new MySqlConnectionStringBuilder(fixture.ConnectionString);
        var user = "class_" + Guid.NewGuid().ToString("N")[..20];
        var password = Guid.NewGuid().ToString("N");
        await Execute($"CREATE USER '{user}'@'%' IDENTIFIED BY '{password}'");
        try
        {
            await Execute($"GRANT SELECT,INSERT,UPDATE,DELETE ON `{settings.Database}`.* TO '{user}'@'%'");
            if (tableSelectOnly)
            {
                // Identical data access, deliberately lacking a schema visibility
                // guarantee: table grants must never be mistaken for a full inventory.
                await using var root = new MySqlConnection(fixture.ConnectionString);
                await root.OpenAsync();
                var tables = new System.Collections.Generic.List<string>();
                await using (var cmd = new MySqlCommand("SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE()", root))
                await using (var reader = await cmd.ExecuteReaderAsync())
                    while (await reader.ReadAsync()) tables.Add(reader.GetString(0));
                foreach (var table in tables) await Execute($"GRANT SELECT ON `{settings.Database}`.`{table}` TO '{user}'@'%'");
                await Execute($"REVOKE SELECT ON `{settings.Database}`.* FROM '{user}'@'%'");
            }
            settings.UserID = user;
            settings.Password = password;
            settings.Pooling = false;
            await using (var c = new MySqlConnection(settings.ConnectionString))
            {
                await c.OpenAsync();
                await using var cmd = new MySqlCommand("SELECT COUNT(*) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE()", c);
                Assert.Equal(0L, Convert.ToInt64(await cmd.ExecuteScalarAsync()));
                cmd.CommandText = "SHOW GRANTS";
                await using var reader = await cmd.ExecuteReaderAsync();
                var grants = new System.Collections.Generic.List<string>();
                while (await reader.ReadAsync()) grants.Add(reader.GetString(0));
                if (!tableSelectOnly)
                {
                    Assert.Equal(2, grants.Count);
                    Assert.Single(grants.Where(g => g.StartsWith("GRANT SELECT, INSERT, UPDATE, DELETE ON ", StringComparison.Ordinal)));
                }
            }
            var original = _host;
            await using var app = await Mt3cHosting.ProgramHost.StartAsync(settings.ConnectionString, _a, Array.Empty<Guid>(), enabled: false, school: true, course: true, exam: true);
            _host = app;
            try { await test(); }
            finally { _host = original; }
        }
        finally { await Execute($"DROP USER '{user}'@'%'"); }
    }

    [Fact]
    public Task LeastPrivilegeEmptyDeletePreservesPermanentAudit() => AsCrudAccount(CreateListDetailRenameAndDeleteEmptyClassKeepPermanentAudit);

    [Theory]
    [InlineData("student")]
    [InlineData("class-subject")]
    public Task LeastPrivilegeDeletePreservesSchoolDependencies(string kind) => AsCrudAccount(() => DeleteNeverCascadesOrDisconnectsSchoolData(kind));

    [Fact]
    public Task LeastPrivilegeDeletePreservesEditionAndHistory() => AsCrudAccount(DeleteWithEditionAndAdministrativeHistoryPreservesEveryReference);

    [Fact]
    public Task LeastPrivilegeDeletePreservesExamsSubmissionsAndGrades() => AsCrudAccount(ClassWithExamsSubmissionsAndGradesCannotBeDeletedOrMutatedIndirectly);

    [Fact]
    public Task IncompleteMetadataPrivilegeContractFailsClosed() => AsCrudAccount(async () =>
    {
        var id = await CreateClass();
        await Expect(await Request(Classes + "/" + id, await Access(), method: "DELETE"), HttpStatusCode.InternalServerError);
        Assert.Equal(id, (await Detail(id)).Id);
    }, tableSelectOnly: true);

    [Fact]
    public Task LeastPrivilegeMissingCalendarHistoryTableFailsClosed() => AsCrudAccount(async () =>
    {
        var id = await CreateClass();
        var table = "calendar_history_probe_" + Guid.NewGuid().ToString("N")[..12];
        await Execute($"RENAME TABLE institution_calendar_event TO `{table}`");
        try
        {
            await Expect(await Request(Classes + "/" + id, await Access(), method: "DELETE"), HttpStatusCode.InternalServerError);
            Assert.Equal(id, (await Detail(id)).Id);
        }
        finally { await Execute($"RENAME TABLE `{table}` TO institution_calendar_event"); }
    });

    [Fact]
    public Task LeastPrivilegeUnknownCascadeForeignKeyBlocksEvenEmptyDelete() => AsCrudAccount(async () =>
    {
        var id = await CreateClass();
        var table = "class_probe_" + Guid.NewGuid().ToString("N")[..16];
        await Execute($"CREATE TABLE `{table}` (class_id CHAR(36) CHARACTER SET ascii, CONSTRAINT `{table}_fk` FOREIGN KEY(class_id) REFERENCES class(id) ON DELETE CASCADE) ENGINE=InnoDB");
        try
        {
            await Expect(await Request(Classes + "/" + id, await Access(), method: "DELETE"), HttpStatusCode.Conflict);
            Assert.Equal(id, (await Detail(id)).Id);
            await Db(async db => Assert.False(await db.Set<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.InstitutionClassEvent>().AnyAsync(e => e.ClassId == id && e.Action == "class.deleted")));
        }
        finally { await Execute($"DROP TABLE `{table}`"); }
    });

    [Fact]
    public Task LeastPrivilegeMissingCalendarForeignKeyFailsClosed() => AsCrudAccount(async () =>
    {
        var id = await CreateClass();
        await Execute("ALTER TABLE institution_calendar_period DROP FOREIGN KEY fk_cal_period_class");
        try
        {
            await Expect(await Request(Classes + "/" + id, await Access(), method: "DELETE"), HttpStatusCode.InternalServerError);
            Assert.Equal(id, (await Detail(id)).Id);
        }
        finally { await Execute("ALTER TABLE institution_calendar_period ADD CONSTRAINT fk_cal_period_class FOREIGN KEY(institution_id,class_id) REFERENCES class(institution_id,id)"); }
    });
}
