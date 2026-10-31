using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Enums;

public enum CalendarCapability
{
    Manage,
    ReadAudit,
    ReadOwnTeacher,
    ReadOwnStudent,
    ConfirmOwnDelivery
}
