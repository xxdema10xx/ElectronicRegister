using Microsoft.EntityFrameworkCore;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal class SubjectRepository : ISubjectRepository
{
    private readonly ElectronicRegisterContext _context;

    public SubjectRepository(ElectronicRegisterContext context)
    {
        _context = context;
    }

    public async Task<int> CountAsync(Guid? teacherId = null)
    {
        var query = _context.Subjects.AsQueryable();
        // TODO: Filter by teacherId if needed (depends on data model relationship)
        return await query.CountAsync();
    }

    public async Task<List<SubjectDto>> GetAllAsync(Guid? teacherId = null)
    {
        var query = _context.Subjects.AsQueryable();
        // TODO: Filter by teacherId if needed (depends on data model relationship)
        var subjects = await query.ToListAsync();
        return subjects.Select(MapToDto).ToList();
    }

    public async Task<SubjectDto?> GetByIdAsync(Guid id)
    {
        var subject = await _context.Subjects.FirstOrDefaultAsync(s => s.Id == id);
        return subject == null ? null : MapToDto(subject);
    }

    public async Task<List<SubjectDto>> GetByIdsAsync(IEnumerable<Guid> ids)
    {
        var subjects = await _context.Subjects.Where(s => ids.Contains(s.Id)).ToListAsync();
        return subjects.Select(MapToDto).ToList();
    }

    public async Task<SubjectDto?> GetByNameAsync(string name)
    {
        var subject = await _context.Subjects.FirstOrDefaultAsync(s => s.Name == name);
        return subject == null ? null : MapToDto(subject);
    }

    public async Task<List<SubjectDto>> GetByTeacherIdAsync(Guid teacherId)
    {
        // TODO: Implement based on data model relationship between Subject and Teacher
        var subjects = await _context.Subjects.ToListAsync();
        return subjects.Select(MapToDto).ToList();
    }

    public async Task<bool> ExistsForTeacherAsync(Guid teacherId)
    {
        // TODO: Implement based on data model relationship between Subject and Teacher
        return await _context.Subjects.AnyAsync();
    }

    public async Task AddAsync(SubjectDto subjectDto)
    {
        var subject = MapToEntity(subjectDto);
        _context.Subjects.Add(subject);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(SubjectDto subjectDto)
    {
        var subject = MapToEntity(subjectDto);
        _context.Subjects.Update(subject);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var subject = await _context.Subjects.FirstOrDefaultAsync(s => s.Id == id);
        if (subject != null)
        {
            _context.Subjects.Remove(subject);
            await _context.SaveChangesAsync();
        }
    }

    private static SubjectDto MapToDto(Subject subject)
    {
        return new SubjectDto
        {
            Id = subject.Id,
            Name = subject.Name
        };
    }

    private static Subject MapToEntity(SubjectDto subjectDto)
    {
        return new Subject
        {
            Id = subjectDto.Id,
            Name = subjectDto.Name
        };
    }
}

