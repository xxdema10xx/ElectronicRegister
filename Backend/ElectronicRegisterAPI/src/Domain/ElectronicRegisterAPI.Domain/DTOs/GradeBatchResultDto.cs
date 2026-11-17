namespace ElectronicRegisterAPI.Domain.DTOs;

public class GradeBatchResultDto
{
    public Guid EventId { get; set; }
    public List<GradeBatchCreatedDto> Created { get; set; } = new();
}
