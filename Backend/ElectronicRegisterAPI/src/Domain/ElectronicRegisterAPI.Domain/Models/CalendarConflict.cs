using ElectronicRegisterAPI.Domain.Enums;
using System.Globalization;

namespace ElectronicRegisterAPI.Domain.Models;

public sealed record CalendarConflict(Guid FirstLessonId, Guid SecondLessonId, CalendarConflictKind Kind);
