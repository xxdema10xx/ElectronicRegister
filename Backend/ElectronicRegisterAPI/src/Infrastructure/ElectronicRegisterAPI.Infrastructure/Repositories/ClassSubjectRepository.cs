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
    private CancellationToken Ct => _scope?.CancellationToken ?? CancellationToken.None;
    private IQueryable<T> Rows<T>()
        where T : class => _scope?.Query<T>() ?? _context.Set<T>();
    public ClassSubjectRepository(ElectronicRegisterContext context, InstitutionExamDataScope? scope = null)
    {
        _context = context;
        _scope = scope;
    }

    public async Task<DomainClassSubject?> GetByIdAsync(Guid id)
    {
        var entity = await Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.ClassSubject>().Include(cs => cs.Subject).AsNoTracking().FirstOrDefaultAsync(cs => cs.Id == id, Ct);
        return entity is null ? null : MapToModel(entity);
    }

    public async Task<List<DomainClassSubject>> GetByIdsAsync(IEnumerable<Guid> ids)
    {
        return await Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.ClassSubject>().Include(cs => cs.Subject).AsNoTracking().Where(cs => ids.Contains(cs.Id)).Select(cs => new DomainClassSubject { Id = cs.Id, ClassId = cs.ClassId, SubjectId = cs.SubjectId, SubjectName = cs.Subject.Name, TeacherId = cs.TeacherId }).ToListAsync(Ct);
    }

    public async Task<DomainClassSubject?> GetAsync(Guid classId, Guid subjectId, Guid? teacherId = null)
    {
        var query = Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.ClassSubject>().Include(cs => cs.Subject).AsNoTracking().Where(cs => cs.ClassId == classId && cs.SubjectId == subjectId);
        if (teacherId.HasValue)
            query = query.Where(cs => cs.TeacherId == teacherId.Value);
        var entity = await query.FirstOrDefaultAsync(Ct);
        return entity is null ? null : MapToModel(entity);
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
