using Microsoft.EntityFrameworkCore;
using User = ElectronicRegisterAPI.Domain.Models.User;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using UserEntity = ElectronicRegisterAPI.Infrastructure.Persistence.Entities.User;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal class UserRepository : IUserRepository
{
    private readonly ElectronicRegisterContext _context;

    public UserRepository(ElectronicRegisterContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        return user == null ? null : MapToModel(user);
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
        return user == null ? null : MapToModel(user);
    }

    public async Task<List<User>> GetAllAsync()
    {
        var users = await _context.Users.ToListAsync();
        return users.Select(MapToModel).ToList();
    }

    public async Task<int> CountAsync()
    {
        return await _context.Users.CountAsync();
    }

    public async Task AddAsync(User userDto)
    {
        var user = MapToEntity(userDto);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(User user)
    {
        var entity = await _context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        if (entity is null) return;

        entity.Email = user.Email;
        entity.PasswordHash = user.PasswordHash;
        entity.Role = user.Role switch
        {
            UserRole.Admin => "admin",
            UserRole.Teacher => "teacher",
            UserRole.Student => "student",
            _ => throw new ArgumentOutOfRangeException(nameof(user.Role), $"Ruolo non valido: {user.Role}")
        };
        entity.StudentId = user.StudentId;
        entity.TeacherId = user.TeacherId;

        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(User user)
    {
        var entity = await _context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        if (entity != null)
        {
            _context.Users.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsByEmailAsync(string email)
    {
        return await _context.Users.AnyAsync(u => u.Email == email);
    }

    private static User MapToModel(UserEntity entity)
    {
        var role = entity.Role.ToLowerInvariant() switch
        {
            "admin" => UserRole.Admin,
            "teacher" => UserRole.Teacher,
            "student" => UserRole.Student,
            _ => throw new ArgumentOutOfRangeException(nameof(entity.Role), $"Ruolo non valido: {entity.Role}")
        };

        return new User
        {
            Id = entity.Id,
            Email = entity.Email,
            PasswordHash = entity.PasswordHash,
            Role = role,
            StudentId = entity.StudentId,
            TeacherId = entity.TeacherId
        };
    }

    private static UserEntity MapToEntity(User user)
    {
        return new UserEntity
        {
            Id = user.Id,
            Email = user.Email,
            PasswordHash = user.PasswordHash,
            Role = user.Role switch
            {
                UserRole.Admin => "admin",
                UserRole.Teacher => "teacher",
                UserRole.Student => "student",
                _ => throw new ArgumentOutOfRangeException(nameof(user.Role), $"Ruolo non valido: {user.Role}")
            },
            StudentId = user.StudentId,
            TeacherId = user.TeacherId
        };
    }
}
