using System;
using System.Collections.Generic;
using System.Text;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface ITransactionManager
{
    Task ExecuteAsync(Func<Task> action, CancellationToken cancellationToken = default);
}
