using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Entity = ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Campus;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal sealed class CampusRepository(ElectronicRegisterContext context) : ICampusRepository
{
    public async Task<Campus?> GetByIdAsync(Guid institutionId, Guid id)
    {
        var row = await context.Campuses.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id && c.InstitutionId == institutionId);
        return row is null ? null : Map(row);
    }

    public async Task<List<Campus>> GetByInstitutionAsync(Guid institutionId) => (await context.Campuses.AsNoTracking().Where(c => c.InstitutionId == institutionId).ToListAsync()).Select(Map).ToList();
    public async Task AddAsync(Campus campus)
    {
        ArgumentNullException.ThrowIfNull(campus);
        if (campus.Id == Guid.Empty || campus.InstitutionId == Guid.Empty || string.IsNullOrWhiteSpace(campus.Name))
            throw new ArgumentException("Campus requires its own identity and one institution.");
        context.Campuses.Add(new Entity { Id = campus.Id, InstitutionId = campus.InstitutionId, Name = campus.Name, Address = campus.Address, City = campus.City, Province = campus.Province, IsActive = campus.IsActive });
        await context.SaveChangesAsync();
    }

    private static Campus Map(Entity row) => new()
    {
        Id = row.Id,
        InstitutionId = row.InstitutionId,
        Name = row.Name,
        Address = row.Address,
        City = row.City,
        Province = row.Province,
        IsActive = row.IsActive ?? true
    };
}
