using System.Globalization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Enums;

public enum CalendarConflictKind
{
    Duplicate,
    ClassOverlap,
    TeacherOverlap
}
