using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Calendar;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CalendarSaveLesson(
    [property: JsonRequired] Guid EventId,
    [property: JsonRequired] long ExpectedRevision,
    CalendarLessonInput Lesson,
    string Reason
);
