using System.Text;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using ElectronicRegisterAPI.Domain.Policies;

namespace ElectronicRegisterAPI.Application.Managers;

internal sealed class IdentityAccountManager(IIdentityAccountRepository accounts, IPasswordHasher passwords) : IIdentityAccountManager
{
    public Task<Guid> RegisterAsync(RegisterIdentityAccountDto input, CancellationToken ct = default)
    {
        var email = input.Email?.Trim();
        if (string.IsNullOrEmpty(email) || email.Length > 255 || !System.Net.Mail.MailAddress.TryCreate(email, out var address) || address.Address != email)
            throw new ArgumentException("Email non valida.");
        PasswordRules.Validate(input.Password);
        return accounts.CreateAsync(email, passwords.Hash(input.Password), ct);
    }

    public async Task<IdentityAccountDto> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var account = await accounts.GetAsync(userId, ct) ?? throw new UnauthorizedAccessException();
        return new(account.UserId, account.Email, account.DisplayName, !string.IsNullOrEmpty(account.PasswordHash));
    }

    public Task UpdateAsync(
        Guid userId,
        long generation,
        UpdateIdentityAccountDto input,
        CancellationToken ct = default
    )
    {
        var name = input.DisplayName?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 160 || name.Any(char.IsControl))
            throw new ArgumentException("Nome visualizzato non valido.");
        return accounts.UpdateDisplayNameAsync(userId, generation, name, ct);
    }

    public async Task ChangePasswordAsync(
        Guid userId,
        long generation,
        ChangeIdentityPasswordDto input,
        CancellationToken ct = default
    )
    {
        var lease = await accounts.BeginPasswordAttemptAsync(userId, ct) ?? throw new Domain.Exceptions.IdentityRateLimitException();
        try
        {
        var account = await accounts.GetAsync(userId, ct) ?? throw new UnauthorizedAccessException();
        // External-only accounts cannot establish a local credential using a bearer token alone.
        if (account.Generation != generation || string.IsNullOrEmpty(account.PasswordHash) || string.IsNullOrEmpty(input.CurrentPassword) || !passwords.Verify(input.CurrentPassword, account.PasswordHash))
            throw new UnauthorizedAccessException("Credenziali non valide.");
        PasswordRules.Validate(input.NewPassword);
        if (passwords.Verify(input.NewPassword, account.PasswordHash))
            throw new ArgumentException("La nuova password deve essere diversa.");
        await accounts.ChangePasswordAsync(userId, generation, account.PasswordHash, passwords.Hash(input.NewPassword), ct);
        }
        finally
        {
            await accounts.EndPasswordAttemptAsync(userId, lease, CancellationToken.None);
        }
    }
}
