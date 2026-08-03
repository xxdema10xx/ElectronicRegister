using System;
using System.Collections.Generic;
using System.Text;
using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Domain.Interfaces.Managers
{
    public interface IUserManager
    {
        Task<int> CountAsync(ClaimsContext caller);
        Task<List<UserDto>> GetAllAsync(ClaimsContext caller);
        Task<UserDto> GetByIdAsync(Guid id, ClaimsContext caller);
        Task UpdateAsync(Guid id, UpdateUserDto dto, ClaimsContext caller);
        Task AddAsync(CreateUserDto dto, ClaimsContext caller);
        Task DeleteAsync(Guid id, ClaimsContext caller);
    }
}
