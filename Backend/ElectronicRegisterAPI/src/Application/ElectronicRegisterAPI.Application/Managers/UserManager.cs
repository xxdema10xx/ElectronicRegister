using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Application.Managers;

internal class UserManager : IUserManager
{
    private readonly IUserRepository _userRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly ITeacherRepository _teacherRepository;
    private readonly IUserService _userService;
    private readonly IPasswordHasher _passwordHasher;

    public UserManager(
        IUserRepository userRepository,
        IStudentRepository studentRepository,
        ITeacherRepository teacherRepository,
        IUserService userService,
        IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _studentRepository = studentRepository;
        _teacherRepository = teacherRepository;
        _userService = userService;
        _passwordHasher = passwordHasher;
    }

    public Task<int> CountAsync() => _userRepository.CountAsync();

    public async Task<List<UserDto>> GetAllAsync()
    {
        var users = await _userRepository.GetAllAsync();
        return await MapToDtosAsync(users);
    }

    public async Task<UserDto?> GetByIdAsync(Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user is null) return null;

        return MapToDto(user);
    }

    public async Task<bool> UpdatePasswordAsync(Guid id, UpdatePasswordDto dto, ClaimsContext caller)
    {
        _userService.EnsureCallerCanChangePassword(caller, id);

        var user = await _userRepository.GetByIdAsync(id);
        if (user is null) return false;

        _userService.EnsureValidPassword(dto.NewPassword);
        _userService.EnsurePasswordMatches(dto.OldPassword, user.PasswordHash);

        user.PasswordHash = _passwordHasher.Hash(dto.NewPassword!);
        await _userRepository.UpdateAsync(user);
        return true;
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateUserDto dto)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user is null) return false;

        _userService.EnsureEmailIsValid(dto.Email);

        if (!string.Equals(dto.Email, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            await _userService.EnsureEmailIsAvailableAsync(dto.Email!);
            user.Email = dto.Email!;
        }

        if (!string.IsNullOrEmpty(dto.Password))
        {
            _userService.EnsureValidPassword(dto.Password);
            user.PasswordHash = _passwordHasher.Hash(dto.Password);
        }

        await _userRepository.UpdateAsync(user);

        if (dto.FirstName != null || dto.LastName != null)
        {
            if (user.StudentId.HasValue)
            {
                var student = await _studentRepository.GetByIdAsync(user.StudentId.Value);
                if (student is null) return false;

                if (dto.FirstName != null) student.FirstName = dto.FirstName;
                if (dto.LastName != null) student.LastName = dto.LastName;
                await _studentRepository.UpdateAsync(student);
            }
            else if (user.TeacherId.HasValue)
            {
                var teacher = await _teacherRepository.GetByIdAsync(user.TeacherId.Value);
                if (teacher is null) return false;

                if (dto.FirstName != null) teacher.FirstName = dto.FirstName;
                if (dto.LastName != null) teacher.LastName = dto.LastName;
                await _teacherRepository.UpdateAsync(teacher);
            }
        }

        return true;
    }
   
    public async Task<bool> AddAsync(CreateUserDto dto)
    {
        _userService.EnsureEmailIsValid(dto.Email);
        await _userService.EnsureEmailIsAvailableAsync(dto.Email!);
        _userService.EnsureValidRole(dto.Role);
        _userService.EnsureValidPassword(dto.Password);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = dto.Email,
            PasswordHash = _passwordHasher.Hash(dto.Password),
            Role = _userService.ParseRole(dto.Role),
            StudentId = dto.StudentId,
            TeacherId = dto.TeacherId
        };

        await _userRepository.AddAsync(user);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user is null) return false;

        _userService.EnsureUserCanBeDeleted(user);

        await _userRepository.DeleteAsync(user);
        return true;
    }

    private static UserDto MapToDto(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            Role = user.Role.ToString().ToLowerInvariant(),
            StudentId = user.StudentId,
            TeacherId = user.TeacherId
        };
    }

    private async Task<List<UserDto>> MapToDtosAsync(List<User> users)
    {
        var studentIds = users.Where(u => u.StudentId.HasValue).Select(u => u.StudentId!.Value).Distinct().ToList();
        var teacherIds = users.Where(u => u.TeacherId.HasValue).Select(u => u.TeacherId!.Value).Distinct().ToList();

        var students = (await _studentRepository.GetByIdsAsync(studentIds)).ToDictionary(s => s.Id);
        var teachers = (await _teacherRepository.GetByIdsAsync(teacherIds)).ToDictionary(t => t.Id);

        return users.Select(u =>
        {
            Student? student = u.StudentId.HasValue && students.TryGetValue(u.StudentId.Value, out var s) ? s : null;
            Teacher? teacher = u.TeacherId.HasValue && teachers.TryGetValue(u.TeacherId.Value, out var t) ? t : null;

            return new UserDto
            {
                Id = u.Id,
                Email = u.Email,
                Role = u.Role.ToString().ToLowerInvariant(),
                StudentId = u.StudentId,
                StudentFirstName = student?.FirstName,
                StudentLastName = student?.LastName,
                TeacherId = u.TeacherId,
                TeacherFirstName = teacher?.FirstName,
                TeacherLastName = teacher?.LastName
            };
        }).ToList();
    }
}
