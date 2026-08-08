using ElectronicRegisterAPI.Domain.DTOs;
public interface IUserManager
{
    Task<int> CountAsync();
    Task<List<UserDto>> GetAllAsync();
    Task<UserDto?> GetByIdAsync(Guid id);
    Task<bool> UpdatePasswordAsync(Guid id, UpdatePasswordDto dto, ClaimsContext caller);
    Task<bool> UpdateAsync(Guid id, UpdateUserDto dto);
    Task<bool> AddAsync(CreateUserDto dto);
    Task<bool> DeleteAsync(Guid id);
}
