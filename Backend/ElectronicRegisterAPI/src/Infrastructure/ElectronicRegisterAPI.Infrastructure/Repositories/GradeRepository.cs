using ElectronicRegisterAPI.Domain.Results;
using ElectronicRegisterAPI.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using ElectronicRegisterAPI.Domain.Models;
using Grade = ElectronicRegisterAPI.Domain.Models.Grade;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using GradeEntity = ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Grade;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal class GradeRepository : IGradeRepository
{
    private readonly ElectronicRegisterContext _context;
    private readonly InstitutionExamDataScope? _scope;
    private CancellationToken Ct => _scope?.CancellationToken ?? CancellationToken.None;
    private IQueryable<T> Rows<T>()
        where T : class => _scope?.Query<T>() ?? _context.Set<T>();
    private Task SaveAsync() => _scope is null ? _context.SaveChangesAsync(Ct) : _scope.SaveAsync();
    public GradeRepository(ElectronicRegisterContext context, InstitutionExamDataScope? scope = null)
    {
        _context = context;
        _scope = scope;
    }

    public async Task<Grade?> GetByIdAsync(Guid id)
    {
        if (_scope is not null)
            await _scope.LockStaffGradeAsync(id);
        var grade = await Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Grade>().FirstOrDefaultAsync(g => g.Id == id, Ct);
        return grade == null ? null : MapToModel(grade);
    }

    public async Task<List<Grade>> GetAllAsync(Guid? teacherId, Guid? studentId)
    {
        var query = Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Grade>().AsNoTracking();
        if (teacherId.HasValue)
        {
            query = query.Where(g => g.ClassSubject.TeacherId == teacherId.Value);
        }

        if (studentId.HasValue)
        {
            query = query.Where(g => g.StudentId == studentId.Value);
        }

        return await query.Select(g => MapToModel(g)).ToListAsync(Ct);
    }

    public async Task<int> CountAsync(Guid? teacherId = null, Guid? studentId = null)
    {
        var query = Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Grade>().AsNoTracking();
        if (teacherId.HasValue)
        {
            query = query.Where(g => g.ClassSubject.TeacherId == teacherId.Value);
        }

        if (studentId.HasValue)
        {
            query = query.Where(g => g.StudentId == studentId.Value);
        }

        return await query.CountAsync(Ct);
    }

    public async Task<List<Grade>> GetByDateAsync(DateOnly date, Guid? studentId = null, Guid? teacherId = null)
    {
        var query = Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Grade>().Where(g => g.Date == date);
        if (studentId.HasValue)
            query = query.Where(g => g.StudentId == studentId.Value);
        if (teacherId.HasValue)
            query = query.Where(g => g.ClassSubject.TeacherId == teacherId.Value);
        return await query.Select(g => MapToModel(g)).ToListAsync(Ct);
    }

    public async Task<List<Grade>> GetBySubjectNameAsync(string subjectName, Guid? studentId = null, Guid? teacherId = null)
    {
        var query = Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Grade>().Where(g => g.ClassSubject.Subject.Name == subjectName);
        if (studentId.HasValue)
            query = query.Where(g => g.StudentId == studentId.Value);
        if (teacherId.HasValue)
            query = query.Where(g => g.ClassSubject.TeacherId == teacherId.Value);
        return await query.Select(g => MapToModel(g)).ToListAsync(Ct);
    }

    public async Task<(List<Grade> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Guid? subjectId,
        Guid? studentId,
        DateOnly? date,
        Guid? restrictToStudentId,
        Guid? restrictToTeacherId
    )
    {
        var query = Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Grade>().AsNoTracking();
        if (restrictToStudentId.HasValue)
            query = query.Where(g => g.StudentId == restrictToStudentId.Value);
        if (restrictToTeacherId.HasValue)
            query = query.Where(g => g.ClassSubject.TeacherId == restrictToTeacherId.Value);
        if (subjectId.HasValue)
            query = query.Where(g => g.ClassSubject.SubjectId == subjectId.Value);
        if (studentId.HasValue)
            query = query.Where(g => g.StudentId == studentId.Value);
        if (date.HasValue)
            query = query.Where(g => g.Date == date.Value);
        var offset = (long)(pageNumber - 1) * pageSize;
        if (pageNumber < 1 || pageSize is < 1 or > 100 || offset > int.MaxValue)
            throw new ArgumentException("Paginazione non valida.");
        var totalCount = await query.CountAsync(Ct);
        var items = await query.OrderByDescending(g => g.Date).ThenBy(g => g.ClassSubjectId).ThenBy(g => g.Value).ThenBy(g => g.StudentId).ThenBy(g => g.Id).Skip((int)offset).Take(pageSize).Select(g => new Grade { Id = g.Id, StudentId = g.StudentId, ClassSubjectId = g.ClassSubjectId, Value = g.Value, Date = g.Date }).ToListAsync(Ct);
        return (items, totalCount);
    }

    public async Task<GradeStatistics> GetStatisticsAsync(Guid? teacherId, Guid? studentId)
    {
        var query = Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Grade>().AsNoTracking();
        if (teacherId.HasValue)
            query = query.Where(g => g.ClassSubject.TeacherId == teacherId.Value);
        if (studentId.HasValue)
            query = query.Where(g => g.StudentId == studentId.Value);
        if (_scope is not null)
            query = _scope.CurrentCareerGrades(query);
        var yearlyAverage = await query.Select(g => (decimal? )g.Value).AverageAsync(Ct) ?? 0;
        var monthlyData = await query.GroupBy(g => g.Date.Month).Select(g => new { Month = g.Key, Average = g.Average(x => x.Value) }).ToListAsync(Ct);
        var monthlyAverage = new decimal? [12];
        foreach (var item in monthlyData)
        {
            monthlyAverage[item.Month - 1] = item.Average;
        }

        return new GradeStatistics
        {
            YearlyAverage = yearlyAverage,
            MonthlyAverage = monthlyAverage
        };
    }

    public async Task<List<Grade>> GetByStudentIdAsync(Guid studentId)
    {
        return await Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Grade>().Where(g => g.StudentId == studentId).Select(g => MapToModel(g)).ToListAsync(Ct);
    }

    public async Task AddAsync(Grade grade)
    {
        var gradeEntity = new GradeEntity
        {
            Id = grade.Id,
            StudentId = grade.StudentId,
            ClassSubjectId = grade.ClassSubjectId,
            Value = grade.Value,
            Date = grade.Date
        };
        await _context.Grades.AddAsync(gradeEntity);
        await SaveAsync();
    }

    public async Task UpdateAsync(Grade grade)
    {
        var gradeEntity = await MapToEntity(grade);
        if (gradeEntity == null)
            return;
        _context.Grades.Update(gradeEntity);
        await SaveAsync();
    }

    public async Task DeleteAsync(Grade grade)
    {
        var gradeEntity = await MapToEntity(grade);
        if (gradeEntity == null)
            return;
        _context.Grades.Remove(gradeEntity);
        await SaveAsync();
    }

    public async Task<bool> ExistsForStudentAsync(Guid studentId)
    {
        return await Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Grade>().AnyAsync(g => g.StudentId == studentId, Ct);
    }

    public Task<bool> IsLinkedToExamSubmissionAsync(Guid gradeId) => Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.ExamSubmission>().AnyAsync(s => s.GradeId == gradeId, Ct);
    private static Grade MapToModel(GradeEntity gradeEntity)
    {
        return new Grade
        {
            Id = gradeEntity.Id,
            StudentId = gradeEntity.StudentId,
            ClassSubjectId = gradeEntity.ClassSubjectId,
            Value = gradeEntity.Value,
            Date = gradeEntity.Date
        };
    }

    private async Task<GradeEntity?> MapToEntity(Grade grade)
    {
        var entity = await Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Grade>().FirstOrDefaultAsync(g => g.Id == grade.Id, Ct);
        if (entity is null)
            return null;
        entity.Id = grade.Id;
        entity.StudentId = grade.StudentId;
        entity.ClassSubjectId = grade.ClassSubjectId;
        entity.Value = grade.Value;
        entity.Date = grade.Date;
        return entity;
    }
}
