using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Application.Managers
{
    internal class ItsClassManager : IItsClassManager
    {
        private readonly IItsClassRepository _itsClassRepository;
        private readonly IItsClassService _itsClassService;
        public ItsClassManager(IItsClassRepository itsClassRepository, IItsClassService itsClassService)
        {
            _itsClassRepository = itsClassRepository;
            _itsClassService = itsClassService;
        }
        public async Task<int> CountAsync()
        {
            return await _itsClassRepository.CountAsync();
        }
        public async Task<List<ItsClassDto>> GetAllAsync()
        {
            var itsClasses = await _itsClassRepository.GetAllAsync();
            return itsClasses.Select(MapToDto).ToList();
        }
        public async Task<ItsClassDto?> GetByIdAsync(Guid id)
        {
            var itsClass = await _itsClassRepository.GetByIdAsync(id);
            if (itsClass is null) return null;
            return MapToDto(itsClass);
        }
        public async Task<List<ItsClassDto>> GetByIdsAsync(IEnumerable<Guid> ids)
        {
            var itsClasses = await _itsClassRepository.GetByIdsAsync(ids);
            return itsClasses.Select(MapToDto).ToList();
        }
        public async Task<ItsClassDto?> GetByNameAsync(string name)
        {
            var itsClass = await _itsClassRepository.GetByNameAsync(name);
            if (itsClass is null) return null;
            return MapToDto(itsClass);
        }
        public async Task<bool> AddAsync(CreateItsClassDto dto)
        {
            var itsClass = new ItsClass
            {
                Id = Guid.NewGuid(),
                Name = dto.Name
            };
            await _itsClassRepository.AddAsync(itsClass);
            return true;
        }
        public async Task<bool> UpdateAsync(Guid id, UpdateItsClassDto dto)
        {
            var itsClass = await _itsClassRepository.GetByIdAsync(id);
            if (itsClass is null) return false;
            itsClass.Name = dto.Name;
            await _itsClassRepository.UpdateAsync(itsClass);
            return true;
        }
        public async Task<bool> DeleteAsync(Guid id)
        {
            var itsClass = await _itsClassRepository.GetByIdAsync(id);
            if (itsClass is null) return false;
            await _itsClassRepository.DeleteAsync(itsClass);
            return true;
        }
        private static ItsClassDto MapToDto(ItsClass itsClass)
        {
            return new ItsClassDto
            {
                Id = itsClass.Id,
                Name = itsClass.Name,
                BienniumStudyPathId = itsClass.BienniumStudyPathId,
            };
        }
    }
}
