using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MySqlConnector;

namespace ElectronicRegister.OwnerProvisioning;

public sealed record ProvisionResult(string Status, Guid InstitutionId, Guid UserId, Guid MembershipId, string? TemporaryPassword);
public sealed record ResetResult(Guid UserId, long Generation, string TemporaryPassword);

// Owner-only operations, each in one transaction. Mirrors the API invariants without
// referencing production assemblies: identity state + append-only identity events,
// active membership (security state created by trigger), audited Operator event.
public static class OwnerProvisioner
{
    private const string OperatorId = "owner-script", KeyId = "unsigned-local";
    public const string Temporary = "password.temporary", Provisioned = "account.provisioned";

    public static string Email(string? input)
    {
        var email = input?.Trim();
        if (string.IsNullOrEmpty(email) || email.Length > 255 || !System.Net.Mail.MailAddress.TryCreate(email, out var address) || address.Address != email)
            throw new ArgumentException("Email non valida.");
        return email;
    }

    private static void Text(string? value, int max, string what)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > max || value.Any(char.IsControl))
            throw new ArgumentException(what + " non valido.");
    }

    private static async Task<int> Execute(MySqlConnection c, MySqlTransaction tx, string sql, params (string, object?)[] values)
    {
        await using var cmd = Command(c, tx, sql, values);
        return await cmd.ExecuteNonQueryAsync();
    }

    private static MySqlCommand Command(MySqlConnection c, MySqlTransaction tx, string sql, params (string Name, object? Value)[] values)
    {
        var cmd = new MySqlCommand(sql, c, tx);
        foreach (var (name, value) in values) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return cmd;
    }

    private static Guid? AsGuid(object? value) => value switch { Guid g => g, string text => Guid.Parse(text), _ => null };

    private static string Intent(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));

    private static Task Audit(MySqlConnection c, MySqlTransaction tx, Guid institution, string action, object intent, string reason, object result) =>
        Execute(c, tx, "INSERT INTO institution_onboarding_event(institution_id,id,actor_kind,operator_id,key_id,action,intent_hash,reason,result_json,occurred_at_utc) VALUES(@i,@e,'Operator',@o,@k,@a,@h,@r,@j,UTC_TIMESTAMP(6))",
            ("@i", institution), ("@e", Guid.NewGuid()), ("@o", OperatorId), ("@k", KeyId), ("@a", action), ("@h", Intent(intent)), ("@r", reason), ("@j", JsonSerializer.Serialize(result)));

    public static async Task<ProvisionResult> CreateInstitutionAsync(MySqlConnection c, string code, string name, string adminEmail, string reason)
    {
        if (string.IsNullOrEmpty(code) || !Regex.IsMatch(code, "^[A-Za-z0-9_-]{2,64}$")) throw new ArgumentException("Codice istituto non valido.");
        Text(name, 200, "Nome istituto");
        Text(reason, 500, "Motivo");
        var email = Email(adminEmail);
        await using var tx = await c.BeginTransactionAsync();
        await using (var existing = Command(c, tx, "SELECT id FROM institution WHERE code=@code FOR UPDATE", ("@code", code)))
        {
            if (AsGuid(await existing.ExecuteScalarAsync()) is { } id)
            {
                // Idempotent: the same institution with the same first admin is already provisioned.
                await using var admin = Command(c, tx, "SELECT m.id,u.id FROM institution_membership m JOIN `user` u ON u.id=m.user_id JOIN institution_membership_role r ON r.membership_id=m.id AND r.role='InstitutionAdmin' WHERE m.institution_id=@i AND u.email=@email", ("@i", id), ("@email", email));
                await using var reader = await admin.ExecuteReaderAsync();
                if (!await reader.ReadAsync()) throw new InvalidOperationException("Istituto già esistente con un amministratore diverso: nessuna modifica.");
                var result = new ProvisionResult("already-provisioned", id, reader.GetGuid(1), reader.GetGuid(0), null);
                await reader.DisposeAsync();
                await tx.CommitAsync();
                return result;
            }
        }
        await using (var used = Command(c, tx, "SELECT COUNT(*) FROM `user` WHERE email=@email", ("@email", email)))
            if (Convert.ToInt64(await used.ExecuteScalarAsync()) != 0) throw new InvalidOperationException("Email già registrata: nessuna modifica.");
        Guid institution = Guid.NewGuid(), user = Guid.NewGuid(), membership = Guid.NewGuid();
        var password = TemporaryPassword.New();
        await Execute(c, tx, "INSERT INTO institution(id,name,code,status) VALUES(@i,@n,@code,'active')", ("@i", institution), ("@n", name.Trim()), ("@code", code));
        // Historical required column, never an authorization decision (same value as the API).
        await Execute(c, tx, "INSERT INTO `user`(id,email,password_hash,role) VALUES(@u,@email,@hash,'student')", ("@u", user), ("@email", email), ("@hash", TemporaryPassword.Hash(password)));
        await Execute(c, tx, "INSERT INTO identity_account_state(user_id,generation) VALUES(@u,0)", ("@u", user));
        await Execute(c, tx, "INSERT INTO identity_account_event(id,user_id,action) VALUES(@e,@u,@a)", ("@e", Guid.NewGuid()), ("@u", user), ("@a", Provisioned));
        await Execute(c, tx, "INSERT INTO identity_account_event(id,user_id,action) VALUES(@e,@u,@a)", ("@e", Guid.NewGuid()), ("@u", user), ("@a", Temporary));
        await Execute(c, tx, "INSERT INTO institution_membership(id,institution_id,user_id,status,joined_at) VALUES(@m,@i,@u,'active',UTC_TIMESTAMP(6))", ("@m", membership), ("@i", institution), ("@u", user));
        await Execute(c, tx, "INSERT INTO institution_membership_role(membership_id,role) VALUES(@m,'InstitutionAdmin')", ("@m", membership));
        await Audit(c, tx, institution, "OwnerProvision", new { institution, code, user, membership }, reason.Trim(), new { institutionId = institution, userId = user, membershipId = membership });
        await tx.CommitAsync();
        return new("created", institution, user, membership, password);
    }

    public static async Task<ResetResult> ResetPasswordAsync(MySqlConnection c, string accountEmail, string reason)
    {
        Text(reason, 500, "Motivo");
        var email = Email(accountEmail);
        await using var tx = await c.BeginTransactionAsync();
        Guid user;
        await using (var find = Command(c, tx, "SELECT id FROM `user` WHERE email=@email FOR UPDATE", ("@email", email)))
            user = AsGuid(await find.ExecuteScalarAsync()) ?? throw new InvalidOperationException("Account non trovato: nessuna modifica.");
        await Execute(c, tx, "INSERT INTO identity_account_state(user_id,generation) VALUES(@u,0) ON DUPLICATE KEY UPDATE user_id=user_id", ("@u", user));
        long generation;
        await using (var state = Command(c, tx, "SELECT generation FROM identity_account_state WHERE user_id=@u FOR UPDATE", ("@u", user)))
            generation = Convert.ToInt64(await state.ExecuteScalarAsync());
        if (generation < 0 || generation == long.MaxValue) throw new InvalidOperationException("Generation esaurita: nessuna modifica.");
        var password = TemporaryPassword.New();
        await Execute(c, tx, "UPDATE `user` SET password_hash=@hash WHERE id=@u", ("@hash", TemporaryPassword.Hash(password)), ("@u", user));
        // Generation +1 revokes every selection and access token issued before the reset.
        if (await Execute(c, tx, "UPDATE identity_account_state SET generation=generation+1 WHERE user_id=@u AND generation=@g", ("@u", user), ("@g", generation)) != 1)
            throw new InvalidOperationException("Stato identità cambiato: nessuna modifica.");
        await Execute(c, tx, "INSERT INTO identity_account_event(id,user_id,action) VALUES(@e,@u,@a)", ("@e", Guid.NewGuid()), ("@u", user), ("@a", Temporary));
        var institutions = new List<Guid>();
        await using (var members = Command(c, tx, "SELECT DISTINCT institution_id FROM institution_membership WHERE user_id=@u", ("@u", user)))
        await using (var reader = await members.ExecuteReaderAsync())
            while (await reader.ReadAsync()) institutions.Add(reader.GetGuid(0));
        foreach (var institution in institutions)
            await Audit(c, tx, institution, "OwnerPasswordReset", new { user, generation }, reason.Trim(), new { userId = user, identityGeneration = generation + 1 });
        await tx.CommitAsync();
        return new(user, generation + 1, password);
    }
}
