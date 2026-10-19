using ElectronicRegisterAPI.Application.Mappers;
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

        public async Task<int> CountAsync(ExamPrincipal caller)
        {
            var studentId = caller.IsStudent
                ? caller.StudentId
                : null;
            var teacherId = (caller.IsTeacher && !caller.CombinedRead)
                ? caller.TeacherId
                : null;
            return await _gradeRepository.CountAsync(teacherId, studentId);
        }
        public async Task<List<GradeDto>> GetAllAsync(ExamPrincipal caller)
        {
            var studentId = caller.IsStudent
                ? caller.StudentId
                : null;

            var teacherId = (caller.IsTeacher && !caller.CombinedRead)
                ? caller.TeacherId
                : null;

            var grades = await _gradeRepository.GetAllAsync(
                teacherId,
                studentId);

            return await MapToDtosAsync(grades);
        }

        public async Task<GradeDto?> GetByIdAsync(Guid id, ExamPrincipal caller)
        {
            var grade = await _gradeRepository.GetByIdAsync(id);

            if (grade is null)
                return null;

            if (caller.IsStudent &&
                grade.StudentId != caller.StudentId)
            {
                return null;
            }

            if ((caller.IsTeacher && !caller.CombinedRead))
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
            ExamPrincipal caller
        )
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            Guid? restrictToStudentId = caller.IsStudent ? caller.StudentId : null;
            Guid? restrictToTeacherId = (caller.IsTeacher && !caller.CombinedRead) ? caller.TeacherId : null;

            var (grades, totalCount) = await _gradeRepository.GetPagedAsync(
                pageNumber, pageSize, subjectId, studentId, date, restrictToStudentId, restrictToTeacherId);

            var items = await MapToDtosAsync(grades);

            return new GradePageDto { Items = items, TotalCount = totalCount };
        }

        public async Task<GradeStatisticsDto?> GetStatisticsAsync(ExamPrincipal caller)
        {
            Guid? teacherId = (caller.IsTeacher && !caller.CombinedRead) ? caller.TeacherId : null;
            Guid? studentId = caller.IsStudent ? caller.StudentId : null;

            var hasAnyGrade = await _gradeRepository.CountAsync(teacherId, studentId) > 0;
            if (!hasAnyGrade) return null;

            var statistics = await _gradeRepository.GetStatisticsAsync(teacherId, studentId);
            return new GradeStatisticsDto { 
                YearlyAverage = statistics.YearlyAverage, 
                MonthlyAverage = statistics.MonthlyAverage 
            };
        }

        public async Task<GradeFiltersDto> GetFiltersAsync(ExamPrincipal caller)
        {
            Guid? teacherId = null;
            Guid? studentId = null;

            if ((caller.IsTeacher && !caller.CombinedRead))
                teacherId = caller.TeacherId;
            else if (caller.IsStudent)
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

        public async Task<List<GradeDto>?>GetGradesBySubjectNameAsync(string subjectName, ExamPrincipal caller)
        {
            Guid? studentId =
                caller.IsStudent
                    ? caller.StudentId
                    : null;

            Guid? teacherId =
                (caller.IsTeacher && !caller.CombinedRead)
                    ? caller.TeacherId
                    : null;

            var grades =
                await _gradeRepository.GetBySubjectNameAsync(
                    subjectName,
                    studentId,
                    teacherId);

            return await MapToDtosAsync(grades);
        }

        public async Task<List<GradeDto>>GetGradesByDateAsync(DateOnly date, ExamPrincipal caller)
        {
            Guid? studentId =
                caller.IsStudent
                    ? caller.StudentId
                    : null;

            Guid? teacherId =
                (caller.IsTeacher && !caller.CombinedRead)
                    ? caller.TeacherId
                    : null;

            var grades =
                await _gradeRepository.GetByDateAsync(
                    date,
                    studentId,
                    teacherId);

            return await MapToDtosAsync(grades);
        }

        public async Task<Guid?> AddAsync(CreateGradeDto dto, ExamPrincipal caller)
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

            if ((caller.IsTeacher && !caller.CombinedRead))
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

        public async Task<bool> UpdateAsync(Guid id, UpdateGradeDto dto, ExamPrincipal caller)
        {
            var grade =
                await _gradeRepository.GetByIdAsync(id);

            if (grade is null)
                return false;

            _gradeService.EnsureValidGradeValue(dto.Value);

            if ((caller.IsTeacher && !caller.CombinedRead))
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

            if ((caller.IsTeacher && !caller.CombinedRead))
            {
                await _gradeService.EnsureTeacherTeachesClassSubjectAsync(
                        caller.TeacherId!.Value,
                        classSubject.Id);
            }

            if (await _gradeRepository.IsLinkedToExamSubmissionAsync(id))
                throw new BusinessRuleException("An exam grade must be managed through submission finalization.");

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

            if (await _gradeRepository.IsLinkedToExamSubmissionAsync(id))
                throw new BusinessRuleException("An exam grade cannot be deleted through the grade endpoint.");

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

            return GradeMapper.ToDtos(
                grades,
                classSubjects,
                subjects,
                students);
        }

        Task<int> IGradeManager.CountAsync(ClaimsContext caller) => CountAsync(ExamPrincipal.Legacy(caller));
        Task<List<GradeDto>> IGradeManager.GetAllAsync(ClaimsContext caller) => GetAllAsync(ExamPrincipal.Legacy(caller));
        Task<GradeDto?> IGradeManager.GetByIdAsync(Guid id, ClaimsContext caller) => GetByIdAsync(id, ExamPrincipal.Legacy(caller));
        Task<GradePageDto> IGradeManager.GetPagedAsync(int pageNumber, int pageSize, Guid? subjectId, Guid? studentId, DateOnly? date, ClaimsContext caller) => GetPagedAsync(pageNumber, pageSize, subjectId, studentId, date, ExamPrincipal.Legacy(caller));
        Task<GradeStatisticsDto?> IGradeManager.GetStatisticsAsync(ClaimsContext caller) => GetStatisticsAsync(ExamPrincipal.Legacy(caller));
        Task<GradeFiltersDto> IGradeManager.GetFiltersAsync(ClaimsContext caller) => GetFiltersAsync(ExamPrincipal.Legacy(caller));
        Task<List<GradeDto>?> IGradeManager.GetGradesBySubjectNameAsync(string subjectName, ClaimsContext caller) => GetGradesBySubjectNameAsync(subjectName, ExamPrincipal.Legacy(caller));
        Task<List<GradeDto>> IGradeManager.GetGradesByDateAsync(DateOnly date, ClaimsContext caller) => GetGradesByDateAsync(date, ExamPrincipal.Legacy(caller));
        Task<Guid?> IGradeManager.AddAsync(CreateGradeDto dto, ClaimsContext caller) => AddAsync(dto, ExamPrincipal.Legacy(caller));
        Task<bool> IGradeManager.UpdateAsync(Guid id, UpdateGradeDto dto, ClaimsContext caller) => UpdateAsync(id, dto, ExamPrincipal.Legacy(caller));
    }
}
