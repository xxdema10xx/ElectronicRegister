using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ElectronicRegisterAPI.Models;
using ElectronicRegisterAPI.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace ElectronicRegisterAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SubjectController : ControllerBase
    {
        private readonly ElectronicRegisterContext _context;
        private static List<Subject> _subjects = new List<Subject>();

        public SubjectController(ElectronicRegisterContext context)
        {
            _context = context;
        }

        [HttpGet("count")]
        [Authorize(Roles = "teacher,admin,student")]
        public async Task<ActionResult<int>> Count()
        {
            if (User.IsInRole("teacher"))
            {
                var teacherId = Guid.Parse(User.FindFirst("teacherId")?.Value!);
                return Ok(await _context.Subjects.CountAsync(s => s.TeacherId == teacherId));
            }
            return Ok(await _context.Subjects.CountAsync());
        }

        [HttpGet]
        [Authorize(Roles = "teacher,admin,student")]
        public async Task<ActionResult<List<SubjectDto>>> GetAll()
        {
            if (User.IsInRole("teacher"))
            {
                var teacherId = Guid.Parse(User.FindFirst("teacherId")?.Value!);
                var teacherSubjects = await _context.Subjects
                    .Where(s => s.TeacherId == teacherId)
                    .Select(s => new SubjectDto
                    {
                        Id = s.Id,
                        Name = s.Name,
                        TeacherId = s.TeacherId,
                        TeacherFirstName = s.Teacher != null ? s.Teacher.FirstName : null,
                        TeacherLastName = s.Teacher != null ? s.Teacher.LastName : null
                    })
                    .ToListAsync();
                if (teacherSubjects.Count == 0) return NotFound();
                return Ok(teacherSubjects);
            }
            var subjects = await _context.Subjects.Select(
            s => new SubjectDto
            {
                Id = s.Id,
                Name = s.Name,
                TeacherId = s.TeacherId,
                TeacherFirstName = s.Teacher != null ? s.Teacher.FirstName : null,
                TeacherLastName = s.Teacher != null ? s.Teacher.LastName : null
            }).ToListAsync();
            if (subjects.Count == 0) return NotFound();
            return Ok(subjects);
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "teacher,admin,student")]
        public async Task<ActionResult<SubjectDto>> GetById(Guid id)
        {
            var subject = await _context.Subjects.Where(s => s.Id == id).Select(
            s => new SubjectDto
            {
                Id = s.Id,
                Name = s.Name,
                TeacherId = s.TeacherId,
                TeacherFirstName = s.Teacher != null ? s.Teacher.FirstName : null,
                TeacherLastName = s.Teacher != null ? s.Teacher.LastName : null
            }).FirstOrDefaultAsync();
            if (subject == null) return NotFound();
            return Ok(subject);
        }

        [HttpGet("byname/{name}")]
        [Authorize(Roles = "teacher,admin,student")]
        public async Task<ActionResult<SubjectDto>> GetSubjectByName(string name)
        {
            var subject = await _context.Subjects.Include(s => s.Teacher).FirstOrDefaultAsync(s => s.Name == name);
            if (subject == null) return NotFound();
            return Ok(new SubjectDto
            {
                Id = subject.Id,
                Name = subject.Name,
                TeacherId = subject.TeacherId,
                TeacherFirstName = subject.Teacher != null ? subject.Teacher.FirstName : null,
                TeacherLastName = subject.Teacher != null ? subject.Teacher.LastName : null
            });
        }

        [HttpGet("byteacher/{id}")]
        [Authorize(Roles = "teacher,admin,student")]
        public async Task<ActionResult<List<SubjectDto>>> GetSubjectByTeacherId(Guid id)
        {
            var subject = await _context.Subjects
                .Include(t => t.Teacher)
                .Where(s => s.TeacherId == id)
                .Select(
                    s => new SubjectDto
                    {
                        Id = s.Id,
                        Name = s.Name,
                        TeacherId = s.TeacherId,
                        TeacherFirstName = s.Teacher != null ? s.Teacher.FirstName : null,
                        TeacherLastName = s.Teacher != null ? s.Teacher.LastName : null
                    })
                .ToListAsync();
            if (subject.Count == 0) return NotFound();
            return Ok(subject);
        }

        [HttpPut("update/{id}")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult> Update(Guid id, UpdateSubjectDto dto)
        {
            var subject = await _context.Subjects.FindAsync(id);
            if(subject == null) return NotFound();
            //Check if teacher exists
            var teacher = await _context.Teachers.FindAsync(dto.TeacherId);
            if(teacher == null) return NotFound();
            //Update
            subject.TeacherId = teacher.Id;
            subject.Name = dto.Name;
            _context.Subjects.Update(subject);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult> Add(AddSubjectDto subjectDto)
        {
            //Check if teacher exists
            var teacher = await _context.Teachers.FindAsync(subjectDto.TeacherId);
            if(teacher == null) return NotFound();
            //Create new subject
            var subject = new Subject
            {
                Id = Guid.NewGuid(),
                Name = subjectDto.Name,
                TeacherId = teacher.Id
            };
            _context.Subjects.Add(subject);
            await _context.SaveChangesAsync();

            var result = new SubjectDto
            {
                Id = subject.Id,
                Name = subject.Name,
                TeacherId = subject.TeacherId
            };

            return CreatedAtAction(nameof(GetById), new { id = subject.Id }, result);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult> Delete(Guid id)
        {
            var subject = await _context.Subjects.FindAsync(id);
            if (subject == null) return NotFound();
            _context.Subjects.Remove(subject);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}

