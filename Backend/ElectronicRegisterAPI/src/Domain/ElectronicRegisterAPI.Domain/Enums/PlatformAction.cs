using System.Text.Json;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Enums;

public enum PlatformAction
{
    Provision,
    Suspend,
    Reactivate,
    Archive,
    RequestRecovery,
    ApproveRecovery,
    ReissueFirstAdmin,
    RevokeFirstAdmin,
    RevokeRecovery,
    ReissueRecovery
}
