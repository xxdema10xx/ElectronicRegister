using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Domain.Models
{
    public class TeacherAvailability
    {
        public Guid Id { get; set; }
        public Guid TeacherId { get; set; }
        public Guid SubjectId { get; set; }
        public Guid ClassId { get; set; }
        public DateTime AvailableFrom { get; set; }
        public DateTime AvailableTo { get; set; }
        public DateOnly AvailableDate { get; set; }
        public AvailabilityStatus Status { get; set; }
        public int Hours => (int)(AvailableTo - AvailableFrom).TotalHours;
    }
}
