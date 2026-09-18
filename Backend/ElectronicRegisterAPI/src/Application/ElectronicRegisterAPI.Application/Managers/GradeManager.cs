using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Application.Managers
{
    internal class GradeManager : IGradeManager
    {
        private readonly IGradeRepository _gradeRepository;
        private readonly ISubjectRepository _subjectRepository;
        private readonly IStudentRepository _studentRepository;
        private readonly IGradeService _gradeService;
        private readonly IClassSubjectRepository _classSubjectRepository;

        public GradeManager(
            IGradeRepository gradeRepository,
            ISubjectRepository subjectRepository,
            IStudentRepository studentRepository,
            IClassSubjectRepository classSubjectRepository,
            IGradeService gradeService)
        {
            _gradeRepository = gradeRepository;
            _subjectRepository = subjectRepository;
            _studentRepository = studentRepository;
            _classSubjectRepository = classSubjectRepository;
            _gradeService = gradeService;
        }

        public async Task<int> CountAsync(ClaimsContext caller)
        {
            var studentId = caller.Role == UserRole.Student
                ? caller.StudentId
                : null;
            var teacherId = caller.Role == UserRole.Teacher
                ? caller.TeacherId
                : null;
            return await _gradeRepository.CountAsync(teacherId, studentId);
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

            return await MapToDtosAsync(grades);
        }

        public async Task<GradeDto?> GetByIdAsync(Guid id, ClaimsContext caller)
        {
            var grade = await _gradeRepository.GetByIdAsync(id);

            if (grade is null)
                return null;

            if (caller.Role == UserRole.Student &&
                grade.StudentId != caller.StudentId)
            {
                return null;
            }

            if (caller.Role == UserRole.Teacher)
            {
                var classSubject =
                    await _classSubjectRepository.GetByIdAsync(
                        grade.ClassSubjectId);

                if (classSubject is null ||
                    classSubject.TeacherId != caller.TeacherId)
                {
                    return null;
                }
            }

            var dto = await MapToDtosAsync(
                new List<Grade> { grade });

            return dto.FirstOrDefault();
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
            Guid? teacherId = caller.Role == UserRole.Teacher ? caller.TeacherId : null;
            Guid? studentId = caller.Role == UserRole.Student ? caller.StudentId : null;

            var hasAnyGrade = await _gradeRepository.CountAsync(teacherId, studentId) > 0;
            if (!hasAnyGrade) return null;

            var statistics = await _gradeRepository.GetStatisticsAsync(teacherId, studentId);
            return new GradeStatisticsDto { 
                YearlyAverage = statistics.YearlyAverage, 
                MonthlyAverage = statistics.MonthlyAverage 
            };
        }

        public async Task<GradeFiltersDto> GetFiltersAsync(ClaimsContext caller)
        {
            Guid? teacherId = null;
            Guid? studentId = null;

            if (caller.Role == UserRole.Teacher)
                teacherId = caller.TeacherId;
            else if (caller.Role == UserRole.Student)
                studentId = caller.StudentId;

            var grades = await _gradeRepository.GetAllAsync(
                teacherId,
                studentId);

            var classSubjectIds = grades
                .Select(g => g.ClassSubjectId)
                .Distinct()
                .ToList();

            var classSubjects =
                await _classSubjectRepository.GetByIdsAsync(
                    classSubjectIds);

            var subjectIds = classSubjects
                .Select(cs => cs.SubjectId)
                .Distinct()
                .ToList();

            var studentIds = grades
                .Select(g => g.StudentId)
                .Distinct()
                .ToList();

            var subjects =
                await _subjectRepository.GetByIdsAsync(subjectIds);

            var students =
                await _studentRepository.GetByIdsAsync(studentIds);

            return new GradeFiltersDto
            {
                Subjects = subjects
                    .Select(s => new SubjectDto
                    {
                        Id = s.Id,
                        Name = s.Name
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

        public async Task<List<GradeDto>>GetGradesByStudentIdAsync(Guid studentId)
        {
            var student =
                await _studentRepository.GetByIdAsync(studentId);

            if (student is null)
                return new List<GradeDto>();

            var grades =
                await _gradeRepository.GetByStudentIdAsync(studentId);

            return await MapToDtosAsync(grades);
        }

        public async Task<List<GradeDto>?>GetGradesBySubjectNameAsync(string subjectName, ClaimsContext caller)
        {
            Guid? studentId =
                caller.Role == UserRole.Student
                    ? caller.StudentId
                    : null;

            Guid? teacherId =
                caller.Role == UserRole.Teacher
                    ? caller.TeacherId
                    : null;

            var grades =
                await _gradeRepository.GetBySubjectNameAsync(
                    subjectName,
                    studentId,
                    teacherId);

            return await MapToDtosAsync(grades);
        }

        public async Task<List<GradeDto>>GetGradesByDateAsync(DateOnly date, ClaimsContext caller)
        {
            Guid? studentId =
                caller.Role == UserRole.Student
                    ? caller.StudentId
                    : null;

            Guid? teacherId =
                caller.Role == UserRole.Teacher
                    ? caller.TeacherId
                    : null;

            var grades =
                await _gradeRepository.GetByDateAsync(
                    date,
                    studentId,
                    teacherId);

            return await MapToDtosAsync(grades);
        }

        public async Task<Guid?> AddAsync(CreateGradeDto dto, ClaimsContext caller)
        {
            _gradeService.EnsureValidGradeValue(dto.Value);

            var student =
                await _studentRepository.GetByIdAsync(dto.StudentId);

            if (student is null)
                return null;

            if (!student.ClassId.HasValue)
                throw new BusinessRuleException(
                    "Lo studente non è assegnato a una classe.");

            var classSubject =
                await _classSubjectRepository.GetAsync(
                    student.ClassId.Value,
                    dto.SubjectId);

            if (classSubject is null)
                throw new BusinessRuleException(
                    "La materia non è assegnata alla classe dello studente.");

            await _gradeService
                .EnsureStudentBelongsToClassSubjectAsync(
                    student,
                    classSubject);

            if (caller.Role == UserRole.Teacher)
            {
                if (!caller.TeacherId.HasValue)
                    throw new UnauthorizedAccessException();

                await _gradeService
                    .EnsureTeacherTeachesClassSubjectAsync(
                        caller.TeacherId.Value,
                        classSubject.Id);
            }

            var grade = new Grade
            {
                Id = Guid.NewGuid(),
                StudentId = student.Id,
                ClassSubjectId = classSubject.Id,
                Value = dto.Value,
                Date = dto.Date
            };

            await _gradeRepository.AddAsync(grade);

            return grade.Id;
        }

        public async Task<bool> UpdateAsync(Guid id, UpdateGradeDto dto, ClaimsContext caller)
        {
            var grade =
                await _gradeRepository.GetByIdAsync(id);

            if (grade is null)
                return false;

            _gradeService.EnsureValidGradeValue(dto.Value);

            if (caller.Role == UserRole.Teacher)
            {
                if (!caller.TeacherId.HasValue)
                    throw new UnauthorizedAccessException();

                await _gradeService.EnsureTeacherOwnsGradeAsync(
                    caller.TeacherId.Value,
                    grade);
            }

            var student =
                await _studentRepository.GetByIdAsync(
                    grade.StudentId);

            if (student is null)
                return false;

            if (!student.ClassId.HasValue)
                throw new BusinessRuleException(
                    "Lo studente non è assegnato a una classe.");

            var classSubject =
                await _classSubjectRepository.GetAsync(
                    student.ClassId.Value,
                    dto.SubjectId);

            if (classSubject is null)
                throw new BusinessRuleException(
                    "La materia non è assegnata alla classe dello studente.");

            await _gradeService
                .EnsureStudentBelongsToClassSubjectAsync(
                    student,
                    classSubject);

            if (caller.Role == UserRole.Teacher)
            {
                await _gradeService
                    .EnsureTeacherTeachesClassSubjectAsync(
                        caller.TeacherId!.Value,
                        classSubject.Id);
            }

            grade.ClassSubjectId = classSubject.Id;
            grade.Value = dto.Value;
            grade.Date = dto.Date;

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
            if (grades.Count == 0)
                return new List<GradeDto>();

            var classSubjectIds = grades
                .Select(g => g.ClassSubjectId)
                .Distinct()
                .ToList();

            var studentIds = grades
                .Select(g => g.StudentId)
                .Distinct()
                .ToList();

            var classSubjects = (await _classSubjectRepository
                    .GetByIdsAsync(classSubjectIds))
                .ToDictionary(cs => cs.Id);

            var subjectIds = classSubjects.Values
                .Select(cs => cs.SubjectId)
                .Distinct()
                .ToList();

            var subjects = (await _subjectRepository
                    .GetByIdsAsync(subjectIds))
                .ToDictionary(s => s.Id);

            var students = (await _studentRepository
                    .GetByIdsAsync(studentIds))
                .ToDictionary(s => s.Id);

            var result = new List<GradeDto>();

            foreach (var grade in grades)
            {
                if (!classSubjects.TryGetValue(
                        grade.ClassSubjectId,
                        out var classSubject))
                {
                    throw new InvalidOperationException(
                        $"ClassSubject {grade.ClassSubjectId} non trovato per il voto {grade.Id}.");
                }

                subjects.TryGetValue(
                    classSubject.SubjectId,
                    out var subject);

                students.TryGetValue(
                    grade.StudentId,
                    out var student);

                result.Add(new GradeDto
                {
                    Id = grade.Id,
                    StudentId = grade.StudentId,

                    SubjectId = classSubject.SubjectId,
                    SubjectName = subject?.Name,

                    TeacherId = classSubject.TeacherId,

                    Value = grade.Value,
                    Date = grade.Date,

                    Student = student is null
                        ? null
                        : new StudentDto
                        {
                            Id = student.Id,
                            FirstName = student.FirstName,
                            LastName = student.LastName
                        }
                });
            }

            return result;
        }
    }
}