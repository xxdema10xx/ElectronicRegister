using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ElectronicRegisterAPI.Models;
using ElectronicRegisterAPI.DTOs;
using Bcrypt = BCrypt.Net.BCrypt;
using Microsoft.AspNetCore.Authorization;

namespace ElectronicRegisterAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ElectronicRegisterContext _context;
        private readonly IConfiguration _configuration;
        private readonly ConfigurationManager<OpenIdConnectConfiguration> _msConfigManager;

        public AuthController(
            ElectronicRegisterContext context,
            IConfiguration configuration,
            ConfigurationManager<OpenIdConnectConfiguration> msConfigManager)
        {
            _context = context;
            _configuration = configuration;
            _msConfigManager = msConfigManager;
        }

        // ... (tutti i tuoi metodi esistenti: Register, RegisterForAdmin, Login, GetCurrentUser restano IDENTICI, non li ripeto qui) ...

       [HttpPost("microsoft-login")]
        public async Task<ActionResult> MicrosoftLogin([FromBody] MicrosoftLoginDto dto)
        {
	    Console.WriteLine("=== MICROSOFT LOGIN CHIAMATO ===");
            Console.WriteLine("TOKEN PRESENTE: " + (!string.IsNullOrEmpty(dto?.AccessToken)));
            ClaimsPrincipal principal;
            try
            {
                principal = await ValidateMicrosoftToken(dto.AccessToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERRORE VALIDAZIONE TOKEN MICROSOFT: " + ex.Message);
                Console.WriteLine(ex.StackTrace);
                return Unauthorized("Token Microsoft non valido: " + ex.Message);
            }

            var email = principal.FindFirst("preferred_username")?.Value
                        ?? principal.FindFirst(ClaimTypes.Email)?.Value;

            if (string.IsNullOrEmpty(email))
                return BadRequest("Email non trovata nel token Microsoft");

            var user = await _context.Users
                .Include(u => u.Student)
                .Include(u => u.Teacher)
                .FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
                return Unauthorized("Utente non registrato nel sistema. Contatta un amministratore.");

            var token = GenerateJwtToken(user);
            return Ok(new { token });
        }

        [HttpPost("login")]
        public async Task<ActionResult> Login(LoginDto dto)
        {
            var user = await _context.Users
                .Include(u => u.Student)
                .Include(u => u.Teacher)
                .FirstOrDefaultAsync(u => u.Email == dto.Email);

            if (user == null || !Bcrypt.Verify(dto.Password, user.PasswordHash))
                return Unauthorized("Credenziali non valide");

            var token = GenerateJwtToken(user);
            return Ok(new { token });
        }

        private async Task<ClaimsPrincipal> ValidateMicrosoftToken(string accessToken)
        {
            var config = await _msConfigManager.GetConfigurationAsync();

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false, // "common" ha issuer diverso per ogni tenant
                ValidateAudience = true,
                ValidAudience = _configuration["AzureAd:Audience"],
                ValidateLifetime = true,
                IssuerSigningKeys = config.SigningKeys
            };

            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(accessToken, validationParameters, out _);

            return principal;
        }

        private static bool HasSpecialChar(string password)
        {
            return password.Any(c => "!@#$%^&*()_-+=<>?/[]{}".Contains(c));
        }

        private bool IsValidName(string name, int minLength)
        {
            return !string.IsNullOrWhiteSpace(name) && name.Length >= minLength;
        }

        [HttpPost("register")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult> Register(RegisterDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) ||
                !dto.Email.StartsWith("allievo_") ||
                !dto.Email.EndsWith("@itsumbria.it"))
            {
                return BadRequest("Invalid email format!");
            }
            // controlla se email già esiste
            if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
                return BadRequest("An error occurred during registration!");

            if(string.IsNullOrEmpty(dto.Password) || dto.Password.Length < 8 || !HasSpecialChar(dto.Password))
                return BadRequest("Password is invalid!");

            // student e teacher richiedono nome e cognome
            if (!IsValidName(dto.FirstName, 3))
                return BadRequest("Your firstname is invalid!");
            if (!IsValidName(dto.LastName, 2))
                return BadRequest("Your lastname is invalid!");
            if (!IsValidName(dto.FirstName, 3) && !IsValidName(dto.LastName, 2))
            {
                return BadRequest("Your firstname and lastname are invalid!");
            }

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = dto.Email,
                PasswordHash = Bcrypt.HashPassword(dto.Password),
                Role = "student"
            };

            var student = new Student
            {
                Id = Guid.NewGuid(),
                FirstName = dto.FirstName!,
                LastName = dto.LastName!
            };
            _context.Students.Add(student);
            user.StudentId = student.Id;

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok("Registration successful!");
        }

        [HttpPost("RegisterForAdmin")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult> RegisterForAdmin(RegisterForAdminDto dto)
        {
            // controlla se email già esiste
            if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
                return BadRequest("Email already registered!");

            // valida il role
            if (dto.Role != "admin" && dto.Role != "student" && dto.Role != "teacher")
                return BadRequest("Invalid role!");

            // student e teacher richiedono nome e cognome
            if (dto.Role != "admin" && !IsValidName(dto.FirstName, 3))
                return BadRequest("Your firstname is invalid!");
            if (dto.Role != "admin" && !IsValidName(dto.LastName, 2))
                return BadRequest("Your lastname is invalid!");
            if (dto.Role != "admin" && !IsValidName(dto.FirstName, 3) && !IsValidName(dto.LastName, 2))
            {
                return BadRequest("Your firstname and lastname are invalid!");
            }

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = dto.Email,
                PasswordHash = Bcrypt.HashPassword(dto.Password),
                Role = dto.Role
            };

            if (dto.Role == "student")
            {
                var student = new Student
                {
                    Id = Guid.NewGuid(),
                    FirstName = dto.FirstName!,
                    LastName = dto.LastName!
                };
                _context.Students.Add(student);
                user.StudentId = student.Id;
            }
            else if (dto.Role == "teacher")
            {
                var teacher = new Teacher
                {
                    Id = Guid.NewGuid(),
                    FirstName = dto.FirstName!,
                    LastName = dto.LastName!
                };
                _context.Teachers.Add(teacher);
                user.TeacherId = teacher.Id;
            }

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok("Registration successful!");
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<UserDto>> GetCurrentUser()
        {
            var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (userId == null) return Unauthorized();

            var guid = Guid.Parse(userId);

            var user = await _context.Users
                .Include(u => u.Student)
                .Include(u => u.Teacher)
                .Where(u => u.Id == guid)
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Email = u.Email,
                    Role = u.Role,
                    StudentId = u.StudentId,
                    StudentFirstName = u.Student != null ? u.Student.FirstName : null,
                    StudentLastName = u.Student != null ? u.Student.LastName : null,
                    TeacherId = u.TeacherId,
                    TeacherFirstName = u.Teacher != null ? u.Teacher.FirstName : null,
                    TeacherLastName = u.Teacher != null ? u.Teacher.LastName : null,
                })
                .FirstOrDefaultAsync();

            if (user == null) return NotFound();
            return Ok(user);
        }

        private string GenerateJwtToken(User user)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            // aggiungi StudentId o TeacherId se presenti
            if (user.StudentId.HasValue)
                claims.Add(new Claim("studentId", user.StudentId.Value.ToString()));

            if (user.TeacherId.HasValue)
                claims.Add(new Claim("teacherId", user.TeacherId.Value.ToString()));

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(double.Parse(_configuration["Jwt:ExpiresInMinutes"]!)),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
