namespace ElectronicRegisterAPI.Domain.DTOs;
public class BienniumDto
{
    public Guid Id { get; set; }
    public DateOnly StartYear { get; set; }
    public DateOnly EndYear { get; set; }
}
