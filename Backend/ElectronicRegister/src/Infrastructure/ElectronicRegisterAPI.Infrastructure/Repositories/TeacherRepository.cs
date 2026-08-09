using Microsoft.EntityFrameworkCore;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Teacher = ElectronicRegisterAPI.Domain.Models.Teacher;
using TeacherEntity = ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Teacher;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal class TeacherRepository : ITeacherRepository
{
    private readonly ElectronicRegisterContext _context;

    public TeacherRepository(ElectronicRegisterContext context)
    {
        _context = context;
    }

    public async Task<int> CountAsync()
    {
        return await _context.Teachers.CountAsync();
    }

    public async Task<List<Teacher>> GetAllAsync()
    {
        var teachers = await _context.Teachers.ToListAsync();
        return teachers.Select(MapToModel).ToList();
    }

    public async Task<Teacher?> GetByIdAsync(Guid id)
    {
        var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.Id == id);
        return teacher == null ? null : MapToModel(teacher);
    }

    public async Task<List<Teacher>> GetByIdsAsync(IEnumerable<Guid> ids)
    {
        var teachers = await _context.Teachers.Where(t => ids.Contains(t.Id)).ToListAsync();
        return teachers.Select(MapToModel).ToList();
    }

    public async Task<List<Teacher>> GetByLastNameAsync(string lastName)
    {
        var teachers = await _context.Teachers.Where(t => t.LastName == lastName).ToListAsync();
        return teachers.Select(MapToModel).ToList();
    }

    public async Task AddAsync(Teacher teacher)
    {
        var teacherEntity = new TeacherEntity
        {
            Id = teacher.Id,
            FirstName = teacher.FirstName,
            LastName = teacher.LastName
        };
        _context.Teachers.Add(teacherEntity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Teacher teacher)
    {
        var teacherEntity = await MapToEntity(teacher);
        if (teacherEntity is null) return;

        _context.Teachers.Update(teacherEntity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Teacher teacher)
    {
        var teacherEntity = await MapToEntity(teacher);
        if(teacherEntity is null) return;
        _context.Teachers.Remove(teacherEntity);
        await _context.SaveChangesAsync();
    }

    private static Teacher MapToModel(TeacherEntity teacher)
    {
        return new Teacher
        {
            Id = teacher.Id,
            FirstName = teacher.FirstName,
            LastName = teacher.LastName
        };
    }

    private async Task<TeacherEntity?> MapToEntity(Teacher teacher)
    {
        var entity = await _context.Teachers.FirstOrDefaultAsync(t => t.Id == teacher.Id);
        if (entity is null) return null;

        entity.Id = teacher.Id;
        entity.FirstName = teacher.FirstName;
        entity.LastName = teacher.LastName;

        return entity;
    }
}
