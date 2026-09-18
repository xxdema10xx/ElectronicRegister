using ElectronicRegisterAPI.Infrastructure.Persistence;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using StudyAreaModel = ElectronicRegisterAPI.Domain.Models.StudyArea;
using StudyAreaEntity = ElectronicRegisterAPI.Infrastructure.Persistence.Entities.StudyArea;
using Microsoft.EntityFrameworkCore;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal class StudyAreaRepository : IStudyAreaRepository
{
    private readonly ElectronicRegisterContext _context;

    public StudyAreaRepository(ElectronicRegisterContext context)
    {
        _context = context;
    }

    public async Task<int> CountAsync()
    {
        return await _context.StudyAreas.CountAsync();
    }

    public async Task<List<StudyAreaModel>> GetAllAsync()
    {
        var studyAreas = await _context.StudyAreas.ToListAsync();
        return studyAreas.Select(MapToModel).ToList();
    }

    public async Task<StudyAreaModel?> GetByIdAsync(Guid id)
    {
        var studyArea = await _context.StudyAreas.FirstOrDefaultAsync(b => b.Id == id);
        return studyArea == null ? null : MapToModel(studyArea);
    }

    public async Task<StudyAreaModel?> GetStudyAreaByNameAsync(string name)
    {
        var studyArea = await _context.StudyAreas.FirstOrDefaultAsync(b => b.Name == name);
        return studyArea == null ? null : MapToModel(studyArea);
    }

    public async Task AddAsync(StudyAreaModel studyArea)
    {
        var studyAreaEntity = new StudyAreaEntity
        {
            Id = studyArea.Id,
            Name = studyArea.Name,
            Description = studyArea.Description
        };
        _context.StudyAreas.Add(studyAreaEntity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(StudyAreaModel studyArea)
    {
        var studyAreaEntity = await MapToEntity(studyArea);
        if (studyAreaEntity is null) return;

        _context.StudyAreas.Update(studyAreaEntity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(StudyAreaModel studyArea)
    {
        var studyAreaEntity = await MapToEntity(studyArea);
        if (studyAreaEntity is null) return;
        _context.StudyAreas.Remove(studyAreaEntity);
        await _context.SaveChangesAsync();
    }

    private static StudyAreaModel MapToModel(StudyAreaEntity studyArea)
    {
        return new StudyAreaModel
        {
            Id = studyArea.Id,
            Name = studyArea.Name,
            Description = studyArea.Description
        };
    }

    private async Task<StudyAreaEntity?> MapToEntity(StudyAreaModel studyArea)
    {
        var entity = await _context.StudyAreas.FirstOrDefaultAsync(b => b.Id == studyArea.Id);
        if (entity is null) return null;

        entity.Id = studyArea.Id;
        entity.Name = studyArea.Name;
        entity.Description = studyArea.Description;

        return entity;
    }
}
