using ElectronicRegisterAPI.Domain.Interfaces.Services;

namespace ElectronicRegisterAPI.Infrastructure.Services;

// Mailboxes are not implemented (future Microsoft 365 provider). Nothing is created,
// suspended, deleted or sent, and no address or identifier is logged.
internal sealed class NoOpMailboxProvisioner : IMailboxProvisioner
{
    public Task CreateAsync(Guid userId, string address, CancellationToken ct) => Task.CompletedTask;
    public Task SuspendAsync(Guid userId, string address, CancellationToken ct) => Task.CompletedTask;
    public Task DeleteAsync(Guid userId, string address, CancellationToken ct) => Task.CompletedTask;
}
