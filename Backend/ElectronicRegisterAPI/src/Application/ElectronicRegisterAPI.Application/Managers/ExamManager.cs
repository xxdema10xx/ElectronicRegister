using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Application.Managers;

internal class ExamManager : IExamManager
{
    private readonly IExamRepository _examRepository;
    private readonly IClassSubjectRepository _classSubjectRepository;
    private readonly ISubjectRepository _subjectRepository;
    private readonly IItsClassRepository _itsClassRepository;
    private readonly IBienniumStudyPathRepository _bienniumStudyPathRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IExamService _examService;

    public ExamManager(
        IExamRepository examRepository,
        IClassSubjectRepository classSubjectRepository,
        ISubjectRepository subjectRepository,
        IItsClassRepository itsClassRepository,
        IBienniumStudyPathRepository bienniumStudyPathRepository,
        IStudentRepository studentRepository,
        IExamService examService)
    {
        _examRepository = examRepository;
        _classSubjectRepository = classSubjectRepository;
        _subjectRepository = subjectRepository;
        _itsClassRepository = itsClassRepository;
        _bienniumStudyPathRepository = bienniumStudyPathRepository;
        _studentRepository = studentRepository;
        _examService = examService;
    }

    public async Task<int> CountAsync(ClaimsContext caller)
    {
        var exams = await FilterVisibleAsync(await _examRepository.GetAllAsync(), caller);
        return exams.Count;
    }

    public async Task<List<ExamDto>> GetAllAsync(ClaimsContext caller)
    {
        var exams = await FilterVisibleAsync(await _examRepository.GetAllAsync(), caller);
        return await MapToDtosAsync(exams);
    }

    public async Task<ExamDto?> GetByIdAsync(Guid id, ClaimsContext caller)
    {
        var exam = await _examRepository.GetByIdAsync(id);
        if (exam is null) return null;

        var visible = await FilterVisibleAsync(new List<Exam> { exam }, caller);
        if (visible.Count == 0) return null;

        return (await MapToDtosAsync(visible)).FirstOrDefault();
    }

    public async Task<List<ExamDto>> GetByDateAsync(DateOnly date, ClaimsContext caller)
    {
        var exams = await FilterVisibleAsync(await _examRepository.GetByDateAsync(date), caller);
        return await MapToDtosAsync(exams);
    }

    public async Task<Guid?> AddAsync(CreateExamDto dto, ClaimsContext caller)
    {
        var classSubject = await ResolveClassSubjectAsync(dto.ClassId, dto.SubjectId);

        if (caller.Role == UserRole.Teacher)
        {
            if (!caller.TeacherId.HasValue)
                throw new UnauthorizedAccessException();

            await _examService.EnsureTeacherTeachesClassSubjectAsync(
                caller.TeacherId.Value, classSubject.Id);
        }

        var exam = new Exam
        {
            Id = Guid.NewGuid(),
            ClassSubjectId = classSubject.Id,
            Date = dto.Date,
            IsRetake = dto.IsRetake
        };

        await _examRepository.AddAsync(exam);
        return exam.Id;
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateExamDto dto, ClaimsContext caller)
    {
        var exam = await _examRepository.GetByIdAsync(id);
        if (exam is null) return false;

        if (caller.Role == UserRole.Teacher)
        {
            if (!caller.TeacherId.HasValue)
                throw new UnauthorizedAccessException();

            await _examService.EnsureTeacherTeachesClassSubjectAsync(
                caller.TeacherId.Value, exam.ClassSubjectId);
        }

        var classSubject = await ResolveClassSubjectAsync(dto.ClassId, dto.SubjectId);

        if (caller.Role == UserRole.Teacher)
        {
            await _examService.EnsureTeacherTeachesClassSubjectAsync(
                caller.TeacherId!.Value, classSubject.Id);
        }

        exam.ClassSubjectId = classSubject.Id;
        exam.Date = dto.Date;
        exam.IsRetake = dto.IsRetake;

        await _examRepository.UpdateAsync(exam);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var exam = await _examRepository.GetByIdAsync(id);
        if (exam is null) return false;

        await _examRepository.DeleteAsync(exam);
        return true;
    }

    // Trova la ClassSubject da classe + materia ed esegue i controlli del service.
    private async Task<ClassSubject> ResolveClassSubjectAsync(Guid classId, Guid subjectId)
    {
        var classSubject = await _classSubjectRepository.GetAsync(classId, subjectId);

        if (classSubject is null)
            throw new BusinessRuleException("La materia non è assegnata alla classe specificata.");

        await _examService.EnsureClassSubjectExistsAsync(classSubject.Id);
        await _examService.EnsureClassSubjectBelongsToBienniumStudyPathAsync(classSubject.Id);

        return classSubject;
    }

    private async Task<List<Exam>> FilterVisibleAsync(List<Exam> exams, ClaimsContext caller)
    {
        if (exams.Count == 0 || caller.Role == UserRole.Admin)
            return exams;

        var classSubjects = (await _classSubjectRepository.GetByIdsAsync(
                exams.Select(e => e.ClassSubjectId).Distinct()))
            .ToDictionary(cs => cs.Id);

        if (caller.Role == UserRole.Teacher)
        {
            return exams
                .Where(e => classSubjects.TryGetValue(e.ClassSubjectId, out var cs)
                            && cs.TeacherId == caller.TeacherId)
                .ToList();
        }

        if (caller.Role == UserRole.Student && caller.StudentId.HasValue)
        {
            var student = await _studentRepository.GetByIdAsync(caller.StudentId.Value);
            if (student?.ClassId is null) return new List<Exam>();

            return exams
                .Where(e => classSubjects.TryGetValue(e.ClassSubjectId, out var cs)
                            && cs.ClassId == student.ClassId.Value)
                .ToList();
        }

        return new List<Exam>();
    }

    private async Task<List<ExamDto>> MapToDtosAsync(List<Exam> exams)
    {
        if (exams.Count == 0)
            return new List<ExamDto>();

        var classSubjects = (await _classSubjectRepository.GetByIdsAsync(
                exams.Select(e => e.ClassSubjectId).Distinct().ToList()))
            .ToDictionary(cs => cs.Id);

        var subjects = (await _subjectRepository.GetByIdsAsync(
                classSubjects.Values.Select(cs => cs.SubjectId).Distinct().ToList()))
            .ToDictionary(s => s.Id);

        var classes = (await _itsClassRepository.GetByIdsAsync(
                classSubjects.Values.Select(cs => cs.ClassId).Distinct().ToList()))
            .ToDictionary(c => c.Id);

        // "corso" = StudyPath, raggiunto dalla classe tramite BienniumStudyPath
        var studyPaths = await _bienniumStudyPathRepository.GetStudyPathsByIdsAsync(
            classes.Values.Select(c => c.BienniumStudyPathId).Distinct().ToList());

        var result = new List<ExamDto>();

        foreach (var exam in exams.OrderBy(e => e.Date))
        {
            if (!classSubjects.TryGetValue(exam.ClassSubjectId, out var classSubject))
            {
                throw new InvalidOperationException(
                    $"ClassSubject {exam.ClassSubjectId} non trovato per l'esame {exam.Id}.");
            }

            subjects.TryGetValue(classSubject.SubjectId, out var subject);
            classes.TryGetValue(classSubject.ClassId, out var itsClass);

            StudyPath? studyPath = null;
            if (itsClass is not null)
                studyPaths.TryGetValue(itsClass.BienniumStudyPathId, out studyPath);

            result.Add(new ExamDto
            {
                Id = exam.Id,
                ClassId = classSubject.ClassId,
                ClassName = itsClass?.Name,
                StudyPathId = studyPath?.Id,
                StudyPathName = studyPath?.Name,
                SubjectId = classSubject.SubjectId,
                SubjectName = subject?.Name,
                Date = exam.Date,
                IsRetake = exam.IsRetake
            });
        }

        return result;
    }
}