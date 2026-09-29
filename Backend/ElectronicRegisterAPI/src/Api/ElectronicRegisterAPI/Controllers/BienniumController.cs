using ElectronicRegisterAPI.Api.Controllers;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectronicRegisterAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BienniumController : ApiControllerBase
    {
        private readonly IBienniumManager _bienniumManager;
        private readonly ISubjectManager _subjectManager;

        public BienniumController(IBienniumManager bienniumManager, ISubjectManager subjectManager)
        {
            _bienniumManager = bienniumManager;
            _subjectManager = subjectManager;
        }

        [HttpGet("count")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<int>> Count()
        {
            return Ok(await _bienniumManager.CountAsync());
        }

        [HttpGet]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<List<SubjectDto>>> GetAll()
        {
            var subjects = await _bienniumManager.GetAllAsync();
            return subjects.Count == 0 ? NotFound() : Ok(subjects);
        }

        //[HttpGet("active")]
        //[Authorize(Roles = "admin")]
        //public async Task<ActionResult<List<SubjectDto>>> GetActiveBiennia()
        //{
        //    var subjects = await _bienniumManager.GetActiveBienniaAsync();
        //    return subjects.Count == 0 ? NotFound() : Ok(subjects);
        //}

        [HttpGet("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<SubjectDto>> GetById(Guid id)
        {
            var subject = await _bienniumManager.GetByIdAsync(id);
            return subject is null ? NotFound() : Ok(subject);
        }

        [HttpGet("byname/{name}")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<SubjectDto>> GetSubjectByName(string name)
        {
            var subject = await _subjectManager.GetSubjectByNameAsync(name, CurrentCaller());
            return subject is null ? NotFound() : Ok(subject);
        }

        [HttpGet("byteacher/{id}")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<List<SubjectDto>>> GetSubjectByTeacherId(Guid id)
        {
            var subjects = await _subjectManager.GetSubjectsByTeacherIdAsync(id, CurrentCaller());
            return subjects.Count == 0 ? NotFound() : Ok(subjects);
        }

        [HttpPut("update/{id}")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult> Update(Guid id, UpdateBienniumDto dto)
        {
            var updated = await _bienniumManager.UpdateAsync(id, dto);
            return updated ? NoContent() : NotFound();
        }

        [HttpPost]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult> Add(CreateBienniumDto dto)
        {
            var id = await _bienniumManager.AddAsync(dto);
            return id ? CreatedAtAction(nameof(GetById), new { id }, id) : NotFound();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult> Delete(Guid id)
        {
            var deleted = await _bienniumManager.DeleteAsync(id);
            return deleted ? NoContent() : NotFound();
        }
    }
}
