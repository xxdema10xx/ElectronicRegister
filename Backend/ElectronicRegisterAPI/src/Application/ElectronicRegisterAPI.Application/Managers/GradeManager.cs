using ElectronicRegisterAPI.Application.Mappers;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Policies;

namespace ElectronicRegisterAPI.Application.Managers;

internal class GradeManager
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
        IGradeService gradeService
    )
    {
        _gradeRepository = gradeRepository;
        _subjectRepository = subjectRepository;
        _studentRepository = studentRepository;
        _classSubjectRepository = classSubjectRepository;
        _gradeService = gradeService;
    }

    public async Task<int> CountAsync(ExamPrincipal caller)
    {
        var studentId = caller.IsStudent ? caller.StudentId : null;
        var teacherId = (caller.IsTeacher && !caller.CombinedRead) ? caller.TeacherId : null;
        return await _gradeRepository.CountAsync(teacherId, studentId);
    }

    public async Task<List<GradeDto>> GetAllAsync(ExamPrincipal caller)
    {
        var studentId = caller.IsStudent ? caller.StudentId : null;
        var teacherId = (caller.IsTeacher && !caller.CombinedRead) ? caller.TeacherId : null;
        var grades = await _gradeRepository.GetAllAsync(teacherId, studentId);
        return await MapToDtosAsync(grades);
    }

    public async Task<GradeDto?> GetByIdAsync(Guid id, ExamPrincipal caller)
    {
        var grade = await _gradeRepository.GetByIdAsync(id);
        if (grade is null)
            return null;
        if (caller.IsStudent && grade.StudentId != caller.StudentId)
        {
            return null;
        }

        if ((caller.IsTeacher && !caller.CombinedRead))
        {
            var classSubject = await _classSubjectRepository.GetByIdAsync(grade.ClassSubjectId);
            if (classSubject is null || classSubject.TeacherId != caller.TeacherId)
            {
                return null;
            }
        }

        var dto = await MapToDtosAsync(new List<Grade> { grade });
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
        if (pageNumber < 1)
            pageNumber = 1;
        if (pageSize < 1 || pageSize > 100)
            pageSize = 20;
        Guid? restrictToStudentId = caller.IsStudent ? caller.StudentId : null;
        Guid? restrictToTeacherId = (caller.IsTeacher && !caller.CombinedRead) ? caller.TeacherId : null;
        var(grades, totalCount) = await _gradeRepository.GetPagedAsync(pageNumber, pageSize, subjectId, studentId, date, restrictToStudentId, restrictToTeacherId);
        var items = await MapToDtosAsync(grades);
        return new GradePageDto
        {
            Items = items,
            TotalCount = totalCount
        };
    }

    public async Task<GradeStatisticsDto?> GetStatisticsAsync(ExamPrincipal caller)
    {
        Guid? teacherId = (caller.IsTeacher && !caller.CombinedRead) ? caller.TeacherId : null;
        Guid? studentId = caller.IsStudent ? caller.StudentId : null;
        var hasAnyGrade = await _gradeRepository.CountAsync(teacherId, studentId) > 0;
        if (!hasAnyGrade)
            return null;
        var statistics = await _gradeRepository.GetStatisticsAsync(teacherId, studentId);
        return new GradeStatisticsDto
        {
            // Averages of whole grades are reported with two decimals, rounded away from zero.
            YearlyAverage = Math.Round(statistics.YearlyAverage, 2, MidpointRounding.AwayFromZero),
            MonthlyAverage = statistics.MonthlyAverage.Select(a => a.HasValue ? Math.Round(a.Value, 2, MidpointRounding.AwayFromZero) : (decimal? )null).ToArray()
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
        var grades = await _gradeRepository.GetAllAsync(teacherId, studentId);
        var classSubjectIds = grades.Select(g => g.ClassSubjectId).Distinct().ToList();
        var classSubjects = await _classSubjectRepository.GetByIdsAsync(classSubjectIds);
        var subjectIds = classSubjects.Select(cs => cs.SubjectId).Distinct().ToList();
        var studentIds = grades.Select(g => g.StudentId).Distinct().ToList();
        var subjects = await _subjectRepository.GetByIdsAsync(subjectIds);
        var students = await _studentRepository.GetByIdsAsync(studentIds);
        return new GradeFiltersDto
        {
            Subjects = subjects.Select(s => new SubjectDto { Id = s.Id, Name = s.Name }).OrderBy(s => s.Name).ToList(),
            Students = students.Select(s => new StudentDto { Id = s.Id, FirstName = s.FirstName, LastName = s.LastName }).OrderBy(s => s.LastName).ThenBy(s => s.FirstName).ToList()
        };
    }

    public async Task<Guid?> AddAsync(CreateGradeDto dto, ExamPrincipal caller)
    {
        _gradeService.EnsureValidGradeValue(dto.Value);
        var description = GradeRules.NormalizeDescription(dto.Description);
        var student = await _studentRepository.GetByIdAsync(dto.StudentId);
        if (student is null)
            return null;
        if (!student.ClassId.HasValue)
            throw new BusinessRuleException("Lo studente non è assegnato a una classe.");
        var classSubject = await _classSubjectRepository.GetAsync(student.ClassId.Value, dto.SubjectId);
        if (classSubject is null)
            throw new BusinessRuleException("La materia non è assegnata alla classe dello studente.");
        await _gradeService.EnsureStudentBelongsToClassSubjectAsync(student, classSubject);
        if ((caller.IsTeacher && !caller.CombinedRead))
        {
            if (!caller.TeacherId.HasValue)
                throw new UnauthorizedAccessException();
            await _gradeService.EnsureTeacherTeachesClassSubjectAsync(caller.TeacherId.Value, classSubject.Id);
        }

        var grade = new Grade
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            ClassSubjectId = classSubject.Id,
            Value = dto.Value,
            Date = dto.Date,
            Description = description
        };
        await _gradeRepository.AddAsync(grade);
        return grade.Id;
    }

    /// <summary>
    /// Creates manual grades for one class-subject and date, all or nothing. A replay of the same
    /// EventId with the same payload returns the stored grades; a different payload is a conflict.
    /// Must run inside the caller's transaction.
    /// </summary>
    public async Task<(GradeBatchResultDto Result, Guid ClassSubjectId, bool Replayed)> AddBatchAsync(CreateGradeBatchDto dto, ExamPrincipal caller)
    {
        var description = ValidateBatchShape(dto);
        var classSubject = await ResolveBatchClassSubjectAsync(dto, caller);
        if (await ReplayBatchAsync(dto, classSubject.Id, description) is { } replay)
            return (replay, classSubject.Id, true);
        var items = dto.Grades!;
        var students = (await _studentRepository.GetByIdsAsync(items.Select(i => i.StudentId).Distinct())).ToDictionary(s => s.Id);
        var errors = new List<GradeBatchRowErrorDto>();
        var seen = new HashSet<Guid>();
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var row = index;
            GradeBatchRowErrorDto Error(string code, string message) => new() { Index = row, StudentId = item.StudentId, Code = code, Message = message };
            if (!seen.Add(item.StudentId))
                errors.Add(Error("duplicate_student", "Lo studente compare più di una volta nel blocco."));
            // Missing and other-institution students are indistinguishable: the scoped read returns neither.
            else if (!students.TryGetValue(item.StudentId, out var student))
                errors.Add(Error("student_not_found", "Lo studente non esiste."));
            else if (student.ClassId != classSubject.ClassId)
                errors.Add(Error("student_not_in_class", "Lo studente non appartiene alla classe della materia."));
            if (!GradeRules.IsValidValue(item.Value))
                errors.Add(Error("invalid_value", GradeRules.InvalidValueMessage));
        }

        if (errors.Count > 0)
            throw new GradeBatchValidationException(errors);
        var grades = items.Select(item => new Grade { Id = Guid.NewGuid(), StudentId = item.StudentId, ClassSubjectId = classSubject.Id, Value = item.Value, Date = dto.Date, Description = description, BatchEventId = dto.EventId }).ToList();
        await _gradeRepository.AddRangeAsync(grades);
        return (ToBatchResult(dto.EventId, grades), classSubject.Id, false);
    }

    /// <summary>Returns the stored result when EventId was already used; throws on a different payload.</summary>
    public async Task<GradeBatchResultDto?> ReplayBatchAsync(CreateGradeBatchDto dto, Guid classSubjectId, string? description)
    {
        var stored = await _gradeRepository.GetByBatchEventAsync(dto.EventId);
        if (stored.Count == 0)
            return null;
        var requested = dto.Grades!;
        var byStudent = stored.ToDictionary(g => g.StudentId);
        var same = stored.Count == requested.Count && requested.All(i => byStudent.TryGetValue(i.StudentId, out var g) && g.Value == i.Value) && stored.All(g => g.ClassSubjectId == classSubjectId && g.Date == dto.Date && g.Description == description);
        if (!same)
            throw new BusinessRuleException("EventId già usato per un blocco di voti diverso.");
        return ToBatchResult(dto.EventId, requested.Select(i => byStudent[i.StudentId]).ToList());
    }

    /// <summary>Checks the request shape and returns the normalized description.</summary>
    public static string? ValidateBatchShape(CreateGradeBatchDto dto)
    {
        if (dto.EventId == Guid.Empty)
            throw new ArgumentException("EventId obbligatorio.");
        if (dto.Grades is null || dto.Grades.Count is < 1 or > GradeRules.MaxBatchSize)
            throw new ArgumentException($"Il blocco deve contenere da 1 a {GradeRules.MaxBatchSize} voti.");
        if (dto.Date == default)
            throw new ArgumentException("Data del voto obbligatoria.");
        if (dto.Grades.Any(g => g is null))
            throw new ArgumentException("Riga del blocco mancante.");
        return GradeRules.NormalizeDescription(dto.Description);
    }

    public async Task<ClassSubject> ResolveBatchClassSubjectAsync(CreateGradeBatchDto dto, ExamPrincipal caller)
    {
        var teacher = caller.IsTeacher && !caller.CombinedRead;
        if (teacher && !caller.TeacherId.HasValue)
            throw new UnauthorizedAccessException();
        ClassSubject? classSubject;
        if (dto.ClassSubjectId is { } classSubjectId && classSubjectId != Guid.Empty)
        {
            classSubject = await _classSubjectRepository.GetByIdAsync(classSubjectId);
            if (classSubject is not null && (dto.SubjectId is { } s && s != classSubject.SubjectId || dto.ClassId is { } c && c != classSubject.ClassId))
                throw new ArgumentException("ClassSubjectId non coerente con SubjectId o ClassId.");
        }
        else if (dto.SubjectId is { } subjectId && subjectId != Guid.Empty)
        {
            if (dto.ClassId is { } classId && classId != Guid.Empty)
                classSubject = await _classSubjectRepository.GetAsync(classId, subjectId);
            else
            {
                var candidates = await _classSubjectRepository.GetBySubjectAsync(subjectId, teacher ? caller.TeacherId : null);
                if (candidates.Count > 1)
                    throw new ArgumentException("La materia è assegnata a più classi: indicare classId o classSubjectId.");
                classSubject = candidates.SingleOrDefault();
            }
        }
        else
            throw new ArgumentException("Indicare classSubjectId oppure subjectId.");
        if (classSubject is null)
            throw new KeyNotFoundException("La risorsa non esiste.");
        if (teacher)
            await _gradeService.EnsureTeacherTeachesClassSubjectAsync(caller.TeacherId!.Value, classSubject.Id);
        return classSubject;
    }

    private static GradeBatchResultDto ToBatchResult(Guid eventId, IEnumerable<Grade> grades) => new() { EventId = eventId, Created = grades.Select(g => new GradeBatchCreatedDto { StudentId = g.StudentId, GradeId = g.Id }).ToList() };

    public async Task<bool> UpdateAsync(Guid id, UpdateGradeDto dto, ExamPrincipal caller)
    {
        var grade = await _gradeRepository.GetByIdAsync(id);
        if (grade is null)
            return false;
        _gradeService.EnsureValidGradeValue(dto.Value);
        if ((caller.IsTeacher && !caller.CombinedRead))
        {
            if (!caller.TeacherId.HasValue)
                throw new UnauthorizedAccessException();
            await _gradeService.EnsureTeacherOwnsGradeAsync(caller.TeacherId.Value, grade);
        }

        var student = await _studentRepository.GetByIdAsync(grade.StudentId);
        if (student is null)
            return false;
        if (!student.ClassId.HasValue)
            throw new BusinessRuleException("Lo studente non è assegnato a una classe.");
        var classSubject = await _classSubjectRepository.GetAsync(student.ClassId.Value, dto.SubjectId);
        if (classSubject is null)
            throw new BusinessRuleException("La materia non è assegnata alla classe dello studente.");
        await _gradeService.EnsureStudentBelongsToClassSubjectAsync(student, classSubject);
        if ((caller.IsTeacher && !caller.CombinedRead))
        {
            await _gradeService.EnsureTeacherTeachesClassSubjectAsync(caller.TeacherId!.Value, classSubject.Id);
        }

        if (await _gradeRepository.IsLinkedToExamSubmissionAsync(id))
            throw new BusinessRuleException("An exam grade must be managed through submission finalization.");
        if (caller.Tenant is { } tenant && tenant.Roles.Contains(InstitutionRole.Staff) && !tenant.Roles.Contains(InstitutionRole.InstitutionAdmin) && classSubject.Id != grade.ClassSubjectId)
            throw new BusinessRuleException("ClassSubjectId non modificabile dallo Staff.");
        grade.ClassSubjectId = classSubject.Id;
        grade.Value = dto.Value;
        grade.Date = dto.Date;
        if (dto.Description is not null)
            grade.Description = GradeRules.NormalizeDescription(dto.Description);
        await _gradeRepository.UpdateAsync(grade);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var grade = await _gradeRepository.GetByIdAsync(id);
        if (grade is null)
            return false;
        if (await _gradeRepository.IsLinkedToExamSubmissionAsync(id))
            throw new BusinessRuleException("An exam grade cannot be deleted through the grade endpoint.");
        await _gradeRepository.DeleteAsync(grade);
        return true;
    }

    private async Task<List<GradeDto>> MapToDtosAsync(List<Grade> grades)
    {
        if (grades.Count == 0)
            return new List<GradeDto>();
        var classSubjectIds = grades.Select(g => g.ClassSubjectId).Distinct().ToList();
        var studentIds = grades.Select(g => g.StudentId).Distinct().ToList();
        var classSubjects = (await _classSubjectRepository.GetByIdsAsync(classSubjectIds)).ToDictionary(cs => cs.Id);
        var subjectIds = classSubjects.Values.Select(cs => cs.SubjectId).Distinct().ToList();
        var subjects = (await _subjectRepository.GetByIdsAsync(subjectIds)).ToDictionary(s => s.Id);
        var students = (await _studentRepository.GetByIdsAsync(studentIds)).ToDictionary(s => s.Id);
        return GradeMapper.ToDtos(grades, classSubjects, subjects, students);
    }

}
