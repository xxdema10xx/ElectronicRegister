using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Application.Managers
{
    internal class GradeManager : IGradeManager
    {
        private readonly IGradeRepository _gradeRepository;
        private readonly ISubjectRepository _subjectRepository;
        private readonly ITeacherRepository _teacherRepository;
        private readonly IStudentRepository _studentRepository;
        private readonly IGradeService _gradeService;

        public GradeManager(
            IGradeRepository gradeRepository,
            ISubjectRepository subjectRepository,
            ITeacherRepository teacherRepository,
            IStudentRepository studentRepository,
            IGradeService gradeService)
        {
            _gradeRepository = gradeRepository;
            _subjectRepository = subjectRepository;
            _teacherRepository = teacherRepository;
            _studentRepository = studentRepository;
            _gradeService = gradeService;
        }

        public async Task<List<GradeDto>> GetAllAsync(ClaimsContext caller)
        {
            var studentId = caller.Role == UserRole.Student
                ? caller.StudentId
                : null;

            var teacherId = caller.Role == UserRole.Teacher
                ? caller.TeacherId
                : null;

            var grades = await _gradeRepository.GetAllAsync(
                teacherId,
                studentId);

            var studentIds = grades
                .Select(g => g.StudentId)
                .Distinct()
                .ToList();

            var students = (await _studentRepository.GetByIdsAsync(studentIds))
                .ToDictionary(s => s.Id);

            var subjectIds = grades
                .Select(g => g.SubjectId)
                .Distinct()
                .ToList();

            var subjects = (await _subjectRepository.GetByIdsAsync(subjectIds))
                .ToDictionary(s => s.Id);

            return grades.Select(g => new GradeDto
            {
                Id = g.Id,
                StudentId = g.StudentId,
                SubjectId = g.SubjectId,
                SubjectName = subjects.TryGetValue(g.SubjectId, out var subject)
                    ? subject.Name
                    : null,
                TeacherId = g.TeacherId,
                Value = g.Value,
                Date = g.Date,
                Student = students.TryGetValue(g.StudentId, out var student)
                    ? new StudentDto
                    {
                        Id = student.Id,
                        FirstName = student.FirstName,
                        LastName = student.LastName
                    }
                    : null
            }).ToList();
        }

        public async Task<GradeDto?> GetByIdAsync(Guid id, ClaimsContext caller)
        {
            var grade = await _gradeRepository.GetByIdAsync(id);
            if (grade is null) return null;

            if (caller.Role == UserRole.Student && grade.StudentId != caller.StudentId) return null;
            if (caller.Role == UserRole.Teacher && grade.TeacherId != caller.TeacherId) return null;

            var student = await _studentRepository.GetByIdAsync(grade.StudentId);
            var subject = await _subjectRepository.GetByIdAsync(grade.SubjectId);
            return new GradeDto
            {
                Id = grade.Id,
                StudentId = grade.StudentId,
                SubjectId = grade.SubjectId,
                SubjectName = subject?.Name,
                TeacherId = grade.TeacherId,
                Value = grade.Value,
                Date = grade.Date,
                Student = student != null ? new StudentDto { Id = student.Id, FirstName = student.FirstName, LastName = student.LastName } : null
            };
        }

        public async Task<GradePageDto> GetPagedAsync(
            int pageNumber, 
            int pageSize, 
            Guid? subjectId, 
            Guid? studentId, 
            DateOnly? date, 
            ClaimsContext caller
        )
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            Guid? restrictToStudentId = caller.Role == UserRole.Student ? caller.StudentId : null;
            Guid? restrictToTeacherId = caller.Role == UserRole.Teacher ? caller.TeacherId : null;

            var (grades, totalCount) = await _gradeRepository.GetPagedAsync(
                pageNumber, pageSize, subjectId, studentId, date, restrictToStudentId, restrictToTeacherId);

            var items = await MapToDtosAsync(grades);

            return new GradePageDto { Items = items, TotalCount = totalCount };
        }

        public async Task<GradeStatisticsDto?> GetStatisticsAsync(ClaimsContext caller)
        {
            // Implementation for getting grade statistics
            if (caller.Role == UserRole.Student)
            {
                var statistics = await _gradeRepository.GetStatisticsAsync(null, caller.StudentId!.Value);
                return new GradeStatisticsDto
                {
                    YearlyAverage = statistics.YearlyAverage,
                    MonthlyAverage = statistics.MonthlyAverage
                };
            }
            if (caller.Role == UserRole.Teacher)
            {
                var statistics = await _gradeRepository.GetStatisticsAsync(caller.TeacherId!.Value, null);
                return new GradeStatisticsDto
                {
                    YearlyAverage = statistics.YearlyAverage,
                    MonthlyAverage = statistics.MonthlyAverage
                };
            }
        
            var statisticsAll = await _gradeRepository.GetStatisticsAsync(null, null);
            return new GradeStatisticsDto
            {
                YearlyAverage = statisticsAll.YearlyAverage,
                MonthlyAverage = statisticsAll.MonthlyAverage
            };
        }

        public async Task<GradeFiltersDto> GetFiltersAsync(ClaimsContext caller)
        {
            Guid? teacherId = null;
            Guid? studentId = null;

            if (caller.Role == UserRole.Teacher)
            {
                teacherId = caller.TeacherId;
            }
            else if (caller.Role == UserRole.Student)
            {
                studentId = caller.StudentId;
            }

            var grades = await _gradeRepository.GetAllAsync(
                teacherId,
                studentId);

            var subjectIds = grades
                .Select(g => g.SubjectId)
                .Distinct()
                .ToList();

            var studentIds = grades
                .Select(g => g.StudentId)
                .Distinct()
                .ToList();

            var subjects = await _subjectRepository.GetByIdsAsync(subjectIds);
            var students = await _studentRepository.GetByIdsAsync(studentIds);
            var teachers = await _teacherRepository.GetAllAsync();

            var teachersById = teachers.ToDictionary(t => t.Id);

            return new GradeFiltersDto
            {
                Subjects = subjects
                    .Select(s =>
                    {
                        teachersById.TryGetValue(s.TeacherId, out var teacher);

                        return new SubjectDto
                        {
                            Id = s.Id,
                            Name = s.Name,
                            TeacherId = s.TeacherId,
                            TeacherFirstName = teacher?.FirstName,
                            TeacherLastName = teacher?.LastName
                        };
                    })
                    .OrderBy(s => s.Name)
                    .ToList(),

                Students = students
                    .Select(s => new StudentDto
                    {
                        Id = s.Id,
                        FirstName = s.FirstName,
                        LastName = s.LastName
                    })
                    .OrderBy(s => s.LastName)
                    .ThenBy(s => s.FirstName)
                    .ToList()
            };
        }

        public async Task<List<GradeDto>> GetGradesByStudentIdAsync(Guid studentId)
        {
            // Implementation for getting grades by student ID
            var student = await _studentRepository.GetByIdAsync(studentId);
            if(student is null) return new List<GradeDto>();
            var grades = await _gradeRepository.GetByStudentIdAsync(studentId);
            var subjectIds = grades.Select(g => g.SubjectId).Distinct().ToList();
            var subjects = await _subjectRepository.GetByIdsAsync(subjectIds);
            var subjectDictionary = subjects.ToDictionary(s => s.Id);
            return grades.Select(
                    g => new GradeDto
                    {
                        Id = g.Id,
                        StudentId = g.StudentId,
                        SubjectId = g.SubjectId,
                        SubjectName = subjectDictionary.TryGetValue(g.SubjectId, out var subject) ? subject.Name : null,
                        TeacherId = g.TeacherId,
                        Value = g.Value,
                        Date = g.Date,
                        Student = new StudentDto { 
                            Id = student.Id, 
                            FirstName = student.FirstName, 
                            LastName = student.LastName
                        }
                    }
                ).ToList();
        }

        public async Task<List<GradeDto>?> GetGradesBySubjectNameAsync(string subjectName, ClaimsContext caller)
        {
            Guid? studentId = caller.Role == UserRole.Student ? caller.StudentId : null;
            Guid? teacherId = caller.Role == UserRole.Teacher ? caller.TeacherId : null;

            var grades = await _gradeRepository.GetBySubjectNameAsync(subjectName, studentId, teacherId);
            if (grades.Count == 0) return new List<GradeDto>();

            var subjectIds = grades.Select(g => g.SubjectId).Distinct().ToList();
            var studentIds = grades.Select(g => g.StudentId).Distinct().ToList();

            var students = (await _studentRepository.GetByIdsAsync(studentIds)).ToDictionary(s => s.Id);
            var subjects = (await _subjectRepository.GetByIdsAsync(subjectIds)).ToDictionary(s => s.Id);

            return grades.Select(g => new GradeDto
            {
                Id = g.Id,
                StudentId = g.StudentId,
                SubjectId = g.SubjectId,
                SubjectName = subjects.TryGetValue(g.SubjectId, out var subj) ? subj.Name : null,
                TeacherId = g.TeacherId,
                Value = g.Value,
                Date = g.Date,
                Student = students.TryGetValue(g.StudentId, out var stud)
                    ? new StudentDto { Id = stud.Id, FirstName = stud.FirstName, LastName = stud.LastName }
                    : null
            }).ToList();
        }

        public async Task<List<GradeDto>> GetGradesByDateAsync(DateOnly date, ClaimsContext caller)
        {
            Guid? studentId = caller.Role == UserRole.Student ? caller.StudentId : null;
            Guid? teacherId = caller.Role == UserRole.Teacher ? caller.TeacherId : null;

            var grades = await _gradeRepository.GetByDateAsync(date, studentId, teacherId);
            if (grades.Count == 0) return grades.Select(s => new GradeDto()).ToList(); // lista vuota, il Controller decide se fare 404

            var subjectIds = grades.Select(g => g.SubjectId).Distinct().ToList();
            var studentIds = grades.Select(g => g.StudentId).Distinct().ToList();

            var subjects = (await _subjectRepository.GetByIdsAsync(subjectIds)).ToDictionary(s => s.Id);
            var students = (await _studentRepository.GetByIdsAsync(studentIds)).ToDictionary(s => s.Id);

            return grades.Select(g => new GradeDto
            {
                Id = g.Id,
                StudentId = g.StudentId,
                SubjectId = g.SubjectId,
                SubjectName = subjects.TryGetValue(g.SubjectId, out var subj) ? subj.Name : null,
                TeacherId = g.TeacherId,
                Value = g.Value,
                Date = g.Date,
                Student = students.TryGetValue(g.StudentId, out var stud)
                    ? new StudentDto { Id = stud.Id, FirstName = stud.FirstName, LastName = stud.LastName }
                    : null
            }).ToList();
        }

        public async Task<Guid?> AddAsync(CreateGradeDto dto, ClaimsContext caller)
        {
            _gradeService.EnsureValidGradeValue(dto.Value);

            if (caller.Role == UserRole.Teacher)
                await _gradeService.EnsureTeacherTeachesSubjectAsync(caller.TeacherId!.Value, dto.SubjectId);

            var subject = await _subjectRepository.GetByIdAsync(dto.SubjectId);
            if (subject is null) return null;

            var grade = new Grade 
            {
                Id = Guid.NewGuid(),
                Value = dto.Value,
                Date = dto.Date,
                SubjectId = dto.SubjectId,
                StudentId = dto.StudentId,
                TeacherId = caller.Role == UserRole.Teacher ? caller.TeacherId!.Value : subject.TeacherId
            };
            await _gradeRepository.AddAsync(grade);
            return grade.Id;
        }

        public async Task<bool> UpdateAsync(Guid id, UpdateGradeDto dto, ClaimsContext caller)
        {
            var grade = await _gradeRepository.GetByIdAsync(id);
            if (grade is null) return false;

            if (caller.Role == UserRole.Teacher)
            {
                await _gradeService.EnsureTeacherOwnsGradeAsync(caller.TeacherId!.Value, grade);
                await _gradeService.EnsureTeacherTeachesSubjectAsync(caller.TeacherId!.Value, dto.SubjectId);
            }

            _gradeService.EnsureValidGradeValue(dto.Value);

            var subject = await _subjectRepository.GetByIdAsync(dto.SubjectId);
            if (subject is null) return false;

            grade.Value = dto.Value;
            grade.Date = dto.Date;
            grade.SubjectId = dto.SubjectId;

            await _gradeRepository.UpdateAsync(grade);
            return true;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var grade = await _gradeRepository.GetByIdAsync(id);
            if (grade is null) return false;

            await _gradeRepository.DeleteAsync(grade);
            return true;
        }

        private async Task<List<GradeDto>> MapToDtosAsync(List<Grade> grades)
        {
            var subjectIds = grades.Select(g => g.SubjectId).Distinct().ToList();
            var studentIds = grades.Select(g => g.StudentId).Distinct().ToList();

            var subjects = (await _subjectRepository.GetByIdsAsync(subjectIds)).ToDictionary(s => s.Id);
            var students = (await _studentRepository.GetByIdsAsync(studentIds)).ToDictionary(s => s.Id);

            return grades.Select(g => new GradeDto
            {
                Id = g.Id,
                StudentId = g.StudentId,
                SubjectId = g.SubjectId,
                SubjectName = subjects.TryGetValue(g.SubjectId, out var subj) ? subj.Name : null,
                TeacherId = g.TeacherId,
                Value = g.Value,
                Date = g.Date,
                Student = students.TryGetValue(g.StudentId, out var stud)
                    ? new StudentDto { Id = stud.Id, FirstName = stud.FirstName, LastName = stud.LastName }
                    : null
            }).ToList();
        }
    }
}