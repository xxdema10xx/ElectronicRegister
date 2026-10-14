namespace ElectronicRegisterAPI.Domain.DTOs;
public class CreateBienniumDto
{
    public DateOnly StartYear { get; set; }
    public DateOnly EndYear { get; set; }
}
