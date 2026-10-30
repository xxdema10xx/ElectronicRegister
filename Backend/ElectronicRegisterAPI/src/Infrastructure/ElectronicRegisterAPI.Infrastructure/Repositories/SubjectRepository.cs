using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Infrastructure.Repositories;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using DomainSubject = ElectronicRegisterAPI.Domain.Models.Subject;
using SubjectEntity = ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Subject;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal class SubjectRepository : ISubjectRepository
{
    private readonly ElectronicRegisterContext _context;
    private readonly InstitutionExamDataScope? _scope;
    private CancellationToken Ct => _scope?.CancellationToken ?? CancellationToken.None;
    private IQueryable<T> Rows<T>()
        where T : class => _scope?.Query<T>() ?? _context.Set<T>();
    public SubjectRepository(ElectronicRegisterContext context, InstitutionExamDataScope? scope = null)
    {
        _context = context;
        _scope = scope;
    }

    public async Task<List<DomainSubject>> GetByIdsAsync(IEnumerable<Guid> ids)
    {
        return await Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Subject>().AsNoTracking().Where(s => ids.Contains(s.Id)).Select(s => new DomainSubject { Id = s.Id, Name = s.Name }).ToListAsync(Ct);
    }

}
