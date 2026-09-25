using ElectronicRegisterAPI.Infrastructure.Persistence;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using StudyPathModel = ElectronicRegisterAPI.Domain.Models.StudyPath;
using StudyPathEntity = ElectronicRegisterAPI.Infrastructure.Persistence.Entities.StudyPath;
using Microsoft.EntityFrameworkCore;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal class StudyPathRepository : IStudyPathRepository
{
    private readonly ElectronicRegisterContext _context;

    public StudyPathRepository(ElectronicRegisterContext context)
    {
        _context = context;
    }

    public async Task<int> CountAsync()
    {
        return await _context.StudyPaths.CountAsync();
    }

    public async Task<List<StudyPathModel>> GetAllAsync()
    {
        var studyPaths = await _context.StudyPaths.ToListAsync();
        return studyPaths.Select(MapToModel).ToList();
    }

    public async Task<StudyPathModel?> GetByIdAsync(Guid id)
    {
        var studyPath = await _context.StudyPaths.FirstOrDefaultAsync(b => b.Id == id);
        return studyPath == null ? null : MapToModel(studyPath);
    }

    public async Task<StudyPathModel?> GetByNameAsync(string name)
    {
        var studyPath = await _context.StudyPaths.FirstOrDefaultAsync(b => b.Name == name);
        return studyPath == null ? null : MapToModel(studyPath);
    }

    public async Task<bool> HasClassesAsync(Guid studyPathId)
    {
        return await _context.Classes.AnyAsync(x => x.BienniumStudyPath.StudyPathId == studyPathId);
    }

    public async Task AddAsync(StudyPathModel studyPath)
    {
        var studyPathEntity = new StudyPathEntity
        {
            Id = studyPath.Id,
            Name = studyPath.Name,
            Description = studyPath.Description
        };
        _context.StudyPaths.Add(studyPathEntity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(StudyPathModel studyPath)
    {
        var studyPathEntity = await MapToEntity(studyPath);
        if (studyPathEntity is null) return;

        _context.StudyPaths.Update(studyPathEntity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(StudyPathModel studyPath)
    {
        var studyPathEntity = await MapToEntity(studyPath);
        if (studyPathEntity is null) return;
        _context.StudyPaths.Remove(studyPathEntity);
        await _context.SaveChangesAsync();
    }

    private static StudyPathModel MapToModel(StudyPathEntity studyPath)
    {
        return new StudyPathModel
        {
            Id = studyPath.Id,
            Name = studyPath.Name,
            Description = studyPath.Description
        };
    }

    private async Task<StudyPathEntity?> MapToEntity(StudyPathModel studyPath)
    {
        var entity = await _context.StudyPaths.FirstOrDefaultAsync(b => b.Id == studyPath.Id);
        if (entity is null) return null;

        entity.Id = studyPath.Id;
        entity.Name = studyPath.Name;
        entity.Description = studyPath.Description;

        return entity;
    }
}
