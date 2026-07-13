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

        [HttpGet("statistics")]
        [Authorize(Roles = "admin,teacher,student")]
        public async Task<ActionResult<GradeStatisticsDto>> GetStatistics()
        {
            IQueryable<Grade> query = _context.Grades;

            if (User.IsInRole("student"))
            {
                var studentId = Guid.Parse(User.FindFirst("studentId")!.Value);
                query = query.Where(g => g.StudentId == studentId);
            }
            else if (User.IsInRole("teacher"))
            {
                var teacherId = Guid.Parse(User.FindFirst("teacherId")!.Value);
                query = query.Where(g => g.TeacherId == teacherId);
            }

            if (!await query.AnyAsync())
                return NotFound();

            var yearlyAverage = await query.AverageAsync(g => g.Value);

            var monthlyData = await query
                .GroupBy(g => g.Date.Month)
                .Select(g => new
                {
                    Month = g.Key,
                    Average = g.Average(x => x.Value)
                })
                .ToListAsync();

            var monthlyAverage = new decimal?[12];

            foreach (var item in monthlyData)
            {
                monthlyAverage[item.Month - 1] = item.Average;
            }

            return Ok(new GradeStatisticsDto
            {
                yearlyAverage = yearlyAverage,
                monthlyAverage = monthlyAverage
            });
        }

        [HttpGet("filters")]
        [Authorize(Roles = "teacher,admin,student")]
        public async Task<ActionResult<GradeFiltersDto>> GetFilters()
        {
            var gradesQuery = _context.Grades.AsQueryable();

            if (User.IsInRole("student"))
            {
                var claimStudentId = Guid.Parse(User.FindFirst("studentId")?.Value!);
                gradesQuery = gradesQuery.Where(g => g.StudentId == claimStudentId);
            }
            else if (User.IsInRole("teacher"))
            {
                var claimTeacherId = Guid.Parse(User.FindFirst("teacherId")?.Value!);
                gradesQuery = gradesQuery.Where(g => g.TeacherId == claimTeacherId);
            }

            var subjectIds = await gradesQuery.Select(g => g.SubjectId).Distinct().ToListAsync();
            var studentIds = await gradesQuery.Select(g => g.StudentId).Distinct().ToListAsync();

            var subjects = await _context.Subjects
                .Where(s => subjectIds.Contains(s.Id))
                .Select(s => new SubjectDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    TeacherId = s.TeacherId,
                    TeacherFirstName = s.Teacher != null ? s.Teacher.FirstName : null,
                    TeacherLastName = s.Teacher != null ? s.Teacher.LastName : null
                })
                .OrderBy(s => s.Name)
                .ToListAsync();

            var students = await _context.Students
                .Where(s => studentIds.Contains(s.Id))
                .Select(s => new StudentDto
                {
                    Id = s.Id,
                    FirstName = s.FirstName,
                    LastName = s.LastName
                })
                .OrderBy(s => s.FirstName).ThenBy(s => s.LastName)
                .ToListAsync();

            return Ok(new GradeFiltersDto { Subjects = subjects, Students = students });
        }

        [HttpGet("paged")]
        [Authorize(Roles = "teacher,admin,student")]
        public async Task<ActionResult<GradePageDto>> GetPaged(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] Guid? subjectId = null,
            [FromQuery] Guid? studentId = null,
            [FromQuery] DateOnly? date = null)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var query = _context.Grades.AsQueryable();

            if (User.IsInRole("student"))
            {
                var claimStudentId = Guid.Parse(User.FindFirst("studentId")?.Value!);
                query = query.Where(g => g.StudentId == claimStudentId);
            }
            else if (User.IsInRole("teacher"))
            {
                var claimTeacherId = Guid.Parse(User.FindFirst("teacherId")?.Value!);
                query = query.Where(g => g.TeacherId == claimTeacherId);
            }

            if (subjectId.HasValue) query = query.Where(g => g.SubjectId == subjectId.Value);
            if (studentId.HasValue) query = query.Where(g => g.StudentId == studentId.Value);
            if (date.HasValue) query = query.Where(g => g.Date == date.Value);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(g => g.Date)
                    .ThenBy(g => g.SubjectId)
                    .ThenBy(g => g.Value)
                    .ThenBy(g => g.StudentId)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(g => new GradeDto
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
                })
                .ToListAsync();

            return Ok(new GradePageDto { Items = items, TotalCount = totalCount });
        }

        //NEW GET ALL METHOD: The following code is the new GetAll() method that includes role-based filtering for students and teachers.
        // It replaces the old GetAll() method that was commented out below. 
        // The new method ensures that students can only see their own grades and teachers can only see grades for their own students.
        [HttpGet]
        [Authorize(Roles = "teacher,admin,student")]
        public async Task<ActionResult<List<GradeDto>>> GetAll()
        {
            var query = _context.Grades.AsQueryable();

            if (User.IsInRole("student"))
            {
                var studentIdClaim = User.FindFirst("studentId")?.Value;

                if (!Guid.TryParse(studentIdClaim, out var studentId))
                {
                    return Unauthorized("Token non valido.");
                }

                query = query.Where(g => g.StudentId == studentId);
            }
            else if (User.IsInRole("teacher"))
            {
                var teacherIdClaim = User.FindFirst("teacherId")?.Value;

                if (!Guid.TryParse(teacherIdClaim, out var teacherId))
                {
                    return Unauthorized("Token non valido.");
                }

                query = query.Where(g => g.TeacherId == teacherId);
            }

            var grades = await query
                .Select(g => new GradeDto
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
                    }
                    )
                .OrderByDescending(g => g.Date)
                    .ThenBy(g => g.SubjectName)
                    .ThenBy(g => g.Value)
                    .ThenBy(g => g.StudentId)
            .ToListAsync();

            // Nota: uno studente senza voti NON è un errore, è un caso normale
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
            if(User.IsInRole("teacher"))
            {
                var teacherId = Guid.Parse(User.FindFirst("teacherId")?.Value!);
                var gradeToUpdate = await _context.Grades.FindAsync(id);
                if (gradeToUpdate == null) return NotFound();
                if (gradeToUpdate.TeacherId != teacherId) return Forbid();
            }
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
        public async Task<ActionResult> Add(AddGradeDto gradeDto)
        {
            if(User.IsInRole("teacher"))
            {
                var teacherId = Guid.Parse(User.FindFirst("teacherId")?.Value!);
                // Check if the teacher is actually teaching the subject
                var subject = await _context.Subjects.FindAsync(gradeDto.SubjectId);
                if (subject == null || subject.TeacherId != teacherId)
                {
                    return Forbid();
                }
            }

            var teacher = await _context.Teachers.FindAsync(_context.Subjects.FirstOrDefault(s => s.Id == gradeDto.SubjectId)?.TeacherId);
            if (teacher == null) return NotFound();
            var grade = new Grade
            {
                Id = Guid.NewGuid(),
                StudentId = gradeDto.StudentId,
                SubjectId = gradeDto.SubjectId,
                TeacherId = teacher.Id,
                Value = gradeDto.Value,
                Date = gradeDto.Date
            };
            _context.Grades.Add(grade);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetById), new { id = grade.Id }, grade);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult> Delete(Guid id)
        {
            var grade = await _context.Grades.FindAsync(id);
            if (grade == null) return NotFound();
            _context.Grades.Remove(grade);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
