using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Managers;

public interface IInstitutionAuthManager
{
    Task<IdentityTicketDto?> LoginAsync(LoginDto input);
    Task<IdentityTicketDto?> MicrosoftLoginAsync(MicrosoftLoginDto input);
    Task<bool> IdentityExistsAsync(Guid userId);
    Task<IReadOnlyList<InstitutionChoiceDto>> InstitutionsAsync(Guid userId, CancellationToken cancellationToken);
    Task<InstitutionAccessDto> SelectAsync(Guid userId, SelectInstitutionDto input, CancellationToken cancellationToken);
    Task<TenantContext?> AuthorizeAsync(TenantContext token, CancellationToken cancellationToken);
}
