namespace ElectronicRegisterAPI.Domain.DTOs
{
    public class ItsClassDto
    {
        public Guid Id { get; set; }
        public required string Name { get; set; }
        public Guid BienniumStudyPathId { get; set; }
    }
}
