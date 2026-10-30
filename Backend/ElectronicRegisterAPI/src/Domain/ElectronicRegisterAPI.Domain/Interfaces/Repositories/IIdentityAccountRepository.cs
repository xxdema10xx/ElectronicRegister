using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface IIdentityAccountRepository
{
    Task<Guid> CreateAsync(string email, string passwordHash, CancellationToken ct = default);
    Task<IdentityAccount?> GetAsync(Guid userId, CancellationToken ct = default);
    Task<IdentityAccount?> FindByEmailAsync(string email, CancellationToken ct = default);
    Task<long> GenerationAsync(Guid userId, CancellationToken ct = default);
    Task RequireGenerationAsync(Guid userId, long generation, CancellationToken ct = default);
    Task<Guid?> BeginPasswordAttemptAsync(Guid userId, CancellationToken ct = default);
    Task EndPasswordAttemptAsync(Guid userId, Guid lease, CancellationToken ct = default);
    Task ChangePasswordAsync(
        Guid userId,
        long generation,
        string expectedHash,
        string newHash,
        CancellationToken ct = default
    );
    Task UpdateDisplayNameAsync(
        Guid userId,
        long generation,
        string displayName,
        CancellationToken ct = default
    );
}
