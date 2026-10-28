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
    private IQueryable<T> Rows<T>()
        where T : class => _scope?.Query<T>() ?? _context.Set<T>();
    private Task SaveAsync() => _scope is null ? _context.SaveChangesAsync() : _scope.SaveAsync();
    public SubjectRepository(ElectronicRegisterContext context, InstitutionExamDataScope? scope = null)
    {
        _context = context;
        _scope = scope;
    }

    public async Task<int> CountAsync()
    {
        return await Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Subject>().CountAsync();
    }

    public async Task<List<DomainSubject>> GetAllAsync()
    {
        return await Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Subject>().AsNoTracking().Select(s => new DomainSubject { Id = s.Id, Name = s.Name }).ToListAsync();
    }

    public async Task<DomainSubject?> GetByIdAsync(Guid id)
    {
        return await Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Subject>().AsNoTracking().Where(s => s.Id == id).Select(s => new DomainSubject { Id = s.Id, Name = s.Name }).FirstOrDefaultAsync();
    }

    public async Task<List<DomainSubject>> GetByIdsAsync(IEnumerable<Guid> ids)
    {
        return await Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Subject>().AsNoTracking().Where(s => ids.Contains(s.Id)).Select(s => new DomainSubject { Id = s.Id, Name = s.Name }).ToListAsync();
    }

    public async Task<DomainSubject?> GetByNameAsync(string name)
    {
        return await Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Subject>().AsNoTracking().Where(s => s.Name == name).Select(s => new DomainSubject { Id = s.Id, Name = s.Name }).FirstOrDefaultAsync();
    }

    public async Task AddAsync(DomainSubject subject)
    {
        var entity = new SubjectEntity
        {
            Id = subject.Id,
            Name = subject.Name
        };
        await _context.Subjects.AddAsync(entity);
        await SaveAsync();
    }

    public async Task UpdateAsync(DomainSubject subject)
    {
        await using var transaction = _context.Database.CurrentTransaction is null ? await _context.Database.BeginTransactionAsync() : null;
        var rows = Rows<SubjectEntity>().Where(s => s.Id == subject.Id);
        if (await rows.ExecuteUpdateAsync(set => set.SetProperty(s => s.Name, subject.Name)) == 0)
            return;
        await Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Exam>().Where(e => e.ClassSubject.SubjectId == subject.Id).ExecuteUpdateAsync(set => set.SetProperty(e => e.Name, subject.Name));
        if (transaction is not null)
            await transaction.CommitAsync();
    }

    public async Task DeleteAsync(DomainSubject subject)
    {
        var entity = await Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Subject>().FirstOrDefaultAsync(s => s.Id == subject.Id);
        if (entity is null)
            return;
        _context.Subjects.Remove(entity);
        await SaveAsync();
    }
}
