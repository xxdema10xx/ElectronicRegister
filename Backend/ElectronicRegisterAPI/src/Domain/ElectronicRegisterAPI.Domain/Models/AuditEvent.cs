using System;
using System.Collections.Generic;
using System.Text;

namespace ElectronicRegisterAPI.Domain.Models
{
    public partial class AuditEvent
    {
        public Guid Id { get; set; }

        public Guid? ActorUserId { get; set; }

        public required string Action { get; set; }

        public required string EntityType { get; set; }

        public Guid? EntityId { get; set; }

        public string? BeforeJson { get; set; }

        public string? AfterJson { get; set; }

        public string? RequestId { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
