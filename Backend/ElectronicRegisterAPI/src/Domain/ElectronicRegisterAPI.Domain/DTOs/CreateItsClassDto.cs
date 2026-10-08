namespace ElectronicRegisterAPI.Domain.DTOs
{
public class CreateItsClassDto
{
        public string Name { get; set; } = null!;
        public Guid BienniumStudyPathId { get; set; }
    }
}
