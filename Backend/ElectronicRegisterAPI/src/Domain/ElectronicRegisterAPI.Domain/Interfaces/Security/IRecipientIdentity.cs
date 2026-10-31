using System.Text.Json;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Security;

public interface IRecipientIdentity
{
    Task<Guid> VerifySelectionTicketAsync(string ticket, CancellationToken ct = default);
}
