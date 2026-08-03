using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface IUserRepository
{
    Task<int> CountAsync();
    Task<List<UserDto>> GetAllAsync();
    Task<UserDto?> GetByIdAsync(Guid id);
    Task<UserDto?> GetByEmailAsync(string email);
    Task<bool> ExistsByEmailAsync(string email);
    Task AddAsync(UserDto userDto);
    Task UpdateAsync(UserDto userDto);
    Task DeleteAsync(Guid id);
}