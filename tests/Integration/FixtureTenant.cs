using System;
using System.Collections.Generic;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;

namespace ElectronicRegisterAPI.Tests.Integration;

// Test transport accessor only. Roles and generations come from persisted IAM;
// the production manager still reauthorizes this context and resolves profiles.
internal sealed class FixtureTenant : ICurrentTenant
{
    private TenantContext? _value;
    public TenantContext Value => _value ?? throw new UnauthorizedAccessException("No interactive fixture context.");

    internal static TenantContext Read(IServiceProvider services, Guid user)
    {
        using var connection = new MySqlConnection(services.GetRequiredService<ElectronicRegisterContext>().Database.GetConnectionString());
        connection.Open();
        using var command = new MySqlCommand("SELECT m.id,s.security_generation,r.role FROM institution_membership m JOIN institution_membership_security_state s ON s.institution_id=m.institution_id AND s.membership_id=m.id JOIN institution_membership_role r ON r.membership_id=m.id WHERE m.institution_id=@tenant AND m.user_id=@user ORDER BY r.role", connection);
        command.Parameters.AddWithValue("@tenant", SeedData.CoreInstitutionId);
        command.Parameters.AddWithValue("@user", user);
        using var reader = command.ExecuteReader();
        Guid membership = default;
        long generation = 0;
        var roles = new List<InstitutionRole>();
        while (reader.Read())
        {
            membership = reader.GetGuid(0);
            generation = reader.GetInt64(1);
            roles.Add(Enum.Parse<InstitutionRole>(reader.GetString(2)));
        }
        if (membership == Guid.Empty || roles.Count == 0)
            throw new UnauthorizedAccessException("No explicit fixture membership.");
        return new(user, SeedData.CoreInstitutionId, membership, roles, generation);
    }

    internal static IInstitutionExamManager For(IServiceProvider services, Guid user)
    {
        services.GetRequiredService<FixtureTenant>()._value = Read(services, user);
        return services.GetRequiredService<IInstitutionExamManager>();
    }

    internal static void Register(IServiceCollection services)
    {
        // Unbound repositories are available only to test data arrangement/inspection.
        // Production creates these implementations solely via the bound factories.
        void Data<T>(string implementation) where T : class => services.AddScoped<T>(sp =>
            (T)ActivatorUtilities.CreateInstance(sp, typeof(ElectronicRegisterContext).Assembly.GetType("ElectronicRegisterAPI.Infrastructure.Repositories." + implementation, true)!));
        Data<ElectronicRegisterAPI.Domain.Interfaces.Repositories.IExamRepository>("ExamRepository");
        Data<ElectronicRegisterAPI.Domain.Interfaces.Repositories.IExamSubmissionRepository>("ExamSubmissionRepository");
        Data<ElectronicRegisterAPI.Domain.Interfaces.Repositories.IGradeRepository>("GradeRepository");
        Data<ElectronicRegisterAPI.Domain.Interfaces.Repositories.IStudentRepository>("StudentRepository");
        Data<ElectronicRegisterAPI.Domain.Interfaces.Repositories.ISubjectRepository>("SubjectRepository");
        Data<ElectronicRegisterAPI.Domain.Interfaces.Repositories.IClassSubjectRepository>("ClassSubjectRepository");
        Data<ElectronicRegisterAPI.Domain.Interfaces.Repositories.IAuditRepository>("AuditRepository");
        services.AddScoped<FixtureTenant>();
        services.AddScoped<FixtureExpiryClient>();
        services.AddScoped<FixtureExamClient>();
        services.AddScoped<FixtureSubmissionClient>();
        services.AddScoped<ICurrentTenant>(sp => sp.GetRequiredService<FixtureTenant>());
    }
}
