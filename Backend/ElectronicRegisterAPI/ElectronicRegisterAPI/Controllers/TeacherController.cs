using ElectronicRegisterAPI.DTOs;
using ElectronicRegisterAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ElectronicRegisterAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TeacherController : ControllerBase
    {
        private readonly ElectronicRegisterContext _context;

        public TeacherController(ElectronicRegisterContext context)
        {
            _context = context;
        }

        [HttpGet("count")]
        [Authorize(Roles = "teacher,admin")]
        public async Task<ActionResult<int>> Count()
        {
            return Ok(await _context.Teachers.CountAsync());
        }

        [HttpGet]
        [Authorize(Roles = "teacher,admin,student")]
        public async Task<ActionResult<List<TeacherDto>>> GetAll()
        {
            var teachers = await _context.Teachers.Select(
            s => new TeacherDto
            {
                Id = s.Id,
                FirstName = s.FirstName,
                LastName = s.LastName
            }).ToListAsync();
            if (teachers.Count == 0) return NotFound();
            return Ok(teachers);
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "teacher,admin,student")]
        public async Task<ActionResult<TeacherDto>> GetById(Guid id)
        {
            var teacher = await _context.Teachers.Where(s => s.Id == id).Select(
            s => new TeacherDto
            {
                Id = s.Id,
                FirstName = s.FirstName,
                LastName = s.LastName
            }).FirstOrDefaultAsync();
            if (teacher == null) return NotFound();
            return Ok(teacher);
        }

        [HttpPut("update/{id}")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult> Update(Guid id, UpdateTeacherDto dto)
        {
            var teacher = await _context.Teachers.FindAsync(id);
            if (teacher == null) return NotFound();
            teacher.FirstName = dto.FirstName;
            teacher.LastName = dto.LastName;
            _context.Teachers.Update(teacher);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult> Add(Teacher teacher)
        {
            teacher.Id = Guid.NewGuid();
            _context.Teachers.Add(teacher);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetById), new { id = teacher.Id }, teacher);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult> Delete(Guid id)
        {
            var teacher = await _context.Teachers.FindAsync(id);
            if (teacher == null) return NotFound();
            _context.Teachers.Remove(teacher);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}