using ElectronicRegisterAPI.Domain.DTOs.Onboarding;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Api.Contracts;
using ElectronicRegisterAPI.Api.Security;
using ElectronicRegisterAPI.Api.ExceptionHandling;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Api.Controllers;

namespace ElectronicRegisterAPI.Api.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class InviteMemberRequest
{
    [JsonRequired]
    public Guid EventId { get; init; }

    [JsonRequired]
    public Guid RecipientUserId { get; init; }

    [JsonRequired]
    public string[] Roles { get; init; } = [];

    [JsonRequired]
    public string Reason { get; init; } = "";

    internal InviteMember ToDomain()
    {
        if (Roles is null || Roles.Any(r => !Enum.GetNames<InstitutionRole>().Contains(r, StringComparer.Ordinal)))
            throw new ArgumentException();
        return new(EventId, RecipientUserId, Roles.Select(Enum.Parse<InstitutionRole>).ToArray(), Reason);
    }
}
