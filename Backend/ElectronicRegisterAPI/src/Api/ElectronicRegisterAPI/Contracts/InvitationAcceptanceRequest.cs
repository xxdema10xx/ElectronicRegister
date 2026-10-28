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
public sealed class InvitationAcceptanceRequest
{
    [JsonRequired]
    public Guid InstitutionId { get; init; }

    [JsonRequired]
    public Guid EventId { get; init; }

    [JsonRequired]
    public string Challenge { get; init; } = "";
}
