using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using ElectronicRegisterAPI.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;
using Xunit.Abstractions;

namespace ElectronicRegisterAPI.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public sealed class Mt1IntegrityTests(IntegrationFixture fixture, ITestOutputHelper output) : IAsyncLifetime
{
    private readonly List<Guid> _institutions = new();
    private readonly Guid _user = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);
        Assert.Equal("electronic_register_test", new MySqlConnectionStringBuilder(fixture.ConnectionString).Database);
        using var scope = fixture.ScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
        db.Users.Add(new() { Id = _user, Email = "mt1-" + _user.ToString("N") + "@example.test", Role = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Mt1HistoricalHash!") });
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        using var scope = fixture.ScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
        var memberships = await db.InstitutionMemberships.Where(m => _institutions.Contains(m.InstitutionId)).Select(m => m.Id).ToListAsync();
        await db.InstitutionMembershipRoles.Where(r => memberships.Contains(r.MembershipId)).ExecuteDeleteAsync();
        await db.InstitutionMemberships.Where(m => _institutions.Contains(m.InstitutionId)).ExecuteDeleteAsync();
        await db.Campuses.Where(c => _institutions.Contains(c.InstitutionId)).ExecuteDeleteAsync();
        await db.Institutions.Where(i => _institutions.Contains(i.Id)).ExecuteDeleteAsync();
        await db.Users.Where(u => u.Id == _user).ExecuteDeleteAsync();
    }

    private async Task InScope(Func<IServiceProvider, Task> action)
    { using var scope = fixture.ScopeFactory.CreateScope(); await action(scope.ServiceProvider); }

    private async Task<Guid> InstitutionAsync(string? code = null)
    {
        var id = Guid.NewGuid();
        _institutions.Add(id);
        await InScope(sp => sp.GetRequiredService<IInstitutionRepository>().AddAsync(new Institution
        { Id = id, Code = code ?? "MT1_" + id.ToString("N"), Name = "MT-1 integrity fixture (not an official foundation)", Region = "Test region" }));
        return id;
    }

    private async Task<Guid> MembershipAsync(Guid institution, params InstitutionRole[] roles)
    {
        var id = Guid.NewGuid();
        await InScope(sp => sp.GetRequiredService<IInstitutionMembershipRepository>().AddAsync(new()
        { Id = id, InstitutionId = institution, UserId = _user, Status = MembershipStatus.Active, Roles = roles.ToList() }));
        return id;
    }

    [Fact]
    public async Task MultipleFoundationsInSameRegionRemainDistinctAndProvisioning()
    {
        var a = await InstitutionAsync(); var b = await InstitutionAsync();
        await InScope(async sp =>
        {
            var repo = sp.GetRequiredService<IInstitutionRepository>();
            var first = (await repo.GetByIdAsync(a))!; var second = (await repo.GetByIdAsync(b))!;
            Assert.NotEqual(first.Id, second.Id); Assert.Equal(first.Region, second.Region);
            Assert.Equal(InstitutionStatus.Provisioning, first.Status);
            Assert.Equal(a, (await repo.GetByCodeAsync(first.Code))!.Id);
            Assert.NotEqual(default, first.CreatedAt); Assert.NotEqual(default, first.UpdatedAt);
        });
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task InstitutionCodeIsUniqueIncludingCaseVariants(bool lowerCase)
    {
        var code = "MT1_" + Guid.NewGuid().ToString("N").ToUpperInvariant();
        await InstitutionAsync(code);
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => InstitutionAsync(lowerCase ? code.ToLowerInvariant() : code));
        Assert.Equal(1062, Assert.IsType<MySqlException>(error.InnerException).Number);
    }

    [Fact]
    public async Task MultipleCampusesBelongToExactlyOneFoundationAndInactiveFalsePersists()
    {
        var a = await InstitutionAsync(); var b = await InstitutionAsync();
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        await InScope(async sp =>
        {
            var repo = sp.GetRequiredService<ICampusRepository>();
            await repo.AddAsync(new() { Id = first, InstitutionId = a, Name = "First", IsActive = false });
            await repo.AddAsync(new() { Id = second, InstitutionId = a, Name = "Second" });
        });
        await InScope(async sp =>
        {
            var repo = sp.GetRequiredService<ICampusRepository>();
            Assert.Equal(2, (await repo.GetByInstitutionAsync(a)).Count);
            Assert.Empty(await repo.GetByInstitutionAsync(b));
            Assert.Null(await repo.GetByIdAsync(b, first));
            Assert.False((await repo.GetByIdAsync(a, first))!.IsActive);
        });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => InScope(sp => sp.GetRequiredService<ICampusRepository>()
            .AddAsync(new() { Id = first, InstitutionId = b, Name = "Cannot belong to both" })));
        Assert.Equal(1062, Assert.IsType<MySqlException>(error.InnerException).Number);
    }

    [Fact]
    public async Task GlobalUserCanHaveDifferentAndMultipleRolesPerFoundation()
    {
        var a = await InstitutionAsync(); var b = await InstitutionAsync();
        await MembershipAsync(a, InstitutionRole.InstitutionAdmin, InstitutionRole.Student);
        await MembershipAsync(b, InstitutionRole.Teacher);
        await InScope(async sp =>
        {
            var repo = sp.GetRequiredService<IInstitutionMembershipRepository>();
            var first = (await repo.GetAsync(a, _user))!; var second = (await repo.GetAsync(b, _user))!;
            Assert.NotEqual(first.Id, second.Id); Assert.Equal(first.UserId, second.UserId);
            Assert.Equal(new[] { InstitutionRole.InstitutionAdmin, InstitutionRole.Student }, first.Roles.OrderBy(r => r));
            Assert.Equal(new[] { InstitutionRole.Teacher }, second.Roles);
            Assert.Equal("admin", await sp.GetRequiredService<ElectronicRegisterContext>().Users.Where(u => u.Id == _user).Select(u => u.Role).SingleAsync());
        });
    }

    [Fact]
    public async Task DuplicateMembershipAndDuplicateRoleAreRejectedByMySql()
    {
        var institution = await InstitutionAsync(); var membership = await MembershipAsync(institution, InstitutionRole.Student);
        var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => MembershipAsync(institution, InstitutionRole.Teacher));
        Assert.Equal(1062, Assert.IsType<MySqlException>(duplicate.InnerException).Number);
        var role = await Assert.ThrowsAsync<DbUpdateException>(() => InScope(sp => sp.GetRequiredService<IInstitutionMembershipRepository>()
            .AddRoleAsync(institution, membership, InstitutionRole.Student)));
        Assert.Equal(1062, Assert.IsType<MySqlException>(role.InnerException).Number);
    }

    [Fact]
    public async Task SuspensionPreservesMembershipRolesIdentityAndEveryLegacyCell()
    {
        var institution = await InstitutionAsync(); var membership = await MembershipAsync(institution, InstitutionRole.Student);
        var before = await LegacySnapshotAsync(); var now = DateTime.UtcNow;
        await InScope(sp => sp.GetRequiredService<IInstitutionMembershipRepository>().SuspendAsync(institution, membership, now));
        await InScope(async sp =>
        {
            var row = (await sp.GetRequiredService<IInstitutionMembershipRepository>().GetAsync(institution, _user))!;
            Assert.Equal(membership, row.Id); Assert.Equal(MembershipStatus.Suspended, row.Status);
            Assert.Equal(now.Ticks / 10, row.SuspendedAt!.Value.Ticks / 10); // MySQL datetime(6).
            Assert.Equal(new[] { InstitutionRole.Student }, row.Roles);
        });
        Assert.Equal(before, await LegacySnapshotAsync());
    }

    [Fact]
    public async Task MembershipOperationsRejectAnotherInstitutionGuid()
    {
        var a = await InstitutionAsync(); var b = await InstitutionAsync(); var membership = await MembershipAsync(a);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => InScope(sp => sp.GetRequiredService<IInstitutionMembershipRepository>()
            .SuspendAsync(b, membership, DateTime.UtcNow)));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => InScope(sp => sp.GetRequiredService<IInstitutionMembershipRepository>()
            .AddRoleAsync(b, membership, InstitutionRole.InstitutionAdmin)));
        await InScope(async sp => Assert.Empty((await sp.GetRequiredService<IInstitutionMembershipRepository>().GetAsync(a, _user))!.Roles));
    }

    [Theory]
    [InlineData("campus-institution")]
    [InlineData("membership-institution")]
    [InlineData("membership-user")]
    [InlineData("role-membership")]
    public async Task InvalidForeignKeysAreRejectedByMySql(string kind)
    {
        var institution = await InstitutionAsync();
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => InScope(async sp =>
        {
            var db = sp.GetRequiredService<ElectronicRegisterContext>();
            if (kind == "campus-institution") db.Campuses.Add(new() { Id = Guid.NewGuid(), InstitutionId = Guid.NewGuid(), Name = "Orphan", IsActive = true });
            else if (kind == "role-membership") db.InstitutionMembershipRoles.Add(new() { MembershipId = Guid.NewGuid(), Role = "Teacher" });
            else db.InstitutionMemberships.Add(new() { Id = Guid.NewGuid(), InstitutionId = kind == "membership-institution" ? Guid.NewGuid() : institution,
                UserId = kind == "membership-user" ? Guid.NewGuid() : _user, Status = "active", JoinedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }));
        Assert.Equal(1452, Assert.IsType<MySqlException>(error.InnerException).Number);
    }

    [Theory]
    [InlineData("institution")]
    [InlineData("user")]
    [InlineData("membership")]
    public async Task HistoricalParentsCannotBeCascadeDeleted(string parent)
    {
        var institution = await InstitutionAsync(); var membership = await MembershipAsync(institution, InstitutionRole.Teacher);
        var error = await Assert.ThrowsAsync<MySqlException>(() => InScope(async sp =>
        {
            var db = sp.GetRequiredService<ElectronicRegisterContext>();
            if (parent == "institution") await db.Institutions.Where(i => i.Id == institution).ExecuteDeleteAsync();
            else if (parent == "user") await db.Users.Where(u => u.Id == _user).ExecuteDeleteAsync();
            else await db.InstitutionMemberships.Where(m => m.Id == membership).ExecuteDeleteAsync();
        }));
        Assert.Equal(1451, error.Number);
        await InScope(async sp => Assert.NotNull(await sp.GetRequiredService<IInstitutionMembershipRepository>().GetAsync(institution, _user)));
    }

    [Fact]
    public async Task UnknownGlobalPrivilegeCannotBeInsertedAsMembershipRole()
    {
        var institution = await InstitutionAsync(); var membership = await MembershipAsync(institution);
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => InScope(async sp =>
        {
            var db = sp.GetRequiredService<ElectronicRegisterContext>();
            db.InstitutionMembershipRoles.Add(new() { MembershipId = membership, Role = "SuperAdmin" });
            await db.SaveChangesAsync();
        }));
        Assert.Equal(1265, Assert.IsType<MySqlException>(error.InnerException).Number);
    }

    [Fact]
    public async Task PreparatoryRepositoryDoesNotActivateFoundations()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => InScope(sp => sp.GetRequiredService<IInstitutionRepository>()
            .AddAsync(new() { Id = Guid.NewGuid(), Name = "Must not activate", Code = "MT1_BLOCKED", Status = InstitutionStatus.Active })));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task MigrationToolRejectsOtherDatabaseBeforeOpeningConnection(bool backfill)
    {
        const string forbidden = "Server=unreachable.invalid;Database=not_authorized;User=none;Password=none;";
        await Assert.ThrowsAsync<InvalidOperationException>(() => backfill ? Mt1SchemaMigration.BackfillAsync(forbidden, Guid.NewGuid(), "Migration identity") :
            Mt1SchemaMigration.ExpandAsync(forbidden));
    }

    [Fact]
    public async Task CanonicalExpandIsRerunnableAndDoesNotChangeLegacyData()
    {
        var before = await LegacySnapshotAsync();
        await Mt1SchemaMigration.ExpandAsync(fixture.ConnectionString);
        await Mt1SchemaMigration.ExpandAsync(fixture.ConnectionString);
        Assert.Equal(before, await LegacySnapshotAsync());
        using var connection = new MySqlConnection(fixture.ConnectionString); await connection.OpenAsync();
        using var command = new MySqlCommand("SELECT COUNT(*) FROM information_schema.referential_constraints " +
            "WHERE constraint_schema=DATABASE() AND constraint_name IN ('fk_campus_institution','fk_membership_institution','fk_membership_user','fk_membership_role_membership') " +
            "AND delete_rule='RESTRICT' AND update_rule='RESTRICT'", connection);
        Assert.Equal(4L, Convert.ToInt64(await command.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task ControlledLegacyBackfillPreservesEveryGuidAndCellAndIsIdempotent()
    {
        // Representative legacy graph includes accounts/profiles, class/subject, answers, Grade and audit.
        var ids = await fixture.PreparePendingReviewScenarioAsync(IntegrationScenario.Phase4Lifecycle);
        await Phase3.ReviewAsync(fixture.ScopeFactory, ids);
        await InScope(sp => sp.GetRequiredService<IExamSubmissionManager>().FinalizeAsync(ids.SubmissionId, new(), Phase3.Teacher));
        var before = await LegacySnapshotAsync();
        var institution = Guid.NewGuid(); _institutions.Add(institution);
        const string name = "Legacy migration holding record - test rehearsal, not an official ITS name";
        await Mt1SchemaMigration.BackfillAsync(fixture.ConnectionString, institution, name);
        Assert.Equal(before, await LegacySnapshotAsync());
        Guid membership;
        DateTime joined;
        await InScope(async sp =>
        {
            var db = sp.GetRequiredService<ElectronicRegisterContext>();
            var users = await db.Users.AsNoTracking().ToListAsync();
            var members = await db.InstitutionMemberships.AsNoTracking().Include(m => m.InstitutionMembershipRoles).Where(m => m.InstitutionId == institution).ToListAsync();
            Assert.Equal(users.Count, members.Count);
            foreach (var user in users)
            {
                var row = Assert.Single(members.Where(m => m.UserId == user.Id));
                Assert.Equal(user.Role switch { "admin" => "InstitutionAdmin", "teacher" => "Teacher", _ => "Student" }, Assert.Single(row.InstitutionMembershipRoles).Role);
            }
        });
        using (var scope = fixture.ScopeFactory.CreateScope())
        {
            var row = (await scope.ServiceProvider.GetRequiredService<IInstitutionMembershipRepository>().GetAsync(institution, _user))!;
            membership = row.Id; joined = row.JoinedAt;
        }
        await InScope(sp => sp.GetRequiredService<IInstitutionMembershipRepository>().SuspendAsync(institution, membership, DateTime.UtcNow));
        await Mt1SchemaMigration.BackfillAsync(fixture.ConnectionString, institution, name);
        Assert.Equal(before, await LegacySnapshotAsync());
        await InScope(async sp =>
        {
            var row = (await sp.GetRequiredService<IInstitutionMembershipRepository>().GetAsync(institution, _user))!;
            Assert.Equal(membership, row.Id); Assert.Equal(joined, row.JoinedAt); Assert.Equal(MembershipStatus.Suspended, row.Status);
            Assert.Equal(InstitutionStatus.Provisioning, (await sp.GetRequiredService<IInstitutionRepository>().GetByIdAsync(institution))!.Status);
        });
        // Configuration mismatch must fail closed, without a second migration holding tenant.
        await Assert.ThrowsAsync<InvalidOperationException>(() => Mt1SchemaMigration.BackfillAsync(fixture.ConnectionString, Guid.NewGuid(), name));
        Assert.Equal(before, await LegacySnapshotAsync());
        output.WriteLine("21-table legacy manifest (row counts and all-cell SHA256, includes every original Guid/hash/value): " + before);
    }

    private static readonly string[] LegacyTables = {
        "audit_event","biennium","biennium_study_area","biennium_study_path","class","class_subject","exam","exam_student","exam_submission",
        "exam_violation","grade","question","question_option","student","study_area","study_path","subject","submission_answer","submission_answer_option","teacher","user" };

    private async Task<string> LegacySnapshotAsync()
    {
        var results = new SortedDictionary<string, object>();
        await using var connection = new MySqlConnection(fixture.ConnectionString); await connection.OpenAsync();
        foreach (var table in LegacyTables)
        {
            var rows = new List<string>();
            // Identifiers come solely from the fixed allowlist, never input.
            await using var command = new MySqlCommand("SELECT * FROM `" + table + "`", connection);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var fields = new List<string?>();
                for (int index = 0; index < reader.FieldCount; index++)
                    fields.Add(reader.IsDBNull(index) ? null : reader.GetValue(index) switch
                    { DateTime date => date.ToString("O", CultureInfo.InvariantCulture), byte[] bytes => Convert.ToHexString(bytes),
                        IFormattable value => value.ToString(null, CultureInfo.InvariantCulture), var value => value.ToString() });
                rows.Add(JsonSerializer.Serialize(fields));
            }
            rows.Sort(StringComparer.Ordinal);
            results[table] = new { Count = rows.Count, Sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(rows)))) };
        }
        return JsonSerializer.Serialize(results);
    }
}
