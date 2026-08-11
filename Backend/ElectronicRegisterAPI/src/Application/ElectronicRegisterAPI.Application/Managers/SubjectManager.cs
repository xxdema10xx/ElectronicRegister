using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace ElectronicRegisterAPI.Application.Managers
{
    internal class SubjectManager : ISubjectManager
    {
        private readonly ISubjectService _subjectService;
        private readonly ISubjectRepository _subjectRepository;
        private readonly ITeacherRepository _teacherRepository;

        public SubjectManager(ISubjectRepository subjectRepository, ITeacherRepository teacherRepository, ISubjectService subjectService)
        {
            _subjectRepository = subjectRepository;
            _teacherRepository = teacherRepository;
            _subjectService = subjectService;
        }

        public async Task<int> CountAsync(ClaimsContext caller)
        {
            if (caller.Role == UserRole.Teacher)
                return await _subjectRepository.CountAsync(caller.TeacherId!.Value);

            return await _subjectRepository.CountAsync();
        }

        public async Task<List<SubjectDto>> GetAllAsync(ClaimsContext caller)
        {
            var subjects = caller.Role == UserRole.Teacher
                ? await _subjectRepository.GetAllAsync(caller.TeacherId!.Value)
                : await _subjectRepository.GetAllAsync();

            return await MapToDtosAsync(subjects);
        }

        public async Task<SubjectDto?> GetByIdAsync(Guid id, ClaimsContext caller)
        {
            var subject = await _subjectRepository.GetByIdAsync(id);
            if (subject == null) return null;
            if (caller.Role == UserRole.Teacher && subject.TeacherId != caller.TeacherId) return null;
            return await MapToDtoAsync(subject);
        }

        public async Task<SubjectDto?> GetSubjectByNameAsync(string name, ClaimsContext caller)
        {
            var subject = await _subjectRepository.GetByNameAsync(name);
            if (subject == null) return null;
            if (caller.Role == UserRole.Teacher && subject.TeacherId != caller.TeacherId) return null;
            return await MapToDtoAsync(subject);
        }
        public async Task<List<SubjectDto>> GetSubjectsByTeacherIdAsync(Guid teacherId, ClaimsContext caller)
        { 
            var teacher = await _teacherRepository.GetByIdAsync(teacherId);
            if (teacher == null) return new List<SubjectDto>();
            var subjects = await _subjectRepository.GetAllAsync(teacherId);
            if (caller.Role == UserRole.Teacher && caller.TeacherId != teacherId) return new List<SubjectDto>();
            return await MapToDtosAsync(subjects);
        }

        public async Task<bool> UpdateAsync(Guid id, UpdateSubjectDto dto)
        {
            var subject = await _subjectRepository.GetByIdAsync(id);
            if (subject == null) return false;

            var teacher = await _teacherRepository.GetByIdAsync(dto.TeacherId);
            if (teacher == null) return false;

            subject.Name = dto.Name;
            subject.TeacherId = dto.TeacherId;
            await _subjectRepository.UpdateAsync(subject);
            return true;
        }

        public async Task<Guid?> AddAsync(CreateSubjectDto dto)
        { 
            await _subjectService.EnsureNameIsAvailableAsync(dto.Name);

            var teacher = await _teacherRepository.GetByIdAsync(dto.TeacherId);
            if (teacher == null) return null;

            var subject = new Subject
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                TeacherId = dto.TeacherId
            };

            await _subjectRepository.AddAsync(subject);
            return subject.Id;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var subject = await _subjectRepository.GetByIdAsync(id);
            if (subject == null) return false;

            await _subjectService.EnsureSubjectCanBeDeletedAsync(id);

            await _subjectRepository.DeleteAsync(subject);
            return true;
        }

        private async Task<SubjectDto> MapToDtoAsync(Subject subject)
        {
            var teacher = await _teacherRepository.GetByIdAsync(subject.TeacherId);
            return new SubjectDto
            {
                Id = subject.Id,
                Name = subject.Name,
                TeacherId = subject.TeacherId,
                TeacherFirstName = teacher?.FirstName,
                TeacherLastName = teacher?.LastName
            };
        }

        private async Task<List<SubjectDto>> MapToDtosAsync(List<Subject> subjects)
        {
            var teacherIds = subjects.Select(s => s.TeacherId).Distinct().ToList();
            var teachers = (await _teacherRepository.GetByIdsAsync(teacherIds)).ToDictionary(t => t.Id);

            return subjects.Select(s =>
            {
                teachers.TryGetValue(s.TeacherId, out var teacher);
                return new SubjectDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    TeacherId = s.TeacherId,
                    TeacherFirstName = teacher?.FirstName,
                    TeacherLastName = teacher?.LastName
                };
            }).ToList();
        }
    }
}
