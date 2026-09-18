using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Application.Managers
{
    internal class BienniumManager : IBienniumManager
    {
        private readonly IBienniumService _bienniumService;

        private readonly IBienniumRepository _bienniumRepository;

        public BienniumManager(IBienniumService bienniumService, IBienniumRepository bienniumRepository)
        {
            _bienniumService = bienniumService;
            _bienniumRepository = bienniumRepository;
        }

        public async Task<int> CountAsync()
        {
            return await _bienniumRepository.CountAsync();
        }

        public async Task<List<BienniumDto>> GetAllAsync()
        {
            var biennia = await _bienniumRepository.GetAllAsync();
            return MapToDtos(biennia);
        }

        public async Task<List<BienniumDto>> GetActiveBienniaAsync()
        {
            var activeBiennia = await _bienniumRepository.GetActiveBienniaAsync();
            return MapToDtos(activeBiennia);
        }

        public async Task<BienniumDto?> GetByIdAsync(Guid id)
        {
            var biennium = await _bienniumRepository.GetByIdAsync(id);
            return biennium == null ? null : MapToDto(biennium);
        }

        public async Task<BienniumDto?> GetBienniumByStartYearAsync(int startYear)
        {
            var biennium = await _bienniumRepository.GetBienniumByStartYearAsync(startYear);
            return biennium == null ? null : MapToDto(biennium);
        }

        public async Task<BienniumDto?> GetBienniumByEndYearAsync(int endYear)
        {
            var biennium = await _bienniumRepository.GetBienniumByEndYearAsync(endYear);
            return biennium == null ? null : MapToDto(biennium);
        }

        public async Task<bool> AddAsync(CreateBienniumDto dto)
        {
            _bienniumService.EnsureValidBienniumValue(dto.StartYear, dto.EndYear);
            var biennium = new Biennium
            {
                Id = Guid.NewGuid(),
                StartYear = dto.StartYear,
                EndYear = dto.EndYear
            };
            await _bienniumRepository.AddAsync(biennium);
            return true;
        }

        public async Task<bool> UpdateAsync(Guid id, UpdateBienniumDto dto)
        {
            var biennium = await _bienniumRepository.GetByIdAsync(id);
            if (biennium == null) return false;
            biennium.StartYear = dto.StartYear;
            biennium.EndYear = dto.EndYear;
            await _bienniumRepository.UpdateAsync(biennium);
            return true;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var biennium = await _bienniumRepository.GetByIdAsync(id);
            if (biennium == null) return false;
            await _bienniumRepository.DeleteAsync(biennium);
            return true;
        }

        private BienniumDto MapToDto(Biennium biennium)
        {
            return new BienniumDto
            {
                Id = biennium.Id,
                StartYear = biennium.StartYear,
                EndYear = biennium.EndYear
            };
        }

        private List<BienniumDto> MapToDtos(List<Biennium> biennia)
        {
            if(biennia == null || biennia.Count == 0)
            {
                return new List<BienniumDto>();
            }

            return biennia.Select(b => new BienniumDto
            {
                Id = b.Id,
                StartYear = b.StartYear,
                EndYear = b.EndYear
            }).ToList();
        }
    }
}
