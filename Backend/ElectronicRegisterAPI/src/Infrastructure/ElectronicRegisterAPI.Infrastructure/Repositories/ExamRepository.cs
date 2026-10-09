using Microsoft.EntityFrameworkCore;
using Exam = ElectronicRegisterAPI.Domain.Models.Exam;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using ExamEntity = ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Exam;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal class ExamRepository : IExamRepository
{
    private readonly ElectronicRegisterContext _context;

    public ExamRepository(ElectronicRegisterContext context)
    {
        _context = context;
    }

    public async Task<int> CountAsync()
    {
        return await _context.Exams.CountAsync();
    }

    public async Task<List<Exam>> GetAllAsync()
    {
        var exams = await _context.Exams.ToListAsync();
        return exams.Select(MapToModel).ToList();
    }

    public async Task<Exam?> GetByIdAsync(Guid id)
    {
        var exam = await _context.Exams.FirstOrDefaultAsync(e => e.Id == id);
        return exam == null ? null : MapToModel(exam);
    }

    public async Task<List<Exam>> GetByIdsAsync(IEnumerable<Guid> ids)
    {
        var exams = await _context.Exams.Where(e => ids.Contains(e.Id)).ToListAsync();
        return exams.Select(MapToModel).ToList();
    }

    public async Task<List<Exam>> GetByDateAsync(DateOnly date)
    {
        var exams = await _context.Exams.Where(e => e.Date == date).ToListAsync();
        return exams.Select(MapToModel).ToList();
    }

    public async Task AddAsync(Exam exam)
    {
        var examEntity = new ExamEntity
        {
            Id = exam.Id,
            ClassSubjectId = exam.ClassSubjectId,
            Date = exam.Date
        };
        _context.Exams.Add(examEntity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Exam exam)
    {
        var entity = await MapToEntityAsync(exam);
        if (entity == null) return;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Exam exam)
    {
        var entity = await _context.Exams.FirstOrDefaultAsync(e => e.Id == exam.Id);
        if (entity == null) return;
        _context.Exams.Remove(entity);
        await _context.SaveChangesAsync();
    }

    private static Exam MapToModel(ExamEntity exam)
    {
        return new Exam
        {
            Id = exam.Id,
            ClassSubjectId = exam.ClassSubjectId,
            Date = exam.Date
        };
    }

    private async Task<ExamEntity?> MapToEntityAsync(Exam exam)
    {
        var entity = await _context.Exams.FirstOrDefaultAsync(e => e.Id == exam.Id);
        if (entity is null) return null;

        entity.ClassSubjectId = exam.ClassSubjectId;
        entity.Date = exam.Date;

        return entity;
    }
}