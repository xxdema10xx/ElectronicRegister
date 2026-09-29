using Microsoft.EntityFrameworkCore;
using ItsClass = ElectronicRegisterAPI.Domain.Models.ItsClass;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using ItsClassEntity = ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Class;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal class ItsClassRepository : IItsClassRepository
{
    private readonly ElectronicRegisterContext _context;

    public ItsClassRepository(ElectronicRegisterContext context)
    {
        _context = context;
    }

    public async Task<int> CountAsync()
    {
        return await _context.Classes.CountAsync();
    }

    public async Task<List<ItsClass>> GetAllAsync()
    {
        var itsClass = await _context.Classes.ToListAsync();
        return itsClass.Select(MapToModel).ToList();
    }

    public async Task<ItsClass?> GetByIdAsync(Guid id)
    {
        var itsClass = await _context.Classes.FirstOrDefaultAsync(s => s.Id == id);
        return itsClass == null ? null : MapToModel(itsClass);
    }

    public async Task<List<ItsClass>> GetByIdsAsync(IEnumerable<Guid> ids)
    {
        var itsClass = await _context.Classes.Where(s => ids.Contains(s.Id)).ToListAsync();
        return itsClass.Select(MapToModel).ToList();
    }

    public async Task <ItsClass?> GetByNameAsync(string name)
    {
        var itsClass = await _context.Classes.Where(s => s.Name == name).FirstOrDefaultAsync();
        return itsClass == null ? null : MapToModel(itsClass);
    }

    public async Task AddAsync(ItsClass itsClass)
    {
        var itsClassEntity = new ItsClassEntity
        {
            Id = itsClass.Id,
            BienniumStudyPathId = itsClass.BienniumStudyPathId,
            Name = itsClass.Name
        };
        _context.Classes.Add(itsClassEntity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ItsClass itsClass)
    {
        var entity = await MapToEntityAsync(itsClass);
        if (entity == null) return;
        _context.Classes.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(ItsClass itsClass)
    {
        var entity = await MapToEntityAsync(itsClass);
        if (entity == null) return;
        _context.Classes    .Remove(entity);
        await _context.SaveChangesAsync();
    }

    private static ItsClass MapToModel(ItsClassEntity itsClass)
    {
        return new ItsClass
        {
            Id = itsClass.Id,
            BienniumStudyPathId = itsClass.BienniumStudyPathId,
            Name = itsClass.Name
        };
    }

    private async Task<ItsClassEntity?> MapToEntityAsync(ItsClass itsClass)
    {
        var entity = await _context.Classes.FirstOrDefaultAsync(s => s.Id == itsClass.Id);
        if (entity is null) return null;

        entity.Id = itsClass.Id;
        entity.BienniumStudyPathId = itsClass.BienniumStudyPathId;
        entity.Name = itsClass.Name;
        return entity;
    }
}