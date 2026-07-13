using ElectronicRegisterAPI.DTOs;
using ElectronicRegisterAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Runtime.ConstrainedExecution;

namespace ElectronicRegisterAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class StudentController : ControllerBase
    {
        private readonly ElectronicRegisterContext _context;

        public StudentController(ElectronicRegisterContext context)
        {
            _context = context;
        }

        [HttpGet("count")]
        [Authorize(Roles = "teacher,admin")]
        public async Task<ActionResult<int>> Count()
        {
            return Ok(await _context.Students.CountAsync());
        }

        [HttpGet]
        [Authorize(Roles = "teacher,admin")]
        public async Task<ActionResult<List<StudentDto>>> GetAll()
        {
            var students = await _context.Students.Select(
            s => new StudentDto
            {
                Id = s.Id,
                FirstName = s.FirstName,
                LastName = s.LastName
            }).ToListAsync();
            if (students.Count == 0) return NotFound();
            return Ok(students);
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "teacher,admin")]
        public async Task<ActionResult<StudentDto>> GetById(Guid id)
        {
            var student = await _context.Students.Where(s=> s.Id == id).Select(
            s => new StudentDto
            {
                Id = s.Id,
                FirstName = s.FirstName,
                LastName = s.LastName
            }).FirstOrDefaultAsync();
            if (student == null) return NotFound();
            return Ok(student);
        }

        [HttpGet("bylastname/{lastName}")]
        [Authorize(Roles = "teacher,admin")]
        public async Task<ActionResult<List<StudentDto>>> GetStudentsByName(string lastName)
        {
            var students = await _context.Students.Where(s => s.LastName == lastName).Select(s => new StudentDto
            {
                Id = s.Id,
                FirstName = s.FirstName,
                LastName = s.LastName
            }).ToListAsync();
            if (students.Count == 0) return NotFound();
            return Ok(students);
        }

        [HttpPut("update/{id}")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult> Update(Guid id, UpdateStudentDto dto)
        {
            //1.Cerca lo studente nel db per id
            var student = await _context.Students.FindAsync(id);
            //2.Se non esiste → restituisci NotFound
            if (student == null) return NotFound();
            //3.Aggiorna i campi FirstName e LastName con i valori ricevuti
            student.FirstName = dto.FirstName;
            student.LastName = dto.LastName;
            //4.Salva le modifiche nel db
            _context.Students.Update(student);
            await _context.SaveChangesAsync();
            //5.Restituisci NoContent(204)
            return NoContent();
        }

        [HttpPost]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult> Add(Student student)
        {
            student.Id = Guid.NewGuid();
            _context.Students.Add(student);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetById), new { id = student.Id }, student);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult> Delete(Guid id)
        {
            var student = await _context.Students.FindAsync(id);
            if (student == null) return NotFound();

            if (await _context.Grades.AnyAsync(g => g.StudentId == id))
                return BadRequest("Impossibile eliminare l'allievo: ha voti assegnati.");

            _context.Students.Remove(student);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}