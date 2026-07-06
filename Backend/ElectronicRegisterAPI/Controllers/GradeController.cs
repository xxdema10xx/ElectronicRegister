using ElectronicRegisterAPI.DTOs;
using ElectronicRegisterAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Metadata;
using System.Runtime.ConstrainedExecution;
using System.Security.Claims;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ElectronicRegisterAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class GradeController : ControllerBase
    {
        private readonly ElectronicRegisterContext _context;

        public GradeController(ElectronicRegisterContext context)
        {
            _context = context;
        }

        private static List<Grade> _grades = new List<Grade>();

        [HttpGet("count")]
        [Authorize(Roles = "teacher,admin")]
        public async Task<ActionResult<int>> Count()
        {
            if (User.IsInRole("teacher"))
            {
                var teacherId = Guid.Parse(User.FindFirst("teacherId")?.Value!);
                return Ok(await _context.Grades.CountAsync(g => g.TeacherId == teacherId));
            }
            return Ok(await _context.Grades.CountAsync());
        }

        [HttpGet]
        [Authorize(Roles = "teacher,admin,student")]
        public async Task<ActionResult<List<GradeDto>>> GetAll()
        {
            var query = _context.Grades.AsQueryable();

            if (User.IsInRole("student"))
            {
                var studentId = Guid.Parse(User.FindFirst("studentId")?.Value!);
                query = query.Where(g => g.StudentId == studentId);
            }
            else if (User.IsInRole("teacher"))
            {
                var teacherId = Guid.Parse(User.FindFirst("teacherId")?.Value!);
                query = query.Where(g => g.TeacherId == teacherId);
            }

            var grades = await query.Select(g => new GradeDto
            {
                Id = g.Id,
                StudentId = g.StudentId,
                SubjectId = g.SubjectId,
                SubjectName = _context.Subjects.FirstOrDefault(s => s.Id == g.SubjectId)!.Name,
                TeacherId = g.TeacherId,
                Value = g.Value,
                Date = g.Date,
                Student = _context.Students.FirstOrDefault(s => s.Id == g.StudentId) != null ? new StudentDto
                {
                    Id = _context.Students.FirstOrDefault(s => s.Id == g.StudentId)!.Id,
                    FirstName = _context.Students.FirstOrDefault(s => s.Id == g.StudentId)!.FirstName,
                    LastName = _context.Students.FirstOrDefault(s => s.Id == g.StudentId)!.LastName
                } : null
            }).ToListAsync();

            if (grades.Count == 0) return NotFound(); //AGGIUNGERE QUESTA RIGA A TUTTI GLI ALTRI CONTROLLER
            return Ok(grades);
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "teacher,admin,student")]
        public async Task<ActionResult<GradeDto>> GetById(Guid id)
        {
            var grade = await _context.Grades.FindAsync(id);
            if (grade == null) return NotFound();

            if (User.IsInRole("student"))
            {
                var studentId = Guid.Parse(User.FindFirst("studentId")?.Value!);
                if (grade.StudentId != studentId)
                    return Forbid();
            }
            else if (User.IsInRole("teacher"))
            {
                var teacherId = Guid.Parse(User.FindFirst("teacherId")?.Value!);
                if (grade.TeacherId != teacherId)
                    return Forbid();
            }

            return Ok(new GradeDto
            {
                Id = grade.Id,
                StudentId = grade.StudentId,
                SubjectId = grade.SubjectId,
                SubjectName = _context.Subjects.FirstOrDefault(s => s.Id == grade.SubjectId)!.Name,
                TeacherId = grade.TeacherId,
                Value = grade.Value,
                Date = grade.Date,
                Student = _context.Students.FirstOrDefault(s => s.Id == grade.StudentId) != null ? new StudentDto
                {
                    Id = _context.Students.FirstOrDefault(s => s.Id == grade.StudentId)!.Id,
                    FirstName = _context.Students.FirstOrDefault(s => s.Id == grade.StudentId)!.FirstName,
                    LastName = _context.Students.FirstOrDefault(s => s.Id == grade.StudentId)!.LastName
                } : null
            });
        }

        [HttpGet("bystudentid/{id}")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<List<GradeDto>>> GetGradesByStudentId(Guid id)
        {
            var student = await _context.Students.FindAsync(id);
            if (student == null) return NotFound();
            var query = _context.Grades.Where(g => g.StudentId == student.Id);
            var grades = await query.Select(g => new GradeDto
            {
                Id = g.Id,
                StudentId = g.StudentId,
                SubjectId = g.SubjectId,
                SubjectName = _context.Subjects.FirstOrDefault(s => s.Id == g.SubjectId)!.Name,
                TeacherId = g.TeacherId,
                Value = g.Value,
                Date = g.Date,
                Student = _context.Students.FirstOrDefault(s => s.Id == g.StudentId) != null ? new StudentDto
                {
                    Id = _context.Students.FirstOrDefault(s => s.Id == g.StudentId)!.Id,
                    FirstName = _context.Students.FirstOrDefault(s => s.Id == g.StudentId)!.FirstName,
                    LastName = _context.Students.FirstOrDefault(s => s.Id == g.StudentId)!.LastName
                } : null
            }).ToListAsync();
            if (grades.Count == 0) return NotFound();
            return Ok(grades);
        }

        [HttpGet("bysubject/{subject}")]
        [Authorize(Roles = "teacher,admin,student")]
        public async Task<ActionResult<List<GradeDto>>> GetGradesBySubjectName(string subject)
        {
            //1.Cerca il subject per nome nel db
            var subjectQuery = await _context.Subjects.FirstOrDefaultAsync(s => s.Name == subject);
            //2.Se non esiste → restituisci NotFound
            if (subjectQuery == null) return NotFound();
            //3.Filtra i grades per SubjectId trovato
            var query = _context.Grades.Where(g => g.SubjectId == subjectQuery.Id);
            //4 student case
            if (User.IsInRole("student"))
            {
                var studentId = Guid.Parse(User.FindFirst("studentId")?.Value!);
                query = query.Where(g => g.StudentId == studentId);
            } 
            else if (User.IsInRole("teacher")) //5 teacher case
            {
                var teacherId = Guid.Parse(User.FindFirst("teacherId")?.Value!);
                query = query.Where(g => g.TeacherId == teacherId);
            }
            //6.Mappa i risultati in GradeDto
            var grades = await query.Select(g => new GradeDto
            {
                Id = g.Id,
                StudentId = g.StudentId,
                SubjectId = g.SubjectId,
                SubjectName = _context.Subjects.FirstOrDefault(s => s.Id == g.SubjectId)!.Name,
                TeacherId = g.TeacherId,
                Value = g.Value,
                Date = g.Date,
                Student = _context.Students.FirstOrDefault(s => s.Id == g.StudentId) != null ? new StudentDto
                {
                    Id = _context.Students.FirstOrDefault(s => s.Id == g.StudentId)!.Id,
                    FirstName = _context.Students.FirstOrDefault(s => s.Id == g.StudentId)!.FirstName,
                    LastName = _context.Students.FirstOrDefault(s => s.Id == g.StudentId)!.LastName
                } : null
            }).ToListAsync();

            //7.Restituisci la lista
            if (grades.Count == 0) return NotFound();
            return Ok(grades);
        }

        [HttpGet("bydate/{date}")]
        [Authorize(Roles = "teacher,admin,student")]
        public async Task<ActionResult<List<GradeDto>>> GetGradesByDate(DateOnly date)
        {
            //2. Filtra i grades per data
            var query = _context.Grades.Where(g => g.Date == date);
            //3. Se student → filtra anche per studentId dal token
            if (User.IsInRole("student"))
            {
                var studentId = Guid.Parse(User.FindFirst("studentId")?.Value!);
                query = query.Where(g => g.StudentId == studentId);
            }
            else if (User.IsInRole("teacher")) //5 teacher case
            {
                var teacherId = Guid.Parse(User.FindFirst("teacherId")?.Value!);
                query = query.Where(g => g.TeacherId == teacherId);
            }
            //5.Mappa i risultati in GradeDto
            var grades = await query.Select(g => new GradeDto
            {
                Id = g.Id,
                StudentId = g.StudentId,
                SubjectId = g.SubjectId,
                SubjectName = _context.Subjects.FirstOrDefault(s => s.Id == g.SubjectId)!.Name,
                TeacherId = g.TeacherId,
                Value = g.Value,
                Date = g.Date,
                Student = _context.Students.FirstOrDefault(s => s.Id == g.StudentId) != null ? new StudentDto
                {
                    Id = _context.Students.FirstOrDefault(s => s.Id == g.StudentId)!.Id,
                    FirstName = _context.Students.FirstOrDefault(s => s.Id == g.StudentId)!.FirstName,
                    LastName = _context.Students.FirstOrDefault(s => s.Id == g.StudentId)!.LastName
                } : null
            }).ToListAsync();
            //6.Restituisci la lista
            if (grades.Count == 0) return NotFound();
            return Ok(grades);
        }

        [HttpPut("update/{id}")]
        [Authorize(Roles = "teacher,admin")]
        public async Task<ActionResult> Update(Guid id, UpdateGradeDto dto)
        { 
            var grade = await _context.Grades.FindAsync(id);
            if (grade == null) return NotFound();
            grade.SubjectId = dto.SubjectId;
            grade.Value = dto.Value;
            grade.Date = dto.Date;
            _context.Grades.Update(grade);
            await _context.SaveChangesAsync();
            return NoContent();
        }


        [HttpPost]
        [Authorize(Roles = "teacher,admin")]
        public async Task<ActionResult> Add(Grade grade)
        {
            grade.Id = Guid.NewGuid();
            _context.Grades.Add(grade);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetById), new { id = grade.Id }, grade);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult> Delete(Guid id)
        {
            var grade = _grades.FirstOrDefault(g => g.Id == id);
            if (grade == null) return NotFound();
            _context.Grades.Remove(grade);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
