namespace ElectronicRegisterAPI.Domain.Interfaces.Services;

// Attachment point for a future mailbox provider (Microsoft 365). The registered
// implementation is a no-op: no mailbox is created and no message is sent.
public interface IMailboxProvisioner
{
    Task CreateAsync(Guid userId, string address, CancellationToken ct);
    Task SuspendAsync(Guid userId, string address, CancellationToken ct);
    Task DeleteAsync(Guid userId, string address, CancellationToken ct);
}
