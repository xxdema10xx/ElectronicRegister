using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Domain.Interfaces.Managers;

public interface IIdentityAccountManager
{
    Task<Guid> RegisterAsync(RegisterIdentityAccountDto input, CancellationToken ct = default);
    Task<IdentityAccountDto> GetAsync(Guid userId, CancellationToken ct = default);
    Task UpdateAsync(
        Guid userId,
        long generation,
        UpdateIdentityAccountDto input,
        CancellationToken ct = default
    );
    Task ChangePasswordAsync(
        Guid userId,
        long generation,
        ChangeIdentityPasswordDto input,
        CancellationToken ct = default
    );
}
