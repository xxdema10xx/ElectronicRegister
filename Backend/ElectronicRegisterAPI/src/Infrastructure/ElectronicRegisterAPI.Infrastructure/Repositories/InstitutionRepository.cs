using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Entity = ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Institution;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal sealed class InstitutionRepository(ElectronicRegisterContext context) : IInstitutionRepository
{
    public async Task<Institution?> GetByIdAsync(Guid id)
    {
        var row = await context.Institutions.AsNoTracking().SingleOrDefaultAsync(i => i.Id == id);
        return row is null ? null : Map(row);
    }

    public async Task<Institution?> GetByCodeAsync(string code)
    {
        var row = await context.Institutions.AsNoTracking().SingleOrDefaultAsync(i => i.Code == code);
        return row is null ? null : Map(row);
    }

    public async Task AddAsync(Institution institution)
    {
        ArgumentNullException.ThrowIfNull(institution);
        if (institution.Id == Guid.Empty || string.IsNullOrWhiteSpace(institution.Name) || string.IsNullOrWhiteSpace(institution.Code) || institution.Name.Length > 200 || institution.Code.Length > 64 || institution.Code.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')))
            throw new ArgumentException("Invalid institution identity/name/internal code.");
        if (institution.Status != InstitutionStatus.Provisioning)
            throw new InvalidOperationException("MT-1 cannot activate an institution.");
        context.Institutions.Add(new Entity { Id = institution.Id, Name = institution.Name, Code = institution.Code, LegalTaxCode = institution.LegalTaxCode, Region = institution.Region, Status = "provisioning" });
        await context.SaveChangesAsync();
    }

    private static Institution Map(Entity row) => new()
    {
        Id = row.Id,
        Name = row.Name,
        Code = row.Code,
        LegalTaxCode = row.LegalTaxCode,
        Region = row.Region,
        Status = Enum.Parse<InstitutionStatus>(row.Status, true),
        CreatedAt = row.CreatedAt,
        UpdatedAt = row.UpdatedAt
    };
}
