using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Application.Managers
{
    internal class SubjectManager : ISubjectManager
    {
        private readonly ISubjectService _subjectService;
        private readonly ISubjectRepository _subjectRepository;
        private readonly IStudentRepository _studentRepository;
        private readonly IClassSubjectRepository _classSubjectRepository;

        public SubjectManager(
            ISubjectRepository subjectRepository, 
            ISubjectService subjectService, 
            IClassSubjectRepository classSubjectRepository, 
            IStudentRepository studentRepository)
        {
            _subjectRepository = subjectRepository;
            _subjectService = subjectService;
            _classSubjectRepository = classSubjectRepository;
            _studentRepository = studentRepository;
        }

        public async Task<int> CountAsync(ClaimsContext caller)
        {
            if (caller.Role == UserRole.Admin) return await _subjectRepository.CountAsync();

            var subjects = await GetVisibleSubjectsAsync(caller);

            return subjects.Count;
        }

        public async Task<List<SubjectDto>> GetAllAsync(ClaimsContext caller)
        {
            var subjects = await GetVisibleSubjectsAsync(caller);
            return await MapToDtosAsync(subjects);
        }

        public async Task<SubjectDto?> GetByIdAsync(Guid id, ClaimsContext caller)
        {
            var subject = await _subjectRepository.GetByIdAsync(id);
            if (caller.Role == UserRole.Admin) return subject is null ? null : MapToDto(subject);

            var visibleSubjects = await GetVisibleSubjectsAsync(caller);

            subject = visibleSubjects.FirstOrDefault(s => s.Id == id);

            return subject is null ? null : MapToDto(subject);
        }

        public async Task<SubjectDto?> GetSubjectByNameAsync(string name, ClaimsContext caller)
        {
            Subject? subject;

            if (caller.Role == UserRole.Admin)
            {
                subject = await _subjectRepository.GetByNameAsync(name);
                return subject is null ? null : MapToDto(subject);
            }

            var visibleSubjects = await GetVisibleSubjectsAsync(caller);

            subject = visibleSubjects.FirstOrDefault(
                s =>string.Equals(s.Name,name,StringComparison.OrdinalIgnoreCase)
            );

            return subject is null ? null : MapToDto(subject);
        }

        public async Task<List<SubjectDto>>GetSubjectsByTeacherIdAsync(Guid teacherId, ClaimsContext caller)
        {
            if (caller.Role == UserRole.Teacher && caller.TeacherId != teacherId)
            {
                return new List<SubjectDto>();
            }

            var assignments = await _classSubjectRepository.GetByTeacherIdAsync(teacherId);

            var subjectIds = assignments
                .Select(cs => cs.SubjectId)
                .Distinct()
                .ToList();

            var subjects = await _subjectRepository.GetByIdsAsync(subjectIds);

            return subjects
                .Select(s => new SubjectDto
                {
                    Id = s.Id,
                    Name = s.Name
                })
                .OrderBy(s => s.Name)
                .ToList();
        }
        public async Task<bool> UpdateAsync(Guid id, UpdateSubjectDto dto)
        {
            var subject = await _subjectRepository.GetByIdAsync(id);

            if (subject is null) return false;

            subject.Name = dto.Name;

            await _subjectRepository.UpdateAsync(subject);

            return true;
        }

        public async Task<Guid?> AddAsync(CreateSubjectDto dto)
        {
            await _subjectService.EnsureNameIsAvailableAsync(dto.Name);

            var subject = new Subject
            {
                Id = Guid.NewGuid(),
                Name = dto.Name
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

        private async Task<List<Subject>> GetVisibleSubjectsAsync(ClaimsContext caller)
        {
            if (caller.Role == UserRole.Teacher)
            {
                if (!caller.TeacherId.HasValue)
                    return new List<Subject>();

                var classSubjects = await _classSubjectRepository.GetByTeacherIdAsync(caller.TeacherId.Value);

                var subjectIds = classSubjects
                    .Select(cs => cs.SubjectId)
                    .Distinct()
                    .ToList();

                if (subjectIds.Count == 0) return new List<Subject>();

                return await _subjectRepository.GetByIdsAsync(subjectIds);
            }

            if (caller.Role == UserRole.Student)
            {
                if (!caller.StudentId.HasValue) return new List<Subject>();

                var student = await _studentRepository.GetByIdAsync(caller.StudentId.Value);

                if (student is null || !student.ClassId.HasValue) return new List<Subject>();

                var classSubjects = await _classSubjectRepository.GetByClassIdAsync(student.ClassId.Value);

                var subjectIds = classSubjects
                    .Select(cs => cs.SubjectId)
                    .Distinct()
                    .ToList();

                if (subjectIds.Count == 0) return new List<Subject>();

                return await _subjectRepository.GetByIdsAsync(subjectIds);
            }

            return await _subjectRepository.GetAllAsync();
        }

        private static SubjectDto MapToDto(Subject subject)
        {
            return new SubjectDto
            {
                Id = subject.Id,
                Name = subject.Name
            };
        }

        private async Task<List<SubjectDto>> MapToDtosAsync(List<Subject> subjects)
        {
            return subjects.Select(s =>
            {
                return new SubjectDto
                {
                    Id = s.Id,
                    Name = s.Name
                };
            }).ToList();
        }
    }
}
