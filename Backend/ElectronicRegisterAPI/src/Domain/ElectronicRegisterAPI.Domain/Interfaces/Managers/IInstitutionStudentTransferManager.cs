using ElectronicRegisterAPI.Domain.DTOs;
namespace ElectronicRegisterAPI.Domain.Interfaces.Managers;
public interface IInstitutionStudentTransferManager
{
    Task<StudentClassTransferResultDto> TransferAsync(Guid studentId,TransferStudentClassDto dto,CancellationToken ct);
    Task<StudentClassTransferPageDto> HistoryAsync(Guid studentId,int page,int size,CancellationToken ct);
}
