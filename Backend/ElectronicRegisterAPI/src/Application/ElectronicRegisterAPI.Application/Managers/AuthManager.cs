using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using ElectronicRegisterAPI.Domain.Models;
using System.Security.Claims;

namespace ElectronicRegisterAPI.Application.Managers;

internal class AuthManager : IAuthManager
{
    private readonly IUserRepository _userRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly ITeacherRepository _teacherRepository;
    private readonly IUserService _userService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IMicrosoftIdentityLink _microsoftIdentityLink;
    private readonly ITransactionManager _transactionManager;
    private readonly ILegacyAccess _legacyAccess;

    public AuthManager(
        IUserRepository userRepository,
        IStudentRepository studentRepository,
        ITeacherRepository teacherRepository,
        IUserService userService,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        IMicrosoftIdentityLink microsoftIdentityLink,
        ITransactionManager transactionManager, ILegacyAccess legacyAccess)
    {
        _userRepository = userRepository;
        _studentRepository = studentRepository;
        _teacherRepository = teacherRepository;
        _userService = userService;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _microsoftIdentityLink = microsoftIdentityLink;
        _transactionManager = transactionManager;
        _legacyAccess = legacyAccess;
    }

    public async Task<string?> LoginAsync(LoginDto dto)
    {
        var user = await _userRepository.GetByEmailAsync(dto.Email);
        if (user is null || !_passwordHasher.Verify(dto.Password, user.PasswordHash) || !await _legacyAccess.IsAllowedAsync(user.Id))
            return null;

        return _jwtTokenGenerator.Generate(user.Id, user.Email, user.Role, user.StudentId, user.TeacherId);
    }

    public async Task<string?> MicrosoftLoginAsync(MicrosoftLoginDto dto)
    {
        var userId = await _microsoftIdentityLink.AuthenticateAsync(dto.AccessToken);
        if (!userId.HasValue) return null;
        var user = await _userRepository.GetByIdAsync(userId.Value);
        if (user is null || !await _legacyAccess.IsAllowedAsync(user.Id)) return null;

        return _jwtTokenGenerator.Generate(user.Id, user.Email, user.Role, user.StudentId, user.TeacherId);
    }

    public Task RegisterAsync(RegisterDto dto) =>
        _transactionManager.ExecuteAsync(() => RegisterCoreAsync(dto));

    private async Task RegisterCoreAsync(RegisterDto dto)
    {
        await _userService.EnsureEmailIsAvailableAsync(dto.Email);
        _userService.EnsureValidPassword(dto.Password);
        _userService.EnsureValidName(dto.FirstName, dto.LastName);
        _userService.EnsureSelfRegistrationEmailFormat(dto.Email);

        var student = new Student
        {
            Id = Guid.NewGuid(),
            FirstName = dto.FirstName!,
            LastName = dto.LastName!
        };

        await _studentRepository.AddAsync(student);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = dto.Email,
            PasswordHash = _passwordHasher.Hash(dto.Password),
            Role = UserRole.Student,
            StudentId = student.Id
        };
        await _userRepository.AddAsync(user);
    }

    public Task RegisterForAdminAsync(RegisterForAdminDto dto) =>
        _transactionManager.ExecuteAsync(() => RegisterForAdminCoreAsync(dto));

    private async Task RegisterForAdminCoreAsync(RegisterForAdminDto dto)
    {
        await _userService.EnsureEmailIsAvailableAsync(dto.Email);
        _userService.EnsureValidRole(dto.Role);
        _userService.EnsureValidPassword(dto.Password);

        var role = _userService.ParseRole(dto.Role);
        Guid? studentId = null;
        Guid? teacherId = null;

        if (role == UserRole.Student)
        {
            _userService.EnsureValidName(dto.FirstName, dto.LastName);

            var student = new Student { Id = Guid.NewGuid(), FirstName = dto.FirstName!, LastName = dto.LastName! };
            await _studentRepository.AddAsync(student);
            studentId = student.Id;
        }
        else if (role == UserRole.Teacher)
        {
            _userService.EnsureValidName(dto.FirstName, dto.LastName);

            var teacher = new Teacher { Id = Guid.NewGuid(), FirstName = dto.FirstName!, LastName = dto.LastName! };
            await _teacherRepository.AddAsync(teacher);
            teacherId = teacher.Id;
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = dto.Email,
            PasswordHash = _passwordHasher.Hash(dto.Password),
            Role = role,
            StudentId = studentId,
            TeacherId = teacherId
        };
        await _userRepository.AddAsync(user);
    }

    public async Task<UserDto?> GetCurrentUserAsync(ClaimsContext caller)
    {
        var user = await _userRepository.GetByIdAsync(caller.UserId);
        if (user is null) return null;

        var student = user.StudentId.HasValue ? await _studentRepository.GetByIdAsync(user.StudentId.Value) : null;
        var teacher = user.TeacherId.HasValue ? await _teacherRepository.GetByIdAsync(user.TeacherId.Value) : null;

        return new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            Role = user.Role.ToString().ToLowerInvariant(),
            StudentId = user.StudentId,
            StudentFirstName = student?.FirstName,
            StudentLastName = student?.LastName,
            TeacherId = user.TeacherId,
            TeacherFirstName = teacher?.FirstName,
            TeacherLastName = teacher?.LastName
        };
    }
}
