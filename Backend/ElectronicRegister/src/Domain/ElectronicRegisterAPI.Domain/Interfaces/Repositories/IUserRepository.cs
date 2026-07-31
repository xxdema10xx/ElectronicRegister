using System;
using System.Collections.Generic;
using System.Text;
using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories
{
    public interface IUserRepository
    {
        Task CreateAsync(CreateUserDto user);
        Task<List<UserDto>> ReadAllAsync();
        Task UpdateAsync(Guid id, UpdateUserDto dto);
        Task UpdatePasswordAsync(Guid id, UpdatePasswordDto dto);
        Task DeleteAsync(Guid id);
        Task<int> CountAsync();
        Task<UserDto> GetByIdAsync(Guid id);
    }
}
