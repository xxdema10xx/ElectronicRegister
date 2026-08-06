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

    public GradeRepository(ElectronicRegisterContext context)
    {
        _context = context;
    }

    public async Task<Grade?> GetByIdAsync(Guid id)
    {
        var grade = await _context.Grades.FirstOrDefaultAsync(g => g.Id == id);
        return grade == null ? null : MapToModel(grade);
    }

    public async Task<List<Grade>> GetAllAsync(Guid? teacherId, Guid? studentId)
    {
        var query = _context.Grades.AsQueryable();
        if (teacherId.HasValue)
        {
            query = query.Where(g => g.TeacherId == teacherId.Value);
        }
        if (studentId.HasValue)
        {
            query = query.Where(g => g.StudentId == studentId.Value);
        }
        return await query.Select(g => MapToModel(g)).ToListAsync();
    }

    public async Task<int> CountAsync(Guid? teacherId = null, Guid? studentId = null)
    {
        var query = _context.Grades.AsQueryable();
        if (teacherId.HasValue)
        {
            query = query.Where(g => g.TeacherId == teacherId.Value);
        }
        if (studentId.HasValue)
        {
            query = query.Where(g => g.StudentId == studentId.Value);
        }
        return await query.CountAsync();
    }

    public async Task<List<Grade>> GetByDateAsync(DateOnly date, Guid? studentId = null, Guid? teacherId = null)
    {
        var query = _context.Grades.Where(g => g.Date == date);

        if (studentId.HasValue) query = query.Where(g => g.StudentId == studentId.Value);
        if (teacherId.HasValue) query = query.Where(g => g.TeacherId == teacherId.Value);

        return await query.Select(g => MapToModel(g)).ToListAsync();
    }

    public async Task<List<Grade>> GetBySubjectNameAsync(string subjectName, Guid? studentId = null, Guid? teacherId = null)
    {
        var query = _context.Grades
            .Include(g => g.Subject)
            .Where(g => g.Subject.Name == subjectName);
        if (studentId.HasValue) query = query.Where(g => g.StudentId == studentId.Value);
        if (teacherId.HasValue) query = query.Where(g => g.TeacherId == teacherId.Value);
        return await query.Select(g => MapToModel(g)).ToListAsync();
    }

    public async Task<(List<Grade> Items, int TotalCount)> GetPagedAsync(
        int pageNumber, int pageSize,
        Guid? subjectId, Guid? studentId, DateOnly? date,
        Guid? restrictToStudentId, Guid? restrictToTeacherId)
    {
        var query = _context.Grades.AsQueryable();

        if (restrictToStudentId.HasValue) query = query.Where(g => g.StudentId == restrictToStudentId.Value);
        if (restrictToTeacherId.HasValue) query = query.Where(g => g.TeacherId == restrictToTeacherId.Value);
        if (subjectId.HasValue) query = query.Where(g => g.SubjectId == subjectId.Value);
        if (studentId.HasValue) query = query.Where(g => g.StudentId == studentId.Value);
        if (date.HasValue) query = query.Where(g => g.Date == date.Value);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(g => g.Date).ThenBy(g => g.SubjectId).ThenBy(g => g.Value).ThenBy(g => g.StudentId)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(g => new Grade
            {
                Id = g.Id,
                StudentId = g.StudentId,
                SubjectId = g.SubjectId,
                TeacherId = g.TeacherId,
                Value = g.Value,
                Date = g.Date
            })
            .ToListAsync();

        return (items, totalCount);
    }
    public async Task<GradeStatistics> GetStatisticsAsync(Guid? teacherId, Guid? studentId)
    {
        var query = _context.Grades.AsQueryable();

        if(teacherId.HasValue) query = query.Where(g => g.TeacherId == teacherId.Value);
        
        if(studentId.HasValue) query = query.Where(g => g.StudentId == studentId.Value);

        var yearlyAverage = await query.Select(g => (decimal?)g.Value).AverageAsync() ?? 0;

        var monthlyData = await query
                .GroupBy(g => g.Date.Month)
                .Select(g => new
                {
                    Month = g.Key,
                    Average = g.Average(x => x.Value)
                })
                .ToListAsync();

        var monthlyAverage = new decimal?[12];

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
        return await _context.Grades
            .Where(g => g.StudentId == studentId)
            .Select(g => MapToModel(g)).ToListAsync();
    }

    public async Task AddAsync(Grade grade)
    {
        var gradeEntity = MapToEntity(grade);
        await _context.Grades.AddAsync(gradeEntity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Grade grade)
    {
        var gradeEntity = MapToEntity(grade);
        _context.Grades.Update(gradeEntity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Grade grade)
    {
        var gradeEntity = MapToEntity(grade);
        _context.Grades.Remove(gradeEntity);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> ExistsForStudentAsync(Guid studentId)
    {
        return await _context.Grades.AnyAsync(g => g.StudentId == studentId);
    }

    public async Task<bool> ExistsForSubjectAsync(Guid subjectId)
    {
        return await _context.Grades.AnyAsync(g => g.SubjectId == subjectId);
    }

    public async Task<bool> ExistsForTeacherAsync(Guid teacherId)
    {
        return await _context.Grades.AnyAsync(g => g.TeacherId == teacherId);
    }

    private static Grade MapToModel(GradeEntity gradeEntity)
    {
        return new Grade
        {
            Id = gradeEntity.Id,
            StudentId = gradeEntity.StudentId,
            SubjectId = gradeEntity.SubjectId,
            TeacherId = gradeEntity.TeacherId,
            Value = gradeEntity.Value,
            Date = gradeEntity.Date
        };
    }

    private static GradeEntity MapToEntity(Grade grade)
    {
        return new GradeEntity
        {
            Id = grade.Id,
            StudentId = grade.StudentId,
            SubjectId = grade.SubjectId,
            TeacherId = grade.TeacherId,
            Value = grade.Value,
            Date = grade.Date
        };
    }
}
