using System.IdentityModel.Tokens.Jwt;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace ElectronicRegisterAPI.Api.Security;

internal sealed class CurrentTenant : ICurrentTenant
{
    private TenantContext? _value;
    public TenantContext Value => _value ?? throw new UnauthorizedAccessException("Contesto non autorizzato.");

    internal void Set(TenantContext value) => _value = value;
}
