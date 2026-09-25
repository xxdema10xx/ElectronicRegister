using ElectronicRegisterAPI.Infrastructure.Persistence;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using BienniumModel = ElectronicRegisterAPI.Domain.Models.Biennium;
using BienniumEntity = ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Biennium;
using Microsoft.EntityFrameworkCore;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;
internal class BienniumRepository : IBienniumRepository
{
    private readonly ElectronicRegisterContext _context;

    private readonly int currentYear = DateTime.Now.Year;

    public BienniumRepository(ElectronicRegisterContext context)
    {
        _context = context;
    }

    public async Task<int> CountAsync()
    {
        return await _context.Biennia.CountAsync();
    }

    public async Task<List<BienniumModel>> GetAllAsync()
    {
        var biennia = await _context.Biennia.ToListAsync();
        return biennia.Select(MapToModel).ToList();
    }

    public async Task<List<BienniumModel>> GetActiveBienniaAsync()
    {
        var activeBiennia = await _context.Biennia
            .Where(b => b.StartYear <= currentYear && b.EndYear >= currentYear)
            .ToListAsync();
        return activeBiennia.Select(MapToModel).ToList();
    }

    public async Task<bool> HasStudyAreasAsync(Guid bienniumId)
    {
        return await _context.BienniumStudyAreas.AnyAsync(x => x.BienniumId == bienniumId);
    }

    public async Task<BienniumModel?> GetByIdAsync(Guid id)
    {
        var biennium = await _context.Biennia.FirstOrDefaultAsync(b => b.Id == id);
        return biennium == null ? null : MapToModel(biennium);
    }

    public async Task<BienniumModel?> GetBienniumByStartYearAsync(int startYear)
    {
        var biennium = await _context.Biennia.FirstOrDefaultAsync(b => b.StartYear == startYear);
        return biennium == null ? null : MapToModel(biennium);
    }

    public async Task<BienniumModel?> GetBienniumByEndYearAsync(int endYear)
    {
        var biennium = await _context.Biennia.FirstOrDefaultAsync(b => b.EndYear == endYear);
        return biennium == null ? null : MapToModel(biennium);
    }

    public async Task AddAsync(BienniumModel biennium)
    {
        var bienniumEntity = new BienniumEntity
        {
            Id = biennium.Id,
            StartYear = biennium.StartYear,
            EndYear = biennium.EndYear
        };
        _context.Biennia.Add(bienniumEntity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(BienniumModel biennium)
    {
        var bienniumEntity = await MapToEntity(biennium);
        if (bienniumEntity is null) return;

        _context.Biennia.Update(bienniumEntity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(BienniumModel biennium)
    {
        var bienniumEntity = await MapToEntity(biennium);
        if (bienniumEntity is null) return;
        _context.Biennia.Remove(bienniumEntity);
        await _context.SaveChangesAsync();
    }

    private static BienniumModel MapToModel(BienniumEntity biennium)
    {
        return new BienniumModel
        {
            Id = biennium.Id,
            StartYear = biennium.StartYear,
            EndYear = biennium.EndYear
        };
    }

    private async Task<BienniumEntity?> MapToEntity(BienniumModel biennium)
    {
        var entity = await _context.Biennia.FirstOrDefaultAsync(b => b.Id == biennium.Id);
        if (entity is null) return null;

        entity.Id = biennium.Id;
        entity.StartYear = biennium.StartYear;
        entity.EndYear = biennium.EndYear;

        return entity;
    }
}
