internal class GradeManager : IGradeManager
{
    private readonly IGradeRepository _gradeRepository;
    private readonly ISubjectRepository _subjectRepository;
    private readonly ITeacherRepository _teacherRepository;
    private readonly IGradeService _gradeService;

    public GradeManager(
        IGradeRepository gradeRepository,
        ISubjectRepository subjectRepository,
        ITeacherRepository teacherRepository,
        IGradeService gradeService)
    {
        _gradeRepository = gradeRepository;
        _subjectRepository = subjectRepository;
        _teacherRepository = teacherRepository;
        _gradeService = gradeService;
    }

    public async Task<List<GradeDto>> GetAllAsync(ClaimsContext caller)
    {
        // Implementation for getting all grades
    }

    public async Task<GradeDto?> GetByIdAsync(Guid id, ClaimsContext caller)
    {
        // Implementation for getting a grade by ID
    }

    public async Task<GradePageDto> GetPagedAsync(int pageNumber, int pageSize, Guid? subjectId, Guid? studentId, DateOnly? date, ClaimsContext caller)
    {
        // Implementation for getting paged grades
    }

    public async Task<GradeStatisticsDto?> GetStatisticsAsync(ClaimsContext caller)
    {
        // Implementation for getting grade statistics
    }

    public async Task<GradeFiltersDto> GetFiltersAsync(ClaimsContext caller)
    {
        // Implementation for getting grade filters
    }

    public async Task<List<GradeDto>> GetGradesByStudentIdAsync(Guid studentId)
    {
        // Implementation for getting grades by student ID
    }

    public async Task<List<GradeDto>?> GetGradesBySubjectNameAsync(string subjectName, ClaimsContext caller)
    {
        // Implementation for getting grades by subject name
    }
    
    public async Task<List<GradeDto>> GetGradesByDateAsync(DateOnly date, ClaimsContext caller)
    {
        // Implementation for getting grades by date
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
            StudentId = dto.StudentId
        };
        await _gradeRepository.AddAsync(grade);
        return grade.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateGradeDto dto, ClaimsContext caller)
    {
        // Implementation for updating a grade
        _gradeService.EnsureGradeExists(id);

        var grade = await _gradeRepository.GetByIdAsync(id);

        _gradeService.EnsureValidGradeValue(dto.Value);
      
        if(caller.Role == UserRole.Teacher)
            await _gradeService.EnsureTeacherTeachesSubjectAsync(caller.TeacherId!.Value, dto.SubjectId);

        var subject = await _subjectRepository.GetByIdAsync(dto.SubjectId); 
        if (subject is null) return null;

        grade.Value = dto.Value;
        grade.Date = dto.Date;
        grade.SubjectId = dto.SubjectId;
        grade.StudentId = dto.StudentId;
        await _gradeRepository.UpdateAsync(grade);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        // Implementation for deleting a grade
    }
}