using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace ElectronicRegisterAPI.Domain.Interfaces.Managers
{
    public interface IItsClassManager
    {
        Task<int> CountAsync();
        Task<List<ItsClassDto>> GetAllAsync();
        Task<ItsClassDto?> GetByIdAsync(Guid id);
        Task<List<ItsClassDto>> GetByIdsAsync(IEnumerable<Guid> ids);
        Task<ItsClassDto?> GetByNameAsync(string name);
        Task<bool> AddAsync(CreateItsClassDto dto);
        Task<bool> UpdateAsync(Guid id, UpdateItsClassDto dto);
        Task<bool> DeleteAsync(Guid id);
    }
}
