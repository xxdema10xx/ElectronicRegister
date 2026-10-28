using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Domain.Interfaces.Managers;

public interface IUserManager
{
    Task<int> CountAsync();
    Task<List<UserDto>> GetAllAsync();
    Task<UserDto?> GetByIdAsync(Guid id);
    Task<bool> UpdatePasswordAsync(Guid id, UpdatePasswordDto dto, ClaimsContext caller);
    Task<bool> UpdateAsync(Guid id, UpdateUserDto dto);
    Task<Guid> AddAsync(CreateUserDto dto);
    Task<bool> DeleteAsync(Guid id);
}
