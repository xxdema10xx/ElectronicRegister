using ElectronicRegisterAPI.Domain.DTOs.Onboarding;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ElectronicRegisterAPI.Infrastructure.Security;

internal sealed class ClosedInvitationChannel : IInvitationChannel
{
    public bool IsOperational => false;

    public Task DeliverAsync(InvitationDelivery delivery, CancellationToken ct) => throw new UnauthorizedAccessException("Canale di consegna non configurato.");
}
