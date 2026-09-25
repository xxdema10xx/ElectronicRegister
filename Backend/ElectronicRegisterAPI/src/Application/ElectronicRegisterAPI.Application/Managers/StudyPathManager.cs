using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Application.Managers
{
    internal class StudyPathManager : IStudyPathManager
    {
        private readonly IStudyPathRepository _studyPathRepository;
        private readonly IStudyPathService _studyPathService;

        public StudyPathManager(IStudyPathRepository repository, IStudyPathService service)
        {
            _studyPathRepository = repository;
            _studyPathService = service;
        }

        public async Task<int> CountAsync()
        {
            return await _studyPathRepository.CountAsync();
        }
        public async Task<List<StudyPathDto>> GetAllAsync()
        {
            var studyPaths = await _studyPathRepository.GetAllAsync();
            return studyPaths.Select(MapToDto).ToList();
        }
        public async Task<StudyPathDto?> GetByIdAsync(Guid id)
        {
            var studyPath = await _studyPathRepository.GetByIdAsync(id);
            return studyPath == null ? null : MapToDto(studyPath);
        }
        public async Task<StudyPathDto?> GetStudyPathByNameAsync(string name)
        {
            var studyPath = await _studyPathRepository.GetByNameAsync(name);
            return studyPath == null ? null : MapToDto(studyPath);
        }
        public async Task<bool> AddAsync(CreateStudyPathDto dto)
        {
            await _studyPathService.StudyPathAlreadyExistsByNameAsync(dto.Name);
            _studyPathService.EnsureValidStudyPathName(dto.Name);
            _studyPathService.EnsureValidStudyPathDescription(dto.Description);
            var studyPath = new StudyPath
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                Description = dto.Description
            };
            await _studyPathRepository.AddAsync(studyPath);
            return true;
        }
        public async Task<bool> UpdateAsync(Guid id, UpdateStudyPathDto dto)
        {
            var studyPath = await _studyPathService.EnsureValidStudyPathIdAsync(id);

            _studyPathService.EnsureValidStudyPathName(dto.Name);
            _studyPathService.EnsureValidStudyPathDescription(dto.Description);
            
            studyPath.Name = dto.Name;
            studyPath.Description = dto.Description;

            await _studyPathRepository.UpdateAsync(studyPath);
            return true;
        }
        public async Task<bool> DeleteAsync(Guid id)
        {
            var studyPath = await _studyPathService.EnsureStudyPathCanBeDeletedAsync(id);
            await _studyPathRepository.DeleteAsync(studyPath);
            return true;
        }

        private StudyPathDto MapToDto(StudyPath model)
        {
            return new StudyPathDto
            {
                Id = model.Id,
                Name = model.Name,
                Description = model.Description
            };
        }
    }
}
