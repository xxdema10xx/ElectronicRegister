using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace ElectronicRegisterAPI.Domain.DTOs;
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class TransferStudentClassDto
{
    [Required] public required Guid EventId { get; init; }
    [Required] public required Guid ToClassId { get; init; }
    [Required] public required DateOnly EffectiveDate { get; init; }
    [Required] public required string Reason { get; init; }
    [Required] public required long ExpectedRevision { get; init; }
}
public sealed record StudentClassTransferResultDto(Guid EventId,Guid StudentId,Guid FromClassId,Guid ToClassId,
    Guid FromCourseEditionId,Guid ToCourseEditionId,DateOnly EffectiveDate,int AppliedLimitMonths,long Revision,DateTime OccurredAtUtc);
public sealed record StudentClassTransferHistoryDto(StudentClassTransferResultDto Receipt,string FromClassName,string ToClassName,
    string FromCourseName,string ToCourseName,Guid SourceEnrollmentId,Guid DestinationEnrollmentId,string Reason,
    Guid ActorUserId,Guid ActorMembershipId,long RevisionBefore,DateOnly SourceCourseStartDate,DateOnly DestinationCourseStartDate);
public sealed record StudentClassTransferPageDto(IReadOnlyList<StudentClassTransferHistoryDto> Items,int TotalCount,int PageNumber,int PageSize,long CurrentRevision);
