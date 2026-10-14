using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Application.Managers
{
    internal class StudyAreaManager : IStudyAreaManager
    {
        private readonly IStudyAreaService _studyAreaService;
        private readonly IStudyAreaRepository _studyAreaRepository;

        public StudyAreaManager(IStudyAreaService studyAreaService, IStudyAreaRepository studyAreaRepository)
        {
            _studyAreaService = studyAreaService;
            _studyAreaRepository = studyAreaRepository;
        }

        public async Task<int> CountAsync()
        {
            return await _studyAreaRepository.CountAsync();
        }

        public async Task<List<StudyAreaDto>> GetAllAsync()
        {
            var studyAreas = await _studyAreaRepository.GetAllAsync();
            return MapToDtos(studyAreas);
        }

        public async Task<StudyAreaDto?> GetByIdAsync(Guid id)
        {
            var studyArea = await _studyAreaService.EnsureStudyAreaExistsAsync(id);
            return MapToDto(studyArea);
        }

        public async Task<StudyAreaDto?> GetStudyAreaByNameAsync(string name)
        {
            var studyArea = await _studyAreaRepository.GetStudyAreaByNameAsync(name);
            return studyArea == null ? null : MapToDto(studyArea);
        }

        public async Task<bool> AddAsync(CreateStudyAreaDto dto)
        {
            _studyAreaService.EnsureValidStudyAreaName(dto.Name);
            _studyAreaService.EnsureValidStudyAreaDescription(dto.Description);

            var studyArea = new StudyArea
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                Description = dto.Description
            };
            await _studyAreaRepository.AddAsync(studyArea);
            return true;
        }

        public async Task<bool> UpdateAsync(Guid id, UpdateStudyAreaDto dto)
        {
            await _studyAreaService.EnsureStudyAreaExistsAsync(id);
            _studyAreaService.EnsureValidStudyAreaName(dto.Name);
            _studyAreaService.EnsureValidStudyAreaDescription(dto.Description);
            var studyArea = new StudyArea
            {
                Id = id,
                Name = dto.Name,
                Description = dto.Description
            };
            await _studyAreaRepository.UpdateAsync(studyArea);
            return true;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var studyArea = await _studyAreaService.EnsureStudyAreaCanBeDeletedAsync(id);
            await _studyAreaRepository.DeleteAsync(studyArea);
            return true;
        }

        private StudyAreaDto MapToDto(StudyArea studyArea)
        {
            return new StudyAreaDto
            {
                Id = studyArea.Id,
                Name = studyArea.Name,
                Description = studyArea.Description
            };
        }

        private List<StudyAreaDto> MapToDtos(List<StudyArea> studyAreas)
        {
            return studyAreas.Select(MapToDto).ToList();
        }
    }
}
