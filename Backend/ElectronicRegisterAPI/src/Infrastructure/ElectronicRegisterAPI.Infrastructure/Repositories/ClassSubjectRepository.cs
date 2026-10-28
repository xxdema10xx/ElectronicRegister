using ElectronicRegisterAPI.Infrastructure.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using DomainClassSubject = ElectronicRegisterAPI.Domain.Models.ClassSubject;
using ClassSubjectEntity = ElectronicRegisterAPI.Infrastructure.Persistence.Entities.ClassSubject;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal class ClassSubjectRepository : IClassSubjectRepository
{
    private readonly ElectronicRegisterContext _context;
    private readonly InstitutionExamDataScope? _scope;
    private IQueryable<T> Rows<T>()
        where T : class => _scope?.Query<T>() ?? _context.Set<T>();
    private Task SaveAsync() => _scope is null ? _context.SaveChangesAsync() : _scope.SaveAsync();
    public ClassSubjectRepository(ElectronicRegisterContext context, InstitutionExamDataScope? scope = null)
    {
        _context = context;
        _scope = scope;
    }

    public async Task<DomainClassSubject?> GetByIdAsync(Guid id)
    {
        var entity = await Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.ClassSubject>().Include(cs => cs.Subject).AsNoTracking().FirstOrDefaultAsync(cs => cs.Id == id);
        return entity is null ? null : MapToModel(entity);
    }

    public async Task<List<DomainClassSubject>> GetByIdsAsync(IEnumerable<Guid> ids)
    {
        return await Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.ClassSubject>().Include(cs => cs.Subject).AsNoTracking().Where(cs => ids.Contains(cs.Id)).Select(cs => new DomainClassSubject { Id = cs.Id, ClassId = cs.ClassId, SubjectId = cs.SubjectId, SubjectName = cs.Subject.Name, TeacherId = cs.TeacherId }).ToListAsync();
    }

    public async Task<DomainClassSubject?> GetAsync(Guid classId, Guid subjectId, Guid? teacherId = null)
    {
        var query = Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.ClassSubject>().Include(cs => cs.Subject).AsNoTracking().Where(cs => cs.ClassId == classId && cs.SubjectId == subjectId);
        if (teacherId.HasValue)
            query = query.Where(cs => cs.TeacherId == teacherId.Value);
        var entity = await query.FirstOrDefaultAsync();
        return entity is null ? null : MapToModel(entity);
    }

    public async Task<List<DomainClassSubject>> GetByTeacherIdAsync(Guid teacherId)
    {
        return await Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.ClassSubject>().Include(cs => cs.Subject).AsNoTracking().Where(cs => cs.TeacherId == teacherId).Select(cs => new DomainClassSubject { Id = cs.Id, ClassId = cs.ClassId, SubjectId = cs.SubjectId, SubjectName = cs.Subject.Name, TeacherId = cs.TeacherId }).ToListAsync();
    }

    public async Task<List<DomainClassSubject>> GetBySubjectIdAsync(Guid subjectId)
    {
        return await Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.ClassSubject>().Include(cs => cs.Subject).AsNoTracking().Where(cs => cs.SubjectId == subjectId).Select(cs => new DomainClassSubject { Id = cs.Id, ClassId = cs.ClassId, SubjectId = cs.SubjectId, SubjectName = cs.Subject.Name, TeacherId = cs.TeacherId }).ToListAsync();
    }

    public async Task<List<DomainClassSubject>> GetByClassIdAsync(Guid classId)
    {
        return await Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.ClassSubject>().Include(cs => cs.Subject).AsNoTracking().Where(cs => cs.ClassId == classId).Select(cs => new DomainClassSubject { Id = cs.Id, ClassId = cs.ClassId, SubjectId = cs.SubjectId, SubjectName = cs.Subject.Name, TeacherId = cs.TeacherId }).ToListAsync();
    }

    public Task<bool> ExistsForTeacherAsync(Guid teacherId)
    {
        return Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.ClassSubject>().AnyAsync(cs => cs.TeacherId == teacherId);
    }

    public Task<bool> ExistsForSubjectAsync(Guid subjectId)
    {
        return Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.ClassSubject>().AnyAsync(cs => cs.SubjectId == subjectId);
    }

    private static DomainClassSubject MapToModel(ClassSubjectEntity entity)
    {
        return new DomainClassSubject
        {
            Id = entity.Id,
            ClassId = entity.ClassId,
            SubjectId = entity.SubjectId,
            SubjectName = entity.Subject.Name,
            TeacherId = entity.TeacherId
        };
    }
}
