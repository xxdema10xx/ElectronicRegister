using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using StudyPathModel = ElectronicRegisterAPI.Domain.Models.StudyPath;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal class BienniumStudyPathRepository : IBienniumStudyPathRepository
{
    private readonly ElectronicRegisterContext _context;

    public BienniumStudyPathRepository(ElectronicRegisterContext context)
    {
        _context = context;
    }

    public async Task<Dictionary<Guid, StudyPathModel>> GetStudyPathsByIdsAsync(
        IEnumerable<Guid> bienniumStudyPathIds)
    {
        var rows = await _context.BienniumStudyPaths
            .AsNoTracking()
            .Where(b => bienniumStudyPathIds.Contains(b.Id))
            .Select(b => new { b.Id, b.StudyPath })
            .ToListAsync();

        return rows.ToDictionary(
            r => r.Id,
            r => new StudyPathModel
            {
                Id = r.StudyPath.Id,
                Name = r.StudyPath.Name,
                Description = r.StudyPath.Description
            });
    }
}
