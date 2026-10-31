using ElectronicRegisterAPI.Infrastructure.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal class AuditRepository : IAuditRepository
{
    private readonly ElectronicRegisterContext _context;
    private readonly InstitutionExamDataScope? _scope;
    private CancellationToken Ct => _scope?.CancellationToken ?? CancellationToken.None;
    private IQueryable<T> Rows<T>()
        where T : class => _scope?.Query<T>() ?? _context.Set<T>();
    private Task SaveAsync() => _scope is null ? _context.SaveChangesAsync(Ct) : _scope.SaveAsync();
    public AuditRepository(ElectronicRegisterContext context, InstitutionExamDataScope? scope = null)
    {
        _context = context;
        _scope = scope;
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
        await SaveAsync();
    }
}
