using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using DomainSubject = ElectronicRegisterAPI.Domain.Models.Subject;
using SubjectEntity = ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Subject;


internal class SubjectRepository : ISubjectRepository
{
    private readonly ElectronicRegisterContext _context;

    public SubjectRepository(ElectronicRegisterContext context)
    {
        _context = context;
    }

    public async Task<int> CountAsync()
    {
        return await _context.Subjects.CountAsync();
    }

    public async Task<List<DomainSubject>> GetAllAsync()
    {
        return await _context.Subjects
            .AsNoTracking()
            .Select(s => new DomainSubject
            {
                Id = s.Id,
                Name = s.Name
            })
            .ToListAsync();
    }

    public async Task<DomainSubject?> GetByIdAsync(Guid id)
    {
        return await _context.Subjects
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new DomainSubject
            {
                Id = s.Id,
                Name = s.Name
            })
            .FirstOrDefaultAsync();
    }

    public async Task<List<DomainSubject>> GetByIdsAsync(IEnumerable<Guid> ids)
    {
        return await _context.Subjects
            .AsNoTracking()
            .Where(s => ids.Contains(s.Id))
            .Select(s => new DomainSubject
            {
                Id = s.Id,
                Name = s.Name
            })
            .ToListAsync();
    }

    public async Task<DomainSubject?> GetByNameAsync(string name)
    {
        return await _context.Subjects
            .AsNoTracking()
            .Where(s => s.Name == name)
            .Select(s => new DomainSubject
            {
                Id = s.Id,
                Name = s.Name
            })
            .FirstOrDefaultAsync();
    }

    public async Task AddAsync(DomainSubject subject)
    {
        var entity = new SubjectEntity
        {
            Id = subject.Id,
            Name = subject.Name
        };

        await _context.Subjects.AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(DomainSubject subject)
    {
        var entity = await _context.Subjects
            .FirstOrDefaultAsync(s => s.Id == subject.Id);

        if (entity is null)
            return;

        entity.Name = subject.Name;

        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(DomainSubject subject)
    {
        var entity = await _context.Subjects
            .FirstOrDefaultAsync(s => s.Id == subject.Id);

        if (entity is null)
            return;

        _context.Subjects.Remove(entity);
        await _context.SaveChangesAsync();
    }
}