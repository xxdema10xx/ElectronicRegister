using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Application.Managers
{
    internal class TeacherManager : ITeacherManager
    {
        private readonly ITeacherRepository _teacherRepository;
        private readonly ITeacherService _teacherService;

        public TeacherManager(ITeacherRepository teacherRepository, ITeacherService teacherService)
        {
            _teacherRepository = teacherRepository;
            _teacherService = teacherService;
        }

        public async Task<int> CountAsync()
        {
            return await _teacherRepository.CountAsync();
        }

        public async Task<List<TeacherDto>> GetAllAsync()
        {
            var teachers = await _teacherRepository.GetAllAsync();
            return teachers.Select(MapToDto).ToList();
        }

        public async Task<TeacherDto?> GetByIdAsync(Guid id)
        {
            var teacher = await _teacherRepository.GetByIdAsync(id);
            return teacher == null ? null : MapToDto(teacher);
        }

        public async Task<bool> UpdateAsync(Guid id, UpdateTeacherDto dto)
        {
            var teacher = await _teacherRepository.GetByIdAsync(id);
            if (teacher == null) return false;

            await _teacherRepository.UpdateAsync(teacher);
            return true;
        }

        public async Task<Guid?> AddAsync(CreateTeacherDto dto)
        {
            var teacher = new Teacher
            {
                Id = Guid.NewGuid(),
                FirstName = dto.FirstName,
                LastName = dto.LastName
            };
            await _teacherRepository.AddAsync(teacher);
            return teacher.Id;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            await _teacherService.EnsureTeacherCanBeDeletedAsync(id);

            var teacher = await _teacherRepository.GetByIdAsync(id);
            if (teacher == null) return false;

            await _teacherRepository.DeleteAsync(teacher);
            return true;
        }

        private TeacherDto MapToDto(Teacher teacher)
        {
            return new TeacherDto
            {
                Id = teacher.Id,
                FirstName = teacher.FirstName,
                LastName = teacher.LastName
            };
        }
    }
}
