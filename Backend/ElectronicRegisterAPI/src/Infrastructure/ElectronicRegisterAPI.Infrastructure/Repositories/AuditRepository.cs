using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal class AuditRepository : IAuditRepository
{
    private readonly ElectronicRegisterContext _context;

    public AuditRepository(ElectronicRegisterContext context)
    {
        _context = context;
    }

    public async Task AddAsync(AuditEvent auditEvent)
    {
        var entity = new Infrastructure.Persistence.Entities.AuditEvent
        {
            Id = auditEvent.Id == Guid.Empty ? Guid.NewGuid() : auditEvent.Id,
            ActorUserId = auditEvent.ActorUserId,
            Action = auditEvent.Action,
            EntityType = auditEvent.EntityType,
            EntityId = auditEvent.EntityId,
            BeforeJson = auditEvent.BeforeJson,
            AfterJson = auditEvent.AfterJson,
            RequestId = auditEvent.RequestId,
            CreatedAt = auditEvent.CreatedAt == default ? DateTime.UtcNow : auditEvent.CreatedAt
        };

        _context.AuditEvents.Add(entity);
        await _context.SaveChangesAsync();
    }
}
