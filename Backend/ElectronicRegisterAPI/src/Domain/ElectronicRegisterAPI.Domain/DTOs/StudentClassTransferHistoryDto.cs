using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.DTOs;

public sealed record StudentClassTransferHistoryDto(
    StudentClassTransferResultDto Receipt,
    string FromClassName,
    string ToClassName,
    string FromCourseName,
    string ToCourseName,
    Guid SourceEnrollmentId,
    Guid DestinationEnrollmentId,
    string Reason,
    Guid ActorUserId,
    Guid ActorMembershipId,
    long RevisionBefore,
    DateOnly SourceCourseStartDate,
    DateOnly DestinationCourseStartDate
);
