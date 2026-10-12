using System;
using System.Collections.Generic;

namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public partial class AuditEvent
{
    public Guid Id { get; set; }

    public Guid? ActorUserId { get; set; }

    public string Action { get; set; } = null!;

    public string EntityType { get; set; } = null!;

    public Guid? EntityId { get; set; }

    public string? BeforeJson { get; set; }

    public string? AfterJson { get; set; }

    public string? RequestId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User? ActorUser { get; set; }
}
