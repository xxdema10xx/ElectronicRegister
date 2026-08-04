using Microsoft.EntityFrameworkCore;
using Subject = ElectronicRegisterAPI.Domain.Models;
using DomainSubject = ElectronicRegisterAPI.Domain.Models.Subject;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using  ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
using SubjectEntity =  ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Subject;

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
        if (teacherId.HasValue)
        {
            query = query.Where(s => s.TeacherId == teacherId);
            return await query.CountAsync();
        }
        return await query.CountAsync();
    }

    public async Task<List<DomainSubject>> GetAllAsync(Guid? teacherId = null)
    {
        var subjects = new List<DomainSubject>();
        var query = _context.Subjects.AsQueryable();
        if (teacherId.HasValue)
        {
            query = query.Where(s => s.TeacherId == teacherId);
            subjects = await query.Select(s => MapTo(s)).ToListAsync();
            return subjects;
        }
        
        return subjects = await query.Select(s => MapTo(s)).ToListAsync();
    }

    public async Task<DomainSubject?> GetByIdAsync(Guid id)
    {
        var subject = await _context.Subjects.FirstOrDefaultAsync(s => s.Id == id);
        return subject == null ? null : MapTo(subject);
    }

    public async Task<List<DomainSubject>> GetByIdsAsync(IEnumerable<Guid> ids)
    {
        return await _context.Subjects
            .Where(s => ids.Contains(s.Id))
            .Select(s => MapTo(s))
            .ToListAsync();
    }

    public async Task<DomainSubject?> GetByNameAsync(string name)
    {
        var subject = await _context.Subjects.FirstOrDefaultAsync(s => s.Name == name);
        return subject == null ? null : MapTo(subject);
    }

    public async Task<List<DomainSubject>> GetByTeacherIdAsync(Guid teacherId)
    {
        return  await _context.Subjects
            .Where(s => s.TeacherId == teacherId)
            .Select(s => MapTo(s))
            .ToListAsync();
    }

    public async Task<bool> ExistsForTeacherAsync(Guid teacherId)
    {
        return await _context.Subjects.AnyAsync(s => s.TeacherId == teacherId);
    }

    public async Task AddAsync(DomainSubject subject)
    {
        var subjectEntity = MapToEntity(subject);
        _context.Subjects.Add(subjectEntity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(DomainSubject subject)
    {
        var subjectEntity = MapToEntity(subject);
        _context.Subjects.Update(subjectEntity);
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

    private static DomainSubject MapTo(SubjectEntity subjects)
    {
        return new DomainSubject
        {
            Id = subjects.Id,
            Name = subjects.Name,
            TeacherId = subjects.TeacherId
        };
    }

    private static SubjectEntity MapToEntity(DomainSubject subject)
    {
        return new SubjectEntity
        {
            Id = subject.Id,
            Name = subject.Name,
            TeacherId = subject.TeacherId
        };
    }
}

