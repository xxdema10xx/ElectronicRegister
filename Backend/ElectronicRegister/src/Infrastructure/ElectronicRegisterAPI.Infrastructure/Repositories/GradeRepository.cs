using Microsoft.EntityFrameworkCore;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal class GradeRepository : IGradeRepository
{
    private readonly ElectronicRegisterContext _context;

    public GradeRepository(ElectronicRegisterContext context)
    {
        _context = context;
    }

    public async Task<GradeDto?> GetByIdAsync(Guid id)
    {
        var grade = await _context.Grades.FirstOrDefaultAsync(g => g.Id == id);
        return grade == null ? null : MapToDto(grade);
    }

    public async Task<List<GradeDto>> GetAllAsync()
    {
        var grades = await _context.Grades.ToListAsync();
        return grades.Select(MapToDto).ToList();
    }

    public async Task<int> CountAsync(Guid? teacherId = null)
    {
        var query = _context.Grades.AsQueryable();
        if (teacherId.HasValue)
        {
            query = query.Where(g => g.TeacherId == teacherId.Value);
        }
        return await query.CountAsync();
    }

    public async Task AddAsync(GradeDto gradeDto)
    {
        var grade = MapToEntity(gradeDto);
        await _context.Grades.AddAsync(grade);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(GradeDto gradeDto)
    {
        var grade = MapToEntity(gradeDto);
        _context.Grades.Update(grade);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var grade = await _context.Grades.FirstOrDefaultAsync(g => g.Id == id);
        if (grade != null)
        {
            _context.Grades.Remove(grade);
            await _context.SaveChangesAsync();
        }
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

    private static GradeDto MapToDto(Grade grade)
    {
        return new GradeDto
        {
            Id = grade.Id,
            StudentId = grade.StudentId,
            SubjectId = grade.SubjectId,
            TeacherId = grade.TeacherId,
            Value = grade.Value,
            Date = grade.Date
        };
    }

    private static Grade MapToEntity(GradeDto gradeDto)
    {
        return new Grade
        {
            Id = gradeDto.Id,
            StudentId = gradeDto.StudentId,
            SubjectId = gradeDto.SubjectId,
            TeacherId = gradeDto.TeacherId,
            Value = gradeDto.Value,
            Date = gradeDto.Date
        };
    }
}
