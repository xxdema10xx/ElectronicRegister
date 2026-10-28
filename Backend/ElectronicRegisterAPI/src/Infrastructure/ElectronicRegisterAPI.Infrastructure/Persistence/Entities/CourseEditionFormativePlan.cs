// Database First entity for course_edition_formative_plan
#nullable enable
namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
public partial class CourseEditionFormativePlan {
 public Guid Id {get;set;}
 public Guid InstitutionId {get;set;}
 public Guid StudyPathId {get;set;}
 public Guid CourseEditionId {get;set;}
 public Guid VersionId {get;set;}
 public DateTime CreatedAtUtc {get;set;}
}
