using BCrypt.Net;
using ElectronicRegisterAPI.DTOs;
using ElectronicRegisterAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Bcrypt = BCrypt.Net.BCrypt;

namespace ElectronicRegisterAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly ElectronicRegisterContext _context;

        public UsersController(ElectronicRegisterContext context)
        {
            _context = context;
        }

        [HttpGet("count")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<int>> Count()
        {
            return Ok(await _context.Users.CountAsync());
        }

        [HttpGet]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<List<UserDto>>> GetAll()
        {
            var users = await _context.Users
                .Include(u => u.Student)
                .Include(u => u.Teacher)
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
                .ToListAsync();
            if (users.Count == 0) return NotFound();
            return Ok(users);
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<UserDto>> GetById(Guid id)
        {
            var user = await _context.Users
                .Include(u => u.Student)
                .Include(u => u.Teacher)
                .Where(u => u.Id == id)
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

        [HttpPut("update/{id}")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult> Update(Guid id, UpdateUserDto dto)
        {
            var user = await _context.Users.FindAsync(id);
            if(user == null) return NotFound();
            if(dto.Email == null || dto.Email == "") return BadRequest("Email is required");
            if (!User.IsInRole("admin")) return Forbid();
            if (dto.Email != null) user.Email = dto.Email;
            if (dto.Password != null) user.PasswordHash = Bcrypt.HashPassword(dto.Password);
            //9. Se dto.FirstName != null → aggiorna FirstName nella tabella student o teacher collegata
            //10.Se dto.LastName != null → aggiorna LastName nella tabella student o teacher collegata
            if (dto.FirstName != null || dto.LastName != null)
            {
                if (user.StudentId != null)
                {
                    var student = await _context.Students.FindAsync(user.StudentId);
                    if (student == null) return NotFound();
                    if (dto.FirstName != null) student.FirstName = dto.FirstName;
                    if (dto.LastName != null) student.LastName = dto.LastName;
                }
                else if (user.TeacherId != null)
                {
                    var teacher = await _context.Teachers.FindAsync(user.TeacherId);
                    if (teacher == null) return NotFound();
                    if (dto.FirstName != null) teacher.FirstName = dto.FirstName;
                    if (dto.LastName != null) teacher.LastName = dto.LastName;
                }
            }
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private static bool HasSpecialChar(string password)
        {
            return password.Any(c => "!@#$%^&*()_-+=<>?/[]{}".Contains(c));
        }

        [HttpPut("updatepassword/{id}")]
        [Authorize(Roles = "student,teacher,admin")]
        public async Task<ActionResult> UpdatePassword(Guid id, UpdatePasswordDto dto)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();
            if (!User.IsInRole("admin") && user.Id != id) return Forbid();
            if (dto.OldPassword == null || dto.NewPassword == null) return BadRequest("Old and new passwords are required");
            if (dto.NewPassword.Length < 8) return BadRequest("Password must be at least 8 characters long");
            if (!HasSpecialChar(dto.NewPassword)) return BadRequest("Password must contain at least one special character");
            if (!Bcrypt.Verify(dto.OldPassword, user.PasswordHash)) return BadRequest("Passwords don't match");
            user.PasswordHash = Bcrypt.HashPassword(dto.NewPassword);
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult> Add(CreateUserDto dto)
        {
            if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
            {
                return BadRequest("Email already exists");
            }
            if (dto.Password.Length < 8)
            {
                return BadRequest("Password must be at least 8 characters long");
            }
            if (!HasSpecialChar(dto.Password))
            {
                return BadRequest("Password must contain at least one special character");
            }
            if (dto.Email == null || dto.Email == "")
            {
                return BadRequest("Email is required");
            }
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = dto.Email,
                PasswordHash = Bcrypt.HashPassword(dto.Password),
                Role = dto.Role,
                StudentId = dto.StudentId,
                TeacherId = dto.TeacherId
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult> Delete(Guid id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}