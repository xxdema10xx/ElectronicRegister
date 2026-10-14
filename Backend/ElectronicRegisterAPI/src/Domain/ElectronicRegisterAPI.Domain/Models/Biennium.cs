namespace ElectronicRegisterAPI.Domain.Models;

public class Biennium
{
    public Guid Id { get; set; }

    public DateOnly StartYear { get; set; }

    public DateOnly EndYear { get; set; }

    public bool IsActive =>
        DateOnly.FromDateTime(DateTime.Today) >= StartYear &&
        DateOnly.FromDateTime(DateTime.Today) <= EndYear;
}