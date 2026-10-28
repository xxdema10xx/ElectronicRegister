using System.ComponentModel.DataAnnotations;

namespace ElectronicRegisterAPI.Domain.DTOs;

public sealed record IdentityTicketDto(string Ticket, int ExpiresInSeconds);
