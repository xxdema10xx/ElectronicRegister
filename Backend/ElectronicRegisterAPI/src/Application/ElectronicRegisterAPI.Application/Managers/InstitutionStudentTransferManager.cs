using ElectronicRegisterAPI.Application.Services;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
namespace ElectronicRegisterAPI.Application.Managers;
internal sealed class InstitutionStudentTransferManager(InstitutionSchoolAuthorization authorization,IInstitutionStudentTransferRepository repository,
    AcademicTransferOptions options,TimeProvider clock) : IInstitutionStudentTransferManager
{
    public async Task<StudentClassTransferResultDto> TransferAsync(Guid studentId,TransferStudentClassDto dto,CancellationToken ct)
    {
        var tenant=await authorization.AdminAsync(ct);
        if(studentId==Guid.Empty || dto.EventId==Guid.Empty || dto.ToClassId==Guid.Empty || dto.ExpectedRevision<0 || dto.EffectiveDate.Year<1000)
            throw new ArgumentException("Dati del trasferimento non validi.");
        _=StudentClassTransferPolicy.Reason(dto.Reason);
        return await repository.TransferAsync(tenant,studentId,dto,options,clock,ct);
    }
    public async Task<StudentClassTransferPageDto> HistoryAsync(Guid studentId,int page,int size,CancellationToken ct)
    {
        var tenant=await authorization.AdminAsync(ct);InstitutionClassRules.Page(page,size);
        return await repository.HistoryAsync(tenant,studentId,page,size,ct);
    }
}
