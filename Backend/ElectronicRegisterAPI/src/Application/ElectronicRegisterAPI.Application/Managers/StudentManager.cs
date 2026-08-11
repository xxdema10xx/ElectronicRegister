using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Application.Managers
{
    internal class StudentManager : IStudentManager
    {
        private readonly IStudentRepository _studentRepository;
        private readonly IStudentService _studentService;
        public StudentManager(IStudentRepository studentRepository, IStudentService studentService)
        {
            _studentRepository = studentRepository;
            _studentService = studentService;
        }

        public async Task<int> CountAsync()
        {
            return await _studentRepository.CountAsync();
        }

        public async Task<List<StudentDto>> GetAllAsync()
        { 
            var students = await _studentRepository.GetAllAsync();
            return students.Select(MapToDto).ToList();
        }

        public async Task<StudentDto?> GetByIdAsync(Guid id)
        {
            var student = await _studentRepository.GetByIdAsync(id);
            if (student is null) return null;

            return MapToDto(student);
        }
        public async Task<List<StudentDto>> GetStudentsByLastNameAsync(string lastName)
        {
            var students = await _studentRepository.GetByLastNameAsync(lastName);
            return students.Select(MapToDto).ToList();
        }
        public async Task<bool> UpdateAsync(Guid id, UpdateStudentDto dto)
        {
            var student = await _studentRepository.GetByIdAsync(id);
            if (student is null) return false;

            student.FirstName = dto.FirstName;
            student.LastName = dto.LastName;

            await _studentRepository.UpdateAsync(student);
            return true;
        }
        public async Task<bool> AddAsync(CreateStudentDto dto)
        {
            var student = new Student
            {
                Id = Guid.NewGuid(),
                FirstName = dto.FirstName,
                LastName = dto.LastName
            };

            await _studentRepository.AddAsync(student);
            return true;
        }
        public async Task<bool> DeleteAsync(Guid id)
        {
            await _studentService.EnsureStudentCanBeDeletedAsync(id);

            var student = await _studentRepository.GetByIdAsync(id);
            if (student is null) return false;

            await _studentRepository.DeleteAsync(student);
            return true;
        }

        private StudentDto MapToDto(Student student)
        {
            return new StudentDto
            {
                Id = student.Id,
                FirstName = student.FirstName,
                LastName = student.LastName
            };
        }
    }
}
