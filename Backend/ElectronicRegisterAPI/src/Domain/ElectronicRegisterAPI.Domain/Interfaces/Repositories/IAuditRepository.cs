using System;
using System.Collections.Generic;
using System.Text;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface IAuditRepository
{
    Task AddAsync(AuditEvent auditEvent);
}
