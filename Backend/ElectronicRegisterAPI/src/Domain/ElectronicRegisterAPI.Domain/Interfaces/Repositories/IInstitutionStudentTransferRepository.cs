using ElectronicRegisterAPI.Domain.Configuration;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface IInstitutionStudentTransferRepository
{
    Task<StudentClassTransferResultDto> TransferAsync(
        TenantContext tenant,
        Guid studentId,
        TransferStudentClassDto dto,
        AcademicTransferOptions options,
        TimeProvider clock,
        CancellationToken ct
    );
    Task<StudentClassTransferPageDto> HistoryAsync(
        TenantContext tenant,
        Guid studentId,
        int page,
        int size,
        CancellationToken ct
    );
}
