using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using DomainClassSubject = ElectronicRegisterAPI.Domain.Models.ClassSubject;
using ClassSubjectEntity =
    ElectronicRegisterAPI.Infrastructure.Persistence.Entities.ClassSubject;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal class ClassSubjectRepository : IClassSubjectRepository
{
    private readonly ElectronicRegisterContext _context;

    public ClassSubjectRepository(ElectronicRegisterContext context)
    {
        _context = context;
    }

    public async Task<DomainClassSubject?> GetByIdAsync(Guid id)
    {
        var entity = await _context.ClassSubjects
            .AsNoTracking()
            .FirstOrDefaultAsync(cs => cs.Id == id);

        return entity is null ? null : MapToModel(entity);
    }

    public async Task<List<DomainClassSubject>> GetByIdsAsync(IEnumerable<Guid> ids)
    {
        return await _context.ClassSubjects
            .AsNoTracking()
            .Where(cs => ids.Contains(cs.Id))
            .Select(cs => new DomainClassSubject
            {
                Id = cs.Id,
                ClassId = cs.ClassId,
                SubjectId = cs.SubjectId,
                TeacherId = cs.TeacherId
            })
            .ToListAsync();
    }

    public async Task<DomainClassSubject?> GetAsync(Guid classId, Guid subjectId, Guid? teacherId = null)
    {
        var query = _context.ClassSubjects
            .AsNoTracking()
            .Where(cs =>
                cs.ClassId == classId &&
                cs.SubjectId == subjectId);

        if (teacherId.HasValue) query = query.Where(cs => cs.TeacherId == teacherId.Value);

        var entity = await query.FirstOrDefaultAsync();

        return entity is null ? null : MapToModel(entity);
    }

    public async Task<List<DomainClassSubject>> GetByTeacherIdAsync(Guid teacherId)
    {
        return await _context.ClassSubjects
            .AsNoTracking()
            .Where(cs => cs.TeacherId == teacherId)
            .Select(cs => new DomainClassSubject
            {
                Id = cs.Id,
                ClassId = cs.ClassId,
                SubjectId = cs.SubjectId,
                TeacherId = cs.TeacherId
            })
            .ToListAsync();
    }

    public async Task<List<DomainClassSubject>> GetBySubjectIdAsync(Guid subjectId)
    {
        return await _context.ClassSubjects
            .AsNoTracking()
            .Where(cs => cs.SubjectId == subjectId)
            .Select(cs => new DomainClassSubject
            {
                Id = cs.Id,
                ClassId = cs.ClassId,
                SubjectId = cs.SubjectId,
                TeacherId = cs.TeacherId
            })
            .ToListAsync();
    }

    public async Task<List<DomainClassSubject>> GetByClassIdAsync(Guid classId)
    {
        return await _context.ClassSubjects
            .AsNoTracking()
            .Where(cs => cs.ClassId == classId)
            .Select(cs => new DomainClassSubject
            {
                Id = cs.Id,
                ClassId = cs.ClassId,
                SubjectId = cs.SubjectId,
                TeacherId = cs.TeacherId
            })
            .ToListAsync();
    }

    public Task<bool> ExistsForTeacherAsync(Guid teacherId)
    {
        return _context.ClassSubjects.AnyAsync(cs => cs.TeacherId == teacherId);
    }

    public Task<bool> ExistsForSubjectAsync(Guid subjectId)
    {
        return _context.ClassSubjects.AnyAsync(cs => cs.SubjectId == subjectId);
    }

    private static DomainClassSubject MapToModel(ClassSubjectEntity entity)
    {
        return new DomainClassSubject
        {
            Id = entity.Id,
            ClassId = entity.ClassId,
            SubjectId = entity.SubjectId,
            TeacherId = entity.TeacherId
        };
    }
}